using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class InterfaceConsumer : IConsumer<InterfaceConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public InterfaceConsumer(TestConsumerInvocations consumerInvocations) =>
        _consumerInvocations = consumerInvocations;

    public Task Consume(ConsumeContext<InterfaceConsumerMessage> context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(InterfaceConsumer));
        return Task.CompletedTask;
    }
}