using System;
using System.Linq.Expressions;
using Moq;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.ML;
using PlatformAI.Tests;

namespace PlatformAi.Test;

[TestFixture]
public class LoadTrainingDataAsync:BaseTest
{
   
[Test]
public async Task DataLoader_LoadTrainingDataAsync_ReturnsData()
{
    var loader = new DataLoader(_uow);

    var result = await loader.LoadTrainingDataAsync(tenantCode,DateTime.MinValue);

    Assert.That(result.Count, Is.GreaterThan(0), "Non sono stati caricati dati di training.");
}
}
