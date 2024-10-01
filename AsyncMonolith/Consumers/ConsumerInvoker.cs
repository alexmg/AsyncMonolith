using System.Linq.Expressions;
using System.Reflection;
using AsyncMonolith.Serialization;
using AsyncMonolith.Utilities;

namespace AsyncMonolith.Consumers;

internal class ConsumerInvoker
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly IPayloadSerializer _serializer;
    private readonly Func<ConsumerInvoker, ConsumerRegistry, ConsumerMessage, object> _contextFunction;
    private readonly Func<object, object, CancellationToken, Task> _consumerFunction;

    internal ConsumerInvoker(ConsumerRegistry consumerRegistry, Type consumerType, IPayloadSerializer serializer)
    {
        _consumerRegistry = consumerRegistry;
        _serializer = serializer;
        var payloadType = consumerType.GetPayloadType();

        _contextFunction = BuildContextFunction(payloadType);
        _consumerFunction = BuildConsumerFunction(payloadType);
    }

    internal async Task InvokeConsumer(object consumer, ConsumerMessage message, CancellationToken cancellationToken)
    {
        var context = _contextFunction(this, _consumerRegistry, message);
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

    private static Func<ConsumerInvoker, ConsumerRegistry, ConsumerMessage, object> BuildContextFunction(Type payloadType)
    {
        var contextMethod = typeof(ConsumerInvoker).GetMethod(
            nameof(BuildConsumeContext),
            BindingFlags.Instance | BindingFlags.NonPublic);

        var genericContextMethod = contextMethod!.MakeGenericMethod(payloadType);

        var invokerParameter = Expression.Parameter(typeof(ConsumerInvoker), "invoker");
        var registryParameter = Expression.Parameter(typeof(ConsumerRegistry), "registry");
        var messageParameter = Expression.Parameter(typeof(ConsumerMessage), "message");

        var methodCall = Expression.Call(
            invokerParameter,
            genericContextMethod,
            registryParameter,
            messageParameter
        );

        return Expression.Lambda<Func<ConsumerInvoker, ConsumerRegistry, ConsumerMessage, object>>(
                methodCall, invokerParameter, registryParameter, messageParameter)
            .Compile();
    }

    private ConsumeContext<T> BuildConsumeContext<T>(
        ConsumerRegistry consumerRegistry,
        ConsumerMessage message)
        where T : IConsumerPayload
    {
        var payload = _serializer.Deserialize<T>(message.Payload);
        var messageContext = new MessageContext(
            message.Attempts,
            consumerRegistry.ResolveConsumerMaxAttempts(message),
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(consumerRegistry.ResolveConsumerTimeout(message)));
        return new ConsumeContext<T>(messageContext, payload);
    }
}