using AsyncMonolith.Consumers;

namespace AsyncMonolith.Serialization;

/// <summary>
///    Serializer for message payloads.
/// </summary>
public interface IPayloadSerializer
{
    /// <summary>
    ///    Deserializes the payload.
    /// </summary>
    /// <param name="payload">The payload as <see cref="string"/>.</param>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <returns>The payload instance.</returns>
    TPayload Deserialize<TPayload>(string payload) where TPayload : IConsumerPayload;

    /// <summary>
    ///   Serializes the payload.
    /// </summary>
    /// <param name="payload">The payload instance.</param>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <returns>The payload as a <see cref="string"/>.</returns>
    string Serialize<TPayload>(TPayload payload) where TPayload : IConsumerPayload;
}