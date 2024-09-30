using System.Reflection;
using System.Text.Json;

namespace AsyncMonolith.Consumers;

internal class ConsumerInvoker
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly MethodInfo _contextMethod;
    private readonly MethodInfo _consumerMethod;

    internal ConsumerInvoker(ConsumerRegistry consumerRegistry, Type consumerType)
    {
        _consumerRegistry = consumerRegistry;
        var payloadType = consumerType.BaseType!.GetGenericArguments()[0];

        var contextMethod = typeof(ConsumerInvoker).GetMethod(
            nameof(BuildConsumeContext),
            BindingFlags.Static | BindingFlags.NonPublic);
        _contextMethod = contextMethod!.MakeGenericMethod(payloadType);

        var contextType = typeof(ConsumeContext<>).MakeGenericType(payloadType);
        var consumerMethod = typeof(BaseConsumer<>).MakeGenericType(payloadType);
        _consumerMethod = consumerMethod.GetMethod(
            nameof(BaseConsumer<IConsumerPayload>.Consume),
            [contextType, typeof(CancellationToken)])!;
    }

    internal async Task InvokeConsumer(IConsumer consumer, ConsumerMessage message, CancellationToken cancellationToken)
    {
        var context = _contextMethod.Invoke(null, [_consumerRegistry, message]);
        await (Task)_consumerMethod.Invoke(consumer, [context, cancellationToken])!;
    }

    private static ConsumeContext<T> BuildConsumeContext<T>(
        ConsumerRegistry consumerRegistry,
        ConsumerMessage message)
        where T : IConsumerPayload
    {
        var payload = JsonSerializer.Deserialize<T>(message.Payload) ?? throw new Exception(
            $"Consumer: '{message.ConsumerType}' failed to deserialize payload: '{message.PayloadType}'");

        var messageContext = new MessageContext(
            message.Attempts,
            consumerRegistry.ResolveConsumerMaxAttempts(message),
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(consumerRegistry.ResolveConsumerTimeout(message)));
        return new ConsumeContext<T>(messageContext, payload);
    }
}