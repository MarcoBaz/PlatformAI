using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace PlatformAI.ML.Services;

public class BlobStoragePersistence
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<TrainingService> _logger;
    private const string containerName = "models";
    private readonly MLContext _mlContext;
    public BlobStoragePersistence(MLContext mlContext, string blobConnectionString, ILogger<TrainingService>? logger = null)
    {
        _mlContext = mlContext;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TrainingService>.Instance;
        _containerClient = new BlobContainerClient(blobConnectionString, containerName);
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task SaveModelAsync(ITransformer model, string tenantCode, string version)
    {
        using var ms = new MemoryStream();
        var emptyData = _mlContext.Data.LoadFromEnumerable(new List<ProductionDataMLEnriched>());
        _mlContext.Model.Save(model, emptyData.Schema, ms);
        ms.Position = 0;

        await UploadModelAsync(tenantCode, version, ms);
        _logger.LogInformation("Modello salvato su blob per tenant {tenant}", tenantCode);
    }

    // =========================
    // SALVATAGGIO CHECKPOINT
    // =========================
    public async Task SaveCheckpointAsync(string tenantCode, DateTime lastProcessedDate, TrainingResultInternal trainingResult)
    {
        var checkpoint = new TrainingCheckpoint
        {
            TenantCode = tenantCode,
            LastProcessedDate = lastProcessedDate,
            LastTrainingDate = DateTime.UtcNow,
            ModelVersion = trainingResult.ModelVersion,
            RSquared = trainingResult.RSquared,
            RMSE = trainingResult.RMSE,
            RecordsProcessed = trainingResult.DataCount
        };

        await SaveCheckpointAsync(tenantCode, checkpoint);
        _logger.LogInformation("Checkpoint salvato su blob per tenant {tenant}", tenantCode);
    }

    // =========================
    // CARICAMENTO MODELLO
    // =========================
    public ITransformer? LoadLatestModel(string tenantCode)
    {
        try
        {
            var latestBlob = _containerClient.GetBlobClient($"{tenantCode}/model_latest.zip");
            if (!latestBlob.Exists())
                return null;

            using var ms = new MemoryStream();
            latestBlob.DownloadTo(ms);
            ms.Position = 0;
            return _mlContext.Model.Load(ms, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossibile caricare il modello latest per tenant {tenant}", tenantCode);
            return null;
        }
    }

    // Predict rimane identico
    public ProductionPrediction? Predict(ProductionDataMLEnriched features, string tenantCode)
    {
        var model = LoadLatestModel(tenantCode);
        if (model == null) return null;

        var engine = _mlContext.Model.CreatePredictionEngine<ProductionDataMLEnriched, ProductionPrediction>(model);
        return engine.Predict(features);
    }

    private async Task UploadModelAsync(string tenantCode, string version, Stream modelStream)
    {
        var tenantFolder = $"{tenantCode}";

        // Modello versione specifica
        var versionBlob = _containerClient.GetBlobClient($"{tenantFolder}/model_{version}.zip");
        modelStream.Position = 0;
        await versionBlob.UploadAsync(modelStream, overwrite: true);

        // Modello latest
        var latestBlob = _containerClient.GetBlobClient($"{tenantFolder}/model_latest.zip");
        modelStream.Position = 0;
        await latestBlob.UploadAsync(modelStream, overwrite: true);

        _logger.LogInformation("Modelli salvati su blob per tenant {tenant}: {versionBlob}", tenantCode, versionBlob.Uri);
    }
    private async Task SaveCheckpointAsync(string tenantCode, TrainingCheckpoint checkpoint)
    {
        var tenantFolder = $"{tenantCode}";

        // Checkpoint corrente
        var checkpointBlob = _containerClient.GetBlobClient($"{tenantFolder}/checkpoint.json");
        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
        using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))) 
        {
            await checkpointBlob.UploadAsync(ms, overwrite: true);
        }

        // Storia checkpoint --@Marco:vedere se è davvero utile
        // var historyBlob = _containerClient.GetBlobClient($"{tenantFolder}/history.jsonl");
        // await historyBlob.UploadAsync(json + Environment.NewLine);

        _logger.LogInformation("Checkpoint salvato su blob per tenant {tenant}", tenantCode);
    }

    public async Task<TrainingCheckpoint?> LoadCheckpointAsync(string tenantCode)
    {
        try
        {
            var checkpointBlob = _containerClient.GetBlobClient($"{tenantCode}/checkpoint.json");
            if (!await checkpointBlob.ExistsAsync())
                return null;

            var download = await checkpointBlob.DownloadContentAsync();
            var json = download.Value.Content.ToString();
            return JsonSerializer.Deserialize<TrainingCheckpoint>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossibile leggere checkpoint per tenant {tenant}", tenantCode);
            return null;
        }
    }

}