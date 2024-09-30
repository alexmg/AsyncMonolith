namespace AsyncMonolith.Consumers;

/// <summary>
/// Interface for Consumers
/// </summary>
public interface IConsumer<TPayload> where TPayload : IConsumerPayload
{
    /// <summary>
    /// Consumes the context and message.
    /// </summary>
    /// <param name="context">The context in which the consumer is executed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Consume(ConsumeContext<TPayload> context, CancellationToken cancellationToken);
}
