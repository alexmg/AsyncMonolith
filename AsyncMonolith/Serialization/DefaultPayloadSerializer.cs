using System.Text.Json;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Serialization;

/// <summary>
///   Default implementation of the <see cref="IPayloadSerializer"/> interface.
/// </summary>
public class DefaultPayloadSerializer : IPayloadSerializer
{
    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(string payload)
        where TPayload : IConsumerPayload =>
        JsonSerializer.Deserialize<TPayload>(payload)
        ?? throw new Exception($"Failed to deserialize payload to type '{typeof(TPayload).Name}'");

    /// <inheritdoc />
    public string Serialize<TPayload>(TPayload payload)
        where TPayload : IConsumerPayload =>
        JsonSerializer.Serialize(payload);
}