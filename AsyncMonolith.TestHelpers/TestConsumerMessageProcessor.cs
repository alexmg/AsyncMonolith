using System.Text.Json;
using AsyncMonolith.Consumers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Helper class for testing consumer message processing.
/// </summary>
public static class TestConsumerMessageProcessor
{
    /// <summary>
    /// Processes the next consumer message of type T.
    /// </summary>
    /// <typeparam name="T">The type of DbContext.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed consumer message, or null if no message is available.</returns>
    public static async Task<ConsumerMessage?> ProcessNext<T>(IServiceScope scope,
        CancellationToken cancellationToken = default) where T : DbContext
    {
        var consumerRegistry = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var consumerMessageSet = dbContext.Set<ConsumerMessage>();
        var message = await consumerMessageSet
            .OrderBy(m => m.AvailableAfter)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return null;
        }

        // Resolve the consumer
        var consumerType = consumerRegistry.ResolveConsumerType(message);
        var consumer = scope.ServiceProvider.GetService(consumerType);
        if (consumer is null)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            return null;
        }

        // Execute the consumer
        var consumerInvoker = consumerRegistry.ResolveConsumerInvoker(consumerType);
        await consumerInvoker.InvokeConsumer(consumer, message, cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the next consumer message of type T and V.
    /// </summary>
    /// <typeparam name="TDbContext">The type of DbContext.</typeparam>
    /// <typeparam name="TConsumer">The type of IConsumer.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed consumer message, or null if no message is available.</returns>
    public static async Task<ConsumerMessage?> ProcessNext<TDbContext, TConsumer>(
        IServiceScope scope,
        CancellationToken cancellationToken = default)
        where TDbContext : DbContext
        where TConsumer : IConsumer<IConsumerPayload>
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var consumerMessageSet = dbContext.Set<ConsumerMessage>();
        var consumerType = typeof(TDbContext);
        var message = await consumerMessageSet
            .Where(m => m.ConsumerType == consumerType.Name)
            .OrderBy(m => m.AvailableAfter)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return null;
        }

        // Resolve the consumer
        var consumer = scope.ServiceProvider.GetService(consumerType);
        if (consumer is null)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            return null;
        }

        // Execute the consumer
        var consumerRegistry = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>();
        var consumerInvoker = consumerRegistry.ResolveConsumerInvoker(consumerType);
        await consumerInvoker.InvokeConsumer(consumer, message, cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the consumer message of type TPayload with the consumer of type TConsumer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="payload">The payload for the consumer message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Process<TConsumer, TPayload>(
        IServiceScope scope,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TConsumer : BaseConsumer<TPayload>
        where TPayload : IConsumerPayload
    {
        // Resolve the consumer
        var consumerType = typeof(TConsumer);
        var consumer = scope.ServiceProvider.GetService(consumerType);
        if (consumer is null)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{consumerType.Name}'");
            return;
        }

        // Execute the consumer
        var consumerRegistry = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>();
        var consumerInvoker = consumerRegistry.ResolveConsumerInvoker(consumerType);
        await consumerInvoker.InvokeConsumer(
            consumer,
            new ConsumerMessage
            {
                Attempts = 0,
                AvailableAfter = 0,
                ConsumerType = consumerType.Name,
                CreatedAt = 0,
                Id = string.Empty,
                InsertId = string.Empty,
                Payload = JsonSerializer.Serialize(payload),
                PayloadType = typeof(TPayload).Name,
                TraceId = null,
                SpanId = null
            },
            cancellationToken);
    }
}
