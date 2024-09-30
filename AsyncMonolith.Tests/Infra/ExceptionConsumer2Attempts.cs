using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerAttempts(2)]
public class ExceptionConsumer2Attempts : BaseConsumer<ExceptionConsumer2AttemptsMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public ExceptionConsumer2Attempts(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(ConsumeContext<ExceptionConsumer2AttemptsMessage> context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(ExceptionConsumer2AttemptsMessage));
        throw new Exception();
    }
}