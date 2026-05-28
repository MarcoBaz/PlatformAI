using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.ML;

public class DataLoader
{
    private readonly IUnitOfWork _uow;

    public DataLoader(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<ProductionData>> LoadTrainingDataAsync(string tenantCode,DateTime TraininDate)
    {
        // return await _context.ProductionData
        //     .AsNoTracking()
        //     .Where(p => p.QuantityProduced > 0) // filtri base
        //     .ToListAsync();
        var productRepo = _uow.Repository<ProductionData>();
        var productions = productRepo.Query(x=> x.Machine.ProductionLine.Department.TenantCode == tenantCode && x.LastModifiedDate >= TraininDate)
                            .Include(p=> p.Machine)
                                .ThenInclude(m=> m.ProductionLine)
                                     .ThenInclude(p=> p.Department)
                            .Include(p => p.MetricType)
                            .ToList();
        return productions?.Any() == true ? productions : new List<ProductionData>();
    }
}
