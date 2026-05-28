namespace PlatformAI.Analytics.Models;

 public class ProductionDataFeature
    {
        public float CycleTime { get; set; }
        public float EnergyConsumption { get; set; }
        public float Temperature { get; set; }
        public float ScrapRatio { get; set; }           // scrap / qty
        public float DowntimeEventsLastHour { get; set; }
        // add other engineered features...
    }

    // Output prediction


    // DTO restituito dall'API
    public class ProductionPredictionResult
    {
        public float PredictedValue { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }