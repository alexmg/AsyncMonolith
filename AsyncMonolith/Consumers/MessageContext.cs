namespace AsyncMonolith.Consumers;

/// <summary>
/// Provides contextual information regarding error handling.
/// </summary>
public class MessageContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageContext"/> class.
    /// </summary>
    /// <param name="attempts">Number of attempts made to process the message.</param>
    /// <param name="maxAttempts">Maximum number of attempts allowed to process the message.</param>
    /// <param name="invokedAt">Time when the consumer was invoked.</param>
    /// <param name="timeout">Timeout for the message to be processed.</param>
    public MessageContext(int attempts, int maxAttempts, DateTimeOffset invokedAt, TimeSpan timeout)
    {
        Attempts = attempts;
        MaxAttempts = maxAttempts;
        InvokedAt = invokedAt;
        Timeout = timeout;
    }

    /// <summary>
    /// Gets the number of attempts made to process the message.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Gets the maximum number of attempts allowed to process the message.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the time when the consumer was invoked.
    /// </summary>
    public DateTimeOffset InvokedAt { get; }

    /// <summary>
    /// Gets the timeout for the message to be processed.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the time remaining before the message times out.
    /// </summary>
    public TimeSpan TimeRemaining => InvokedAt.Add(Timeout) - DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether this is the last attempt to process the message.
    /// </summary>
    public bool IsLastAttempt => Attempts + 1 == MaxAttempts;
}