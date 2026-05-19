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

            float cycleTimeAvg = data.Any() ? (float)data.Average(d => (double)d.CycleTime) : 0f;
            float energyAvg = data.Any() ? (float)data.Average(d => (double)d.EnergyConsumption) : 0f;
            float tempAvg = data.Any() ? (float)data.Average(d => (double)d.Temperature) : 0f;
            float qtySum = data.Any() ? data.Sum(d => d.QuantityProduced) : 0;
            float scrapSum = data.Any() ? data.Sum(d => d.ScrapQuantity) : 0;
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
