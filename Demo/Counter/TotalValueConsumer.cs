using AsyncMonolith.Consumers;
using Microsoft.EntityFrameworkCore;

namespace Demo.Counter;

public class TotalValueConsumer : BaseConsumer<ValuePersisted>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TotalValueService _totalValueService;

    public TotalValueConsumer(TotalValueService totalValueService, ApplicationDbContext dbContext)
    {
        _totalValueService = totalValueService;
        _dbContext = dbContext;
    }

    public override async Task Consume(ConsumeContext<ValuePersisted> context, CancellationToken cancellationToken)
    {
        var totalValue = await _dbContext.SubmittedValues.SumAsync(v => v.Value, cancellationToken);
        _totalValueService.Set(totalValue);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}