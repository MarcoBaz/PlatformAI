using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Core.Services;

/// <summary>
/// Simula un turno di produzione inserendo dati realistici nel DB.
/// Replica la logica di SeedProductionData.sql — usato per simulazioni e test del training ML.
/// </summary>
public class SeedDataService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SeedDataService> _logger;
    private static readonly Random _rng = new();

    public SeedDataService(IUnitOfWork uow, ILogger<SeedDataService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Inserisce un turno di 8 ore (ieri 08:00→16:00 UTC) con:
    ///  - 1 ProductionOrder
    ///  - 2 MachineEvent (START/STOP) per ogni macchina della linea
    ///  - 15 ProductionData ogni 30 minuti per ogni macchina
    /// </summary>
    public async Task<SeedDataResult> SeedProductionDataAsync( string lineName = "Linea A", int daysAgo = 1, CancellationToken ct = default)
    {
        var now       = DateTime.UtcNow;
        var startTime = now.Date.AddDays(-daysAgo).AddHours(8);   // ieri 08:00
        var stopTime  = startTime.AddHours(8);                     // ieri 16:00

        // ── 1. Trova la linea e le macchine ─────────────────────────────────
        var line = await _uow.Repository<ProductionLine>()
            .Query(x => x.Name == lineName)
            .Include(x => x.Machines)
            .FirstOrDefaultAsync(ct);

        if (line is null)
            throw new InvalidOperationException(    
                $"Linea '{lineName}' non trovata. Esegui prima TestData.sql.");

       //var machines = line.Machines.OrderBy(m => m.Code).ToList();
       var machines = await  _uow.Repository<Machine>()
            .Query(x => x.ProductionLineId == line.Id)
            .OrderBy(m => m.Code)
            .ToListAsync(ct);

        if (machines.Count == 0)
            throw new InvalidOperationException(
                $"Nessuna macchina trovata su '{lineName}'.");

        _logger.LogInformation(
            "Seed avviato — linea={Line}, macchine={Count}, periodo={Start}→{Stop}",
            lineName, machines.Count, startTime, stopTime);

        // ── 2. Calcola il prossimo numero ordine ─────────────────────────────
        var allOrders = await _uow.Repository<ProductionOrder>().ListAsync();
        var maxNum = allOrders
            .Select(o =>
            {
                var parts = o.OrderNumber.Split('-');
                return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        var orderCode = $"ORD-{maxNum + 1:D5}";

        // ── 3. Inserimento transazionale ─────────────────────────────────────
        await _uow.BeginTransactionAsync();
        try
        {
            // ProductionOrder
            var order = new ProductionOrder
            {
                Id                = Guid.NewGuid(),
                OrderNumber       = orderCode,
                ProductCode       = "PRD-001",
                PlannedQuantity   = 500,
                StartTime         = startTime,
                EndTime           = stopTime,
                ProductionLineId  = line.Id,
            };
            await _uow.Repository<ProductionOrder>().AddAsync(order);

            int pdCount = 0;
            int evCount = 0;

            foreach (var machine in machines)
            {
                // MachineEvent START
                await _uow.Repository<MachineEvent>().AddAsync(new MachineEvent
                {
                    Id        = Guid.NewGuid(),
                    MachineId = machine.Id,
                    EventType = "START",
                    EventTime = startTime,
                    Message   = $"{machine.Code} - Avvio turno",
                });

                // MachineEvent STOP
                await _uow.Repository<MachineEvent>().AddAsync(new MachineEvent
                {
                    Id        = Guid.NewGuid(),
                    MachineId = machine.Id,
                    EventType = "STOP",
                    EventTime = stopTime,
                    Message   = $"{machine.Code} - Fine turno",
                });
                evCount += 2;

                // 15 ProductionData — ogni 30 minuti
                for (int slot = 1; slot <= 15; slot++)
                {
                    var ts = startTime.AddMinutes(slot * 30);
                    await _uow.Repository<ProductionData>().AddAsync(new ProductionData
                    {
                        Id                = Guid.NewGuid(),
                        MachineId         = machine.Id,
                        ProductionOrderId = order.Id,
                        Timestamp         = ts,
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["quantity_produced"]  = _rng.Next(80, 121),
                            ["scrap_quantity"]     = _rng.Next(0, 6),
                            ["cycle_time"]         = Math.Round((decimal)(1.5 + _rng.NextDouble() * 2.0), 2),
                            ["energy_consumption"] = Math.Round((decimal)(80.0 + _rng.NextDouble() * 40.0), 1),
                            ["temperature"]        = Math.Round((decimal)(35.0 + _rng.NextDouble() * 20.0), 1),
                        }
                    });
                    pdCount++;
                }
            }

            await _uow.CommitTransactionAsync();

            _logger.LogInformation(
                "Seed completato — ordine={Order}, ProductionData={PD}, MachineEvents={EV}",
                orderCode, pdCount, evCount);

            return new SeedDataResult
            {
                Success              = true,
                OrderNumber          = orderCode,
                LineName             = lineName,
                MachinesCount        = machines.Count,
                ProductionDataCount  = pdCount,
                MachineEventsCount   = evCount,
                Period               = $"{startTime:yyyy-MM-dd HH:mm} → {stopTime:HH:mm} UTC",
                Message              = $"Seed completato: ordine {orderCode}, {pdCount} record ProductionData su {machines.Count} macchine.",
            };
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync();
            _logger.LogError(ex, "Errore durante il seed di ProductionData");
            throw;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DTO risultato
// ─────────────────────────────────────────────────────────────────────────────

public class SeedDataResult
{
    public bool   Success             { get; set; }
    public string OrderNumber         { get; set; } = string.Empty;
    public string LineName            { get; set; } = string.Empty;
    public int    MachinesCount       { get; set; }
    public int    ProductionDataCount { get; set; }
    public int    MachineEventsCount  { get; set; }
    public string Period              { get; set; } = string.Empty;
    public string Message             { get; set; } = string.Empty;
}
