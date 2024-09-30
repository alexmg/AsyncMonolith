namespace AsyncMonolith.Consumers;

/// <summary>
///     Base class for consumers.
/// </summary>
/// <typeparam name="T">The type of the consumer payload.</typeparam>
public abstract class BaseConsumer<T> : IConsumer where T : IConsumerPayload
{
    /// <summary>
    /// Consumes the context and message.
    /// </summary>
    /// <param name="context">The context in which the consumer is executed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task Consume(ConsumeContext<T> context, CancellationToken cancellationToken = default);
}