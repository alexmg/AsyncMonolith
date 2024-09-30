using AsyncMonolith.Consumers;

namespace Demo.Spam;

public class SpamMessageConsumer : BaseConsumer<SpamMessage>
{
    private readonly TimeProvider _timeProvider;

    public SpamMessageConsumer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override Task Consume(ConsumeContext<SpamMessage> context, CancellationToken cancellationToken)
    {
        if (context.Message.Last)
        {
            SpamResultService.End = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        }

        SpamResultService.Count++;
        return Task.CompletedTask;
    }
}