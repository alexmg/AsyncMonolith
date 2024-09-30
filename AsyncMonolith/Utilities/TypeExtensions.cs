using AsyncMonolith.Consumers;

namespace AsyncMonolith.Utilities;

internal static class TypeExtensions
{
    private static readonly Type OpenGenericConsumerType = typeof(IConsumer<>);

    internal static bool IsConsumerType(this Type type) =>
        !type.IsAbstract && type.GetInterfaces().Any(InterfaceIsConsumer);

    internal static Type GetPayloadType(this Type consumerType) =>
        consumerType.GetInterfaces()
            .First(InterfaceIsConsumer)
            .GetGenericArguments()[0];

    internal static bool IsPayloadType(this Type type) =>
        type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IConsumerPayload));

    private static bool InterfaceIsConsumer(Type interfaceType) =>
        interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == OpenGenericConsumerType;
}