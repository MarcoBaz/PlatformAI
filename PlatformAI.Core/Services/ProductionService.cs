using System;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Core.Services;

public class ProductionService
{
     private readonly IUnitOfWork _uow;

    public ProductionService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> RegisterMachineAndEventAsync(Machine machine, MachineEvent ev)
    {
         await _uow.BeginTransactionAsync();

        try
        {
            var machineRepo = _uow.Repository<Machine>();
            var eventRepo = _uow.Repository<MachineEvent>();

            await machineRepo.AddAsync(machine);
            ev.MachineId = machine.Id;
            await eventRepo.AddAsync(ev);

            await _uow.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<Machine>> GetAllMachinesWithEvents()
    {
          var machineRepo = _uow.Repository<Machine>();
          var machines = machineRepo.Query(x=> x.Id != Guid.Empty).Include(x=> x.MachineEvents).ToList();
          if (machines != null && machines.Any())
            return machines;
        return new List<Machine>();
    }
}
