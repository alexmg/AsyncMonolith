namespace AsyncMonolith.Consumers;

/// <summary>
///     Base class for consumers.
/// </summary>
/// <typeparam name="TPayload">The type of the consumer payload.</typeparam>
public abstract class BaseConsumer<TPayload> : IConsumer<TPayload> where TPayload : IConsumerPayload
{
    /// <inheritdoc />
    async Task IConsumer<TPayload>.Consume(ConsumeContext<TPayload> context, CancellationToken cancellationToken) =>
        await Consume(context.Message, cancellationToken);

    /// <summary>
    ///     Consumes the payload.
    /// </summary>
    /// <param name="payload">The consumer payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task Consume(TPayload payload, CancellationToken cancellationToken = default);
}