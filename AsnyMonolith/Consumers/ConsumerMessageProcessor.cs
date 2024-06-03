﻿using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsnyMonolith.Consumers;

public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private const int MaxChainLength = 10;
    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _options = options;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumedMessageChainLength = 0;
        var deltaDelay = (_options.Value.ProcessorMaxDelay - _options.Value.ProcessorMinDelay) / MaxChainLength;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ConsumeNext(stoppingToken))
                    consumedMessageChainLength++;
                else
                    consumedMessageChainLength = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message");
            }

            var delay = _options.Value.ProcessorMaxDelay - deltaDelay * Math.Clamp(consumedMessageChainLength, 0, MaxChainLength);
            if (delay >= 10)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    public async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var consumerSet = dbContext.Set<ConsumerMessage>();

        var message = await consumerSet
            .Where(m => m.AvailableAfter <= currentTime && m.Attempts <= _options.Value.MaxAttempts)
            .OrderBy(m => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
            // No messages waiting.
            return false;

        try
        {
            message.AvailableAfter = currentTime + _options.Value.AttemptDelay;
            message.Attempts++;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // ignore
            return false;
        }

        try
        {
            if (scope.ServiceProvider.GetRequiredService(_consumerRegistry.ResolveConsumerType(message))
                is not IConsumer consumer)
                throw new Exception($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");

            await consumer.Consume(message, cancellationToken);

            consumerSet.Remove(message);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully processed message for consumer: '{id}'", message.ConsumerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                message.Attempts > _options.Value.MaxAttempts
                    ? "Failed to consume message on attempt {attempt}, will NOT retry"
                    : "Failed to consume message on attempt {attempt}, will retry", message.Attempts);
        }

        return true;
    }
}