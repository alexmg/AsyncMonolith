namespace AsyncMonolith.Consumers;

/// <summary>
///     Base class for consumers.
/// </summary>
/// <typeparam name="TPayload">The type of the consumer payload.</typeparam>
public abstract class BaseConsumer<TPayload> : IConsumer<TPayload> where TPayload : IConsumerPayload
{
    /// <summary>
    /// Consumes the context and message.
    /// </summary>
    /// <param name="context">The context in which the consumer is executed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task Consume(ConsumeContext<TPayload> context, CancellationToken cancellationToken = default);
}