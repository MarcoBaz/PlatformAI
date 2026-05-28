using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ProductionLineDto(
    Guid   Id,
    string Code,
    string Name,
    string? Description,
    bool   IsActive,
    Guid   DepartmentId,
    string DepartmentName,
    IReadOnlyList<MachineDto> Machines);

public record MachineDto(
    Guid   Id,
    string Code,
    string Name,
    string Type,
    string Status,
    Guid   ProductionLineId);

public record CreateProductionLineRequest(
    string  Code,
    string  Name,
    string? Description,
    Guid    DepartmentId);

public record UpdateProductionLineRequest(
    string  Code,
    string  Name,
    string? Description,
    bool    IsActive);

public record CreateMachineRequest(
    string Code,
    string Name,
    string Type,
    Guid   ProductionLineId);

public record UpdateMachineRequest(
    string Code,
    string Name,
    string Type,
    string Status);

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// CRUD per ProductionLine e Machine.
/// Ogni operazione è isolata al tenant dell'utente autenticato
/// (tramite la catena Machine → ProductionLine → Department → TenantCode).
/// </summary>
[Route("api/production-lines")]
[ApiController]
[Authorize]
public class ProductionLineController : ControllerBase
{
    private readonly IUnitOfWork                        _uow;
    private readonly ILogger<ProductionLineController>  _logger;

    public ProductionLineController(
        IUnitOfWork uow,
        ILogger<ProductionLineController> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    // ── GET /api/production-lines ─────────────────────────────────────────────
    // Restituisce tutte le linee del tenant con le relative macchine.

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductionLineDto>>> GetLines()
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var lines = await _uow.Repository<ProductionLine>()
            .Query(l => l.Department.TenantCode == tenantCode)
            .Include(l => l.Department)
            .Include(l => l.Machines)
            .OrderBy(l => l.Code)
            .ToListAsync();

        return Ok(lines.Select(ToDto));
    }

    // ── GET /api/production-lines/{id} ────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductionLineDto>> GetLine(Guid id)
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var line = await _uow.Repository<ProductionLine>()
            .Query(l => l.Id == id && l.Department.TenantCode == tenantCode)
            .Include(l => l.Department)
            .Include(l => l.Machines)
            .FirstOrDefaultAsync();

