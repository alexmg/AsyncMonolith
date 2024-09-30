namespace AsyncMonolith.Consumers;

/// <summary>
/// Represents the context in which a consumer is executed.
/// </summary>
/// <typeparam name="TPayload">The type of the message payload.</typeparam>
public class ConsumeContext<TPayload> where TPayload : IConsumerPayload
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumeContext{TPayload}"/> class.
    /// </summary>
    /// <param name="messageContext">Receive context.</param>
    /// <param name="payload">Message payload.</param>
    public ConsumeContext(MessageContext messageContext, TPayload payload)
    {
        Message = payload;
        MessageContext = messageContext;
    }

    /// <summary>
    /// Gets the payload of the message.
    /// </summary>
    public TPayload Message { get; }

    /// <summary>
    /// Gets the message context.
    /// </summary>
    public MessageContext MessageContext { get; }
}
