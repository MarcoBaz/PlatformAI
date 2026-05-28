using PlatformAI.Analytics.Models;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlatformAI.Analytics.Services
{
    public interface IFeatureBuilder
    {
        ProductionDataFeature BuildFeature(IEnumerable<ProductionData> recentData, IEnumerable<MachineEvent> recentEvents);
    }

    public class FeatureBuilder : IFeatureBuilder
    {
        public ProductionDataFeature BuildFeature(IEnumerable<ProductionData> recentData, IEnumerable<MachineEvent> recentEvents)
        {
            // Raggruppa le letture per snapshot (macchina + timestamp)
            var snapshots = (recentData ?? Enumerable.Empty<ProductionData>()).ToSnapshots();

            float cycleTimeAvg = snapshots.Any() ? (float)snapshots.Average(s => (double)s.GetMetric("cycle_time")) : 0f;
            float energyAvg    = snapshots.Any() ? (float)snapshots.Average(s => (double)s.GetMetric("energy_consumption")) : 0f;
            float tempAvg      = snapshots.Any() ? (float)snapshots.Average(s => (double)s.GetMetric("temperature")) : 0f;
            float qtySum       = snapshots.Any() ? (float)snapshots.Sum(s => s.GetMetric("quantity_produced")) : 0f;
            float scrapSum     = snapshots.Any() ? (float)snapshots.Sum(s => s.GetMetric("scrap_quantity")) : 0f;
            float scrapRatio   = qtySum > 0 ? scrapSum / qtySum : 0f;

            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            float downtimeEvents = recentEvents?.Count(e => e.EventTime >= oneHourAgo && e.EventType == "STOP") ?? 0;

            return new ProductionDataFeature
            {
                CycleTime              = cycleTimeAvg,
                EnergyConsumption      = energyAvg,
                Temperature            = tempAvg,
                ScrapRatio             = scrapRatio,
                DowntimeEventsLastHour = downtimeEvents
            };
        }
    }
}
