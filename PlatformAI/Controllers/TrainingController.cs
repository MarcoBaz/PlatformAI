using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Master;
using PlatformAI.ML;
using PlatformAI.ML.Services;
using PlatformAI.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace PlatformAI.Controllers;

/// <summary>
/// Espone il TrainingService via REST.
/// Permette di lanciare il training ML parametrizzato e leggere lo stato del checkpoint.
/// </summary>
[Route("api/training")]
[ApiController]
[Authorize]
public class TrainingController : ControllerBase
{
    private readonly TrainingService _trainingService;
    private readonly SeedDataService _seedService;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TrainingController> _logger;

    public TrainingController( TrainingService trainingService, SeedDataService seedService, IUnitOfWork uow, ILogger<TrainingController> logger)
    {
        _trainingService = trainingService;
        _seedService     = seedService;
        _uow             = uow;
        _logger          = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/training/run
    // Lancia il training incrementale (o full se forceFullTraining=true)
    // con la configurazione ricevuta nel body.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("run")]
    public async Task<ActionResult<IncrementalTrainingResult>> Run( [FromBody] TrainingRunRequest request, CancellationToken cancellationToken)
    {
        var tenantCode = await GetTenantCodeAsync(request.UserId);
        if (tenantCode == null)
            return Unauthorized("Utente o tenant non trovato.");

        _logger.LogInformation(
            "Training richiesto — tenant={Tenant} forceFullTraining={Force} trainer={Trainer}",
            tenantCode, request.ForceFullTraining, request.Config?.Trainer);

        // Sovrascrive la configurazione default con quella ricevuta dalla UI (null = usa i default del servizio)
        var config = request.Config;

        var result = request.ForceFullTraining
            ? await _trainingService.ForceFullTrainingAsync(tenantCode, config, cancellationToken)
            : await _trainingService.TrainIncrementalAsync(tenantCode, config, cancellationToken);

        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/training/checkpoint?userId={userId}
    // Restituisce lo stato corrente del checkpoint (ultima data processata,
    // metriche dell'ultimo training, versione del modello).
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("checkpoint")]
    public async Task<ActionResult<TrainingCheckpoint?>> GetCheckpoint([FromQuery] string userId)
    {
        var tenantCode = await GetTenantCodeAsync(userId);
        if (tenantCode == null)
            return Unauthorized("Utente o tenant non trovato.");

        var checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
        if (checkpoint == null)
            return NoContent(); // 204 — nessun checkpoint ancora creato
        return Ok(checkpoint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/training/reset
    // Azzera il checkpoint — il prossimo training ripartirà da tutti i dati.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("reset")]
    public async Task<ActionResult> Reset([FromBody] UserIdRequest request)
    {
        var tenantCode = await GetTenantCodeAsync(request.UserId);
        if (tenantCode == null)
            return Unauthorized("Utente o tenant non trovato.");

        await _trainingService.ResetCheckpointAsync(tenantCode);
        return Ok(new { message = "Checkpoint azzerato. Il prossimo training caricherà tutti i dati storici." });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/training/seed
    // Inserisce un turno simulato nel DB (ProductionOrder + MachineEvents + ProductionData).
    // Utile per fare simulazioni e testare il training ML su dati freschi.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("seed")]
    public async Task<ActionResult<SeedDataResult>> SeedData( [FromBody] SeedDataRequest request, CancellationToken cancellationToken)
    {
        // Verifica autenticazione utente (non serve tenantCode per il seed)
        var tenantCode = await GetTenantCodeAsync(request.UserId);
        if (tenantCode == null)
            return Unauthorized("Utente o tenant non trovato.");

        _logger.LogInformation( "Seed ProductionData richiesto — tenant={Tenant} linea={Line} daysAgo={Days}", tenantCode, request.LineName, request.DaysAgo);

        try
        {
            var result = await _seedService.SeedProductionDataAsync(lineName: request.LineName, daysAgo:  request.DaysAgo, ct: cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<string?> GetTenantCodeAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var guid)) return null;
        var user = await _uow.Repository<User>()
            .Query(x => x.Id == guid)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync();
        return user?.Tenant?.Code;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public class TrainingRunRequest
{
    public string UserId { get; set; } = string.Empty;
    public bool ForceFullTraining { get; set; } = false;
    public IncrementalTrainingConfig? Config { get; set; }
}

public class UserIdRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class SeedDataRequest
{
    public string UserId  { get; set; } = string.Empty;
    public string LineName { get; set; } = "Linea A";
    public int    DaysAgo  { get; set; } = 1;
}
