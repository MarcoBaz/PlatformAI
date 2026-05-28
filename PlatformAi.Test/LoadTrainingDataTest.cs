using System;
using System.Linq.Expressions;
using Moq;
using Xunit;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.ML;
using PlatformAI.Tests;

namespace PlatformAi.Test;

[Trait("Category", "Integration")]
public class LoadTrainingDataAsync : BaseTest
{
    [Fact]
    public async Task DataLoader_LoadTrainingDataAsync_ReturnsData()
    {
        var loader = new DataLoader(_uow);

        var result = await loader.LoadTrainingDataAsync(tenantCode, DateTime.MinValue);

        Assert.True(result.Count > 0, "Non sono stati caricati dati di training.");
    }
}