        return line is null ? NotFound() : Ok(ToDto(line));
    }

    // ── POST /api/production-lines ────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<ProductionLineDto>> CreateLine(
        [FromBody] CreateProductionLineRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        // Verifica che il Department appartenga al tenant
        var dept = await _uow.Repository<Department>()
            .Query(d => d.Id == req.DepartmentId && d.TenantCode == tenantCode)
            .FirstOrDefaultAsync();

        if (dept is null)
            return BadRequest(new { message = "Dipartimento non trovato o non appartenente al tenant." });

        // Unicità codice all'interno del tenant
        var exists = await _uow.Repository<ProductionLine>()
            .Query(l => l.Code == req.Code && l.Department.TenantCode == tenantCode)
            .AnyAsync();

        if (exists)
            return Conflict(new { message = $"Esiste già una linea con codice '{req.Code}'." });

        var line = new ProductionLine
        {
            Id           = Guid.NewGuid(),
            Code         = req.Code,
            Name         = req.Name,
            Description  = req.Description,
            IsActive     = true,
            DepartmentId = req.DepartmentId,
        };

        await _uow.Repository<ProductionLine>().AddAsync(line);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Linea creata — id={Id} code={Code}", line.Id, line.Code);

        // Ricarica con Include per restituire il DTO completo
        var created = await _uow.Repository<ProductionLine>()
            .Query(l => l.Id == line.Id)
            .Include(l => l.Department)
            .Include(l => l.Machines)
            .FirstOrDefaultAsync();

        return CreatedAtAction(nameof(GetLine), new { id = line.Id }, ToDto(created!));
    }

    // ── PUT /api/production-lines/{id} ────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductionLineDto>> UpdateLine(
        Guid id,
        [FromBody] UpdateProductionLineRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var line = await _uow.Repository<ProductionLine>()
            .Query(l => l.Id == id && l.Department.TenantCode == tenantCode)
            .Include(l => l.Department)
            .Include(l => l.Machines)
            .FirstOrDefaultAsync();

        if (line is null) return NotFound();

        line.Code        = req.Code;
        line.Name        = req.Name;
        line.Description = req.Description;
        line.IsActive    = req.IsActive;

        await _uow.Repository<ProductionLine>().UpdateAsync(line);
        await _uow.SaveChangesAsync();

        return Ok(ToDto(line));
    }

    // ── DELETE /api/production-lines/{id} ────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteLine(Guid id)
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var line = await _uow.Repository<ProductionLine>()
            .Query(l => l.Id == id && l.Department.TenantCode == tenantCode)
            .FirstOrDefaultAsync();

        if (line is null) return NotFound();

        await _uow.Repository<ProductionLine>().DeleteAsync(line);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Linea eliminata — id={Id}", id);
        return NoContent();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // MACCHINE
    // ════════════════════════════════════════════════════════════════════════════

    // ── POST /api/production-lines/{lineId}/machines ──────────────────────────

    [HttpPost("{lineId:guid}/machines")]
    public async Task<ActionResult<MachineDto>> CreateMachine(
        Guid lineId,
        [FromBody] CreateMachineRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var line = await _uow.Repository<ProductionLine>()
            .Query(l => l.Id == lineId && l.Department.TenantCode == tenantCode)
            .FirstOrDefaultAsync();

        if (line is null)
            return NotFound(new { message = "Linea di produzione non trovata." });

        // Unicità codice macchina dentro la linea
        var exists = await _uow.Repository<Machine>()
            .Query(m => m.ProductionLineId == lineId && m.Code == req.Code)
            .AnyAsync();

        if (exists)
            return Conflict(new { message = $"Esiste già una macchina con codice '{req.Code}' su questa linea." });

        var machine = new Machine
        {
            Id               = Guid.NewGuid(),
            Code             = req.Code,
            Name             = req.Name,
            Type             = req.Type,
            Status           = "Idle",
            ProductionLineId = lineId,
        };

        await _uow.Repository<Machine>().AddAsync(machine);
        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "Macchina creata — id={Id} code={Code} linea={LineId}", machine.Id, machine.Code, lineId);

        return Created(string.Empty, ToMachineDto(machine));
    }

    // ── PUT /api/production-lines/{lineId}/machines/{machineId} ──────────────

    [HttpPut("{lineId:guid}/machines/{machineId:guid}")]
    public async Task<ActionResult<MachineDto>> UpdateMachine(
        Guid lineId, Guid machineId,
        [FromBody] UpdateMachineRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var machine = await _uow.Repository<Machine>()
            .Query(m => m.Id == machineId && m.ProductionLineId == lineId
                        && m.ProductionLine.Department.TenantCode == tenantCode)
            .Include(m => m.ProductionLine)
            .FirstOrDefaultAsync();

        if (machine is null) return NotFound();

        machine.Code   = req.Code;
        machine.Name   = req.Name;
        machine.Type   = req.Type;
        machine.Status = req.Status;

        await _uow.Repository<Machine>().UpdateAsync(machine);
        await _uow.SaveChangesAsync();

        return Ok(ToMachineDto(machine));
    }

    // ── DELETE /api/production-lines/{lineId}/machines/{machineId} ───────────

    [HttpDelete("{lineId:guid}/machines/{machineId:guid}")]
    public async Task<ActionResult> DeleteMachine(Guid lineId, Guid machineId)
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var machine = await _uow.Repository<Machine>()
            .Query(m => m.Id == machineId && m.ProductionLineId == lineId
                        && m.ProductionLine.Department.TenantCode == tenantCode)
            .Include(m => m.ProductionLine)
            .FirstOrDefaultAsync();

        if (machine is null) return NotFound();

        await _uow.Repository<Machine>().DeleteAsync(machine);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Macchina eliminata — id={Id}", machineId);
        return NoContent();
    }

    // ── GET /api/production-lines/departments ─────────────────────────────────
    // Lista dipartimenti del tenant (usata nella form di creazione linea).

    [HttpGet("departments")]
    public async Task<ActionResult> GetDepartments()
    {
        var tenantCode = await GetTenantCodeAsync();
        if (tenantCode is null) return Unauthorized();

        var departments = await _uow.Repository<Department>()
            .Query(d => d.TenantCode == tenantCode && d.IsActive)
            .OrderBy(d => d.Code)
            .Select(d => new { d.Id, d.Code, d.Name })
            .ToListAsync();

        return Ok(departments);
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

    private static ProductionLineDto ToDto(ProductionLine l) => new(
        l.Id,
        l.Code,
        l.Name,
        l.Description,
        l.IsActive,
        l.DepartmentId,
        l.Department?.Name ?? string.Empty,
        l.Machines.Select(ToMachineDto).ToList());

    private static MachineDto ToMachineDto(Machine m) => new(
        m.Id,
        m.Code,
        m.Name,
        m.Type,
        m.Status,
        m.ProductionLineId);
}
