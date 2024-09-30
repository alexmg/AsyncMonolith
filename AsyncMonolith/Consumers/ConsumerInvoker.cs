using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using AsyncMonolith.Utilities;

namespace AsyncMonolith.Consumers;

internal class ConsumerInvoker
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly Func<ConsumerRegistry, ConsumerMessage, object> _contextFunction;
    private readonly Func<object, object, CancellationToken, Task> _consumerFunction;

    internal ConsumerInvoker(ConsumerRegistry consumerRegistry, Type consumerType)
    {
        _consumerRegistry = consumerRegistry;
        var payloadType = consumerType.GetPayloadType();

        _contextFunction = BuildContextFunction(payloadType);
        _consumerFunction = BuildConsumerFunction(payloadType);
    }

    internal async Task InvokeConsumer(object consumer, ConsumerMessage message, CancellationToken cancellationToken)
    {
        var context = _contextFunction(_consumerRegistry, message);
        await _consumerFunction(consumer, context, cancellationToken);
    }

    private static Func<object, object, CancellationToken, Task> BuildConsumerFunction(Type payloadType)
    {
        var contextType = typeof(ConsumeContext<>).MakeGenericType(payloadType);
        var closedConsumerType = typeof(IConsumer<>).MakeGenericType(payloadType);

        var consumeMethod = closedConsumerType.GetMethod(
            nameof(IConsumer<IConsumerPayload>.Consume),
            [contextType, typeof(CancellationToken)]);

        var consumerParameter = Expression.Parameter(typeof(object), "consumer");
        var contextParameter = Expression.Parameter(typeof(object), "context");
        var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var castConsumer = Expression.Convert(consumerParameter, closedConsumerType);
        var castContext = Expression.Convert(contextParameter, contextType);

        var consumeCall = Expression.Call(castConsumer, consumeMethod!, castContext, cancellationTokenParameter);

        return Expression.Lambda<Func<object, object, CancellationToken, Task>>(
                consumeCall, consumerParameter, contextParameter, cancellationTokenParameter)
            .Compile();
    }

    private static Func<ConsumerRegistry, ConsumerMessage, object> BuildContextFunction(Type payloadType)
    {
        var contextMethod = typeof(ConsumerInvoker).GetMethod(
            nameof(BuildConsumeContext),
            BindingFlags.Static | BindingFlags.NonPublic);

        var genericContextMethod = contextMethod!.MakeGenericMethod(payloadType);

        var registryParameter = Expression.Parameter(typeof(ConsumerRegistry), "registry");
        var messageParameter = Expression.Parameter(typeof(ConsumerMessage), "message");

        var methodCall = Expression.Call(
            null, // Static method, so no instance is needed
            genericContextMethod,
            registryParameter,
            messageParameter
        );

        return Expression.Lambda<Func<ConsumerRegistry, ConsumerMessage, object>>(
                methodCall, registryParameter, messageParameter)
            .Compile();
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