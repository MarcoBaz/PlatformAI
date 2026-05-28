using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record DepartmentDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int ProductionLinesCount);

public record CreateDepartmentRequest(
    string Code,
    string Name,
    string? Description);

public record UpdateDepartmentRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

// ── Controller ────────────────────────────────────────────────────────────────

[Route("api/departments")]
[ApiController]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DepartmentController> _logger;

    public DepartmentController(IUnitOfWork uow, ILogger<DepartmentController> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    // ── GET /api/departments ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
    {
        try
        {
            var tenantCode = await GetTenantCodeAsync();
            if (tenantCode is null) return Unauthorized();

            var raw = await _uow.Repository<Department>()
                .Query(d => d.TenantCode == tenantCode)
                .OrderBy(d => d.Code)
                .Select(d => new
                {
                    d.Id,
                    d.Code,
                    d.Name,
                    d.Description,
                    d.IsActive,
                    LinesCount = d.ProductionLines.Count()
                })
                .ToListAsync();

            var depts = raw.Select(d => new DepartmentDto(
                d.Id, d.Code, d.Name, d.Description, d.IsActive, d.LinesCount)).ToList();

            return Ok(depts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore in GetAll departments");
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace });
        }
    }

    // ── GET /api/departments/{id} ─────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> GetById(Guid id)
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var raw = await _uow.Repository<Department>()
            .Query(d => d.Id == id && d.TenantCode == tenantCode)
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.Name,
                d.Description,
                d.IsActive,
                LinesCount = d.ProductionLines.Count()
            })
            .FirstOrDefaultAsync();

        if (raw is null) return NotFound();

        var dept = new DepartmentDto(raw.Id, raw.Code, raw.Name, raw.Description, raw.IsActive, raw.LinesCount);
        return Ok(dept);
    }

    // ── POST /api/departments ─────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        // Unicità codice nel tenant
        var exists = await _uow.Repository<Department>()
            .Query(d => d.Code == req.Code && d.TenantCode == tenantCode)
            .AnyAsync();

        if (exists)
            return Conflict(new { message = $"Esiste già un dipartimento con codice '{req.Code}'." });

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Code = req.Code,
            Name = req.Name,
            Description = req.Description,
            IsActive = true,
            TenantCode = tenantCode,
        };

        await _uow.Repository<Department>().AddAsync(dept);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Dipartimento creato — id={Id} code={Code}", dept.Id, dept.Code);

        return CreatedAtAction(nameof(GetById), new { id = dept.Id },
            new DepartmentDto(dept.Id, dept.Code, dept.Name, dept.Description, dept.IsActive, 0));
    }

    // ── PUT /api/departments/{id} ─────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(
        Guid id, [FromBody] UpdateDepartmentRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var dept = await _uow.Repository<Department>()
            .Query(d => d.Id == id && d.TenantCode == tenantCode)
            .FirstOrDefaultAsync();

        if (dept is null) return NotFound();

        // Unicità codice (esclude se stesso)
        var codeConflict = await _uow.Repository<Department>()
            .Query(d => d.Code == req.Code && d.TenantCode == tenantCode && d.Id != id)
            .AnyAsync();

        if (codeConflict)
            return Conflict(new { message = $"Esiste già un dipartimento con codice '{req.Code}'." });

        dept.Code = req.Code;
        dept.Name = req.Name;
        dept.Description = req.Description;
        dept.IsActive = req.IsActive;

        await _uow.Repository<Department>().UpdateAsync(dept);
        await _uow.SaveChangesAsync();

        // Ricarica con conteggio aggiornato
        var linesCount = await _uow.Repository<ProductionLine>()
            .Query(l => l.DepartmentId == dept.Id)
            .CountAsync();

        return Ok(new DepartmentDto(dept.Id, dept.Code, dept.Name, dept.Description, dept.IsActive, linesCount));
    }

    // ── DELETE /api/departments/{id} ──────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var dept = await _uow.Repository<Department>()
            .Query(d => d.Id == id && d.TenantCode == tenantCode)
            .FirstOrDefaultAsync();

        if (dept is null) return NotFound();

        var hasLines = await _uow.Repository<ProductionLine>()
            .Query(l => l.DepartmentId == id)
            .AnyAsync();

        if (hasLines)
            return Conflict(new { message = "Impossibile eliminare: il dipartimento ha linee di produzione associate." });

        await _uow.Repository<Department>().DeleteAsync(dept);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Dipartimento eliminato — id={Id}", id);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> GetTenantCodeAsync()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(raw, out var userId)) return null;

        var user = await _uow.Repository<PlatformAI.Infrastructure.Master.User>()
            .Query(u => u.Id == userId)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync();

        return user?.Tenant?.Code;
    }

}
