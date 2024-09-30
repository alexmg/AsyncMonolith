using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using Demo.Spam;

namespace Demo.Counter;

public class ValueSubmittedConsumer : BaseConsumer<ValueSubmitted>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProducerService _producerService;

    public ValueSubmittedConsumer(ApplicationDbContext dbContext, IProducerService producerService)
    {
        _dbContext = dbContext;
        _producerService = producerService;
    }

    public override async Task Consume(ConsumeContext<ValueSubmitted> context, CancellationToken cancellationToken = default)
    {
        var newValue = new SubmittedValue
        {
            Value = context.Message.Value
        };

        _dbContext.SubmittedValues.Add(newValue);
        await _producerService.Produce(new ValuePersisted(), cancellationToken: cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}