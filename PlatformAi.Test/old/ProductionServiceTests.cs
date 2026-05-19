using System;
using Microsoft.EntityFrameworkCore;
using Moq;
using PlatformAI.Core.Services;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Tests;

namespace PlatformAI.Test;

[TestFixture]
public class ProductionServiceTests : BaseTest
{
    private ProductionService _service = null!;

    [SetUp]
    public new void Setup()
    {
        base.Setup();
        // Usa la UnitOfWork reale configurata nel BaseTest
        _service = new ProductionService(_uow);
    }

    [Test]
    public async Task RegisterMachineAndEventAsync_Commits_OnSuccess()
    {
        // Arrange
        var productionLine = new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = "Line 1",
            Code = "L001"
        };

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Code = "M001",
            Name = "Machine 1",
            Type = "CNC",
            Status = "Active",
            ProductionLineId = productionLine.Id
        };

        var machineEvent = new MachineEvent
        {
            Id = Guid.NewGuid(),
            EventType = "Start",
            EventTime = DateTime.UtcNow,
            Message = "Machine started"
        };

        // Seed la ProductionLine prima
        SeedData(productionLine);

        // Act
        var result = await _service.RegisterMachineAndEventAsync(machine, machineEvent);

        // Assert
        Assert.That(result, Is.True);

        // Verifica che i dati siano stati salvati
        var savedMachine = await _uow.Repository<Machine>().GetByIdAsync(machine.Id);
        Assert.That(savedMachine, Is.Not.Null);
        Assert.That(savedMachine!.Code, Is.EqualTo("M001"));

        var savedEvent = await _uow.Repository<MachineEvent>().GetByIdAsync(machineEvent.Id);
        Assert.That(savedEvent, Is.Not.Null);
        Assert.That(savedEvent!.MachineId, Is.EqualTo(machine.Id));
    }

    [Test]
    public async Task GetAllMachines_Returns_MachinesWithEvents()
    {
        // Arrange - Seed alcuni dati di test
        var productionLine = new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = "Line 1",
            Code = "L001",
            UserCreate ="Test",
            CreateDate = DateTime.UtcNow,
            UserModify ="Test",
            LastModifiedDate = DateTime.UtcNow
        };

        var machine1 = new Machine
        {
            Id = Guid.NewGuid(),
            Code = "M001",
            Name = "Machine 1",
            Type = "CNC",
            Status = "Active",
            ProductionLineId = productionLine.Id,
              UserCreate ="Test",
            CreateDate = DateTime.UtcNow,
            UserModify ="Test",
            LastModifiedDate = DateTime.UtcNow
        };

        var machine2 = new Machine
        {
            Id = Guid.NewGuid(),
            Code = "M002",
            Name = "Machine 2",
            Type = "Press",
            Status = "Idle",
            ProductionLineId = productionLine.Id,
              UserCreate ="Test",
            CreateDate = DateTime.UtcNow,
            UserModify ="Test",
            LastModifiedDate = DateTime.UtcNow
        };

        var event1 = new MachineEvent
        {
            Id = Guid.NewGuid(),
            MachineId = machine1.Id,
            EventType = "Start",
            EventTime = DateTime.UtcNow,
            Message = "Machine 1 started",
              UserCreate ="Test",
            CreateDate = DateTime.UtcNow,
            UserModify ="Test",
            LastModifiedDate = DateTime.UtcNow
        };

        var event2 = new MachineEvent
        {
            Id = Guid.NewGuid(),
            MachineId = machine1.Id,
            EventType = "Stop",
            EventTime = DateTime.UtcNow.AddHours(1),
            Message = "Machine 1 stopped",
            UserCreate ="Test",
            CreateDate = DateTime.UtcNow,
            UserModify ="Test",
            LastModifiedDate = DateTime.UtcNow
        };

        // Seed dei dati
        SeedData(productionLine);
        SeedData(machine1, machine2);
        SeedData(event1, event2);

        // Act
        var result = await _service.GetAllMachinesWithEvents();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        
        var machineWithEvents = result.FirstOrDefault(m => m.Id == machine1.Id);
        Assert.That(machineWithEvents, Is.Not.Null);
        Assert.That(machineWithEvents!.MachineEvents.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllMachines_Returns_EmptyList_WhenNoData()
    {
        // Act
        var result = await _service.GetAllMachinesWithEvents();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }
}
