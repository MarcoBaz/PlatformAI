using PlatformAI.Analytics.Models;
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
            var data = recentData?.ToArray() ?? Array.Empty<ProductionData>();

            float cycleTimeAvg = data.Any() ? (float)data.Average(d => (double)d.GetMetric("cycle_time")) : 0f;
            float energyAvg    = data.Any() ? (float)data.Average(d => (double)d.GetMetric("energy_consumption")) : 0f;
            float tempAvg      = data.Any() ? (float)data.Average(d => (double)d.GetMetric("temperature")) : 0f;
            float qtySum       = data.Any() ? (float)data.Sum(d => d.GetMetric("quantity_produced")) : 0;
            float scrapSum     = data.Any() ? (float)data.Sum(d => d.GetMetric("scrap_quantity")) : 0;
            float scrapRatio = qtySum > 0 ? scrapSum / qtySum : 0f;

            // downtime events in last hour
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            float downtimeEvents = recentEvents?.Count(e => e.EventTime >= oneHourAgo && e.EventType == "STOP") ?? 0;

            return new ProductionDataFeature
            {
                CycleTime = cycleTimeAvg,
                EnergyConsumption = energyAvg,
                Temperature = tempAvg,
                ScrapRatio = scrapRatio,
                DowntimeEventsLastHour = downtimeEvents
            };
        }
    }
}
