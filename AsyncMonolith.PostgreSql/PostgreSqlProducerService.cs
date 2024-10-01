﻿using System.Diagnostics;
using System.Text;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Serialization;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

/// <summary>
/// Represents a service for producing messages to a PostgreSQL database.
/// </summary>
/// <typeparam name="T">The type of the DbContext.</typeparam>
public sealed class PostgreSqlProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly IPayloadSerializer _payloadSerializer;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlProducerService{T}"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="consumerRegistry">The consumer registry.</param>
    /// <param name="dbContext">The DbContext.</param>
    /// <param name="idGenerator">The ID generator.</param>
    /// <param name="payloadSerializer">The payload serializer.</param>
    public PostgreSqlProducerService(
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry,
        T dbContext,
        IAsyncMonolithIdGenerator idGenerator,
        IPayloadSerializer payloadSerializer)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
        _payloadSerializer = payloadSerializer;
    }

    /// <summary>
    /// Produces a single message to the database.
    /// </summary>
    /// <typeparam name="TK">The type of the message.</typeparam>
    /// <param name="message">The message to produce.</param>
    /// <param name="availableAfter">The time when the message should be available for consumption.</param>
    /// <param name="insertId">The insert ID for the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null,
        CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = _payloadSerializer.Serialize(message);
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        var payloadType = typeof(TK).Name;
        insertId ??= _idGenerator.GenerateId();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
            {
                new("@created_at", currentTime),
                new("@available_after", availableAfter),
                new("@payload_type", payloadType),
                new("@payload", payload),
                new("@insert_id", insertId),
                new("@trace_id", string.IsNullOrEmpty(traceId) ? DBNull.Value : traceId),
                new("@span_id", string.IsNullOrEmpty(spanId) ? DBNull.Value : spanId)
            };

        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        for (var index = 0; index < consumerTypes.Count; index++)
        {
            if (sqlBuilder.Length > 0)
            {
                sqlBuilder.Append(", ");
            }

            sqlBuilder.Append(
                $@"(@id_{index}, @created_at, @available_after, 0, @consumer_type_{index}, @payload_type, @payload, @insert_id, @trace_id, @span_id)");

            parameters.Add(new NpgsqlParameter($"@id_{index}", _idGenerator.GenerateId()));
            parameters.Add(new NpgsqlParameter($"@consumer_type_{index}", consumerTypes[index]));
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id, trace_id, span_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    /// <summary>
    /// Produces a list of messages to the database.
    /// </summary>
    /// <typeparam name="TK">The type of the messages.</typeparam>
    /// <param name="messages">The messages to produce.</param>
    /// <param name="availableAfter">The time when the messages should be available for consumption.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProduceList<TK>(List<TK> messages, long? availableAfter = null,
        CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
            {
                new("@created_at", currentTime),
                new("@available_after", availableAfter),
                new("@trace_id", string.IsNullOrEmpty(traceId) ? DBNull.Value : traceId),
                new("@span_id", string.IsNullOrEmpty(spanId) ? DBNull.Value : spanId)
            };

        var payloadType = typeof(TK).Name;
        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var insertId = _idGenerator.GenerateId();
            var payload = _payloadSerializer.Serialize(message);
            parameters.Add(new NpgsqlParameter($"@insert_id_{i}", insertId));
            parameters.Add(new NpgsqlParameter($"@payload_type_{i}", payloadType));
            parameters.Add(new NpgsqlParameter($"@payload_{i}", payload));

            for (var index = 0; index < consumerTypes.Count; index++)
            {
                if (sqlBuilder.Length > 0)
                {
                    sqlBuilder.Append(", ");
                }

                sqlBuilder.Append(
                    $@"(@id_{i}_{index}, @created_at, @available_after, 0, @consumer_type_{i}_{index}, @payload_type_{i}, @payload_{i}, @insert_id_{i}, @trace_id, @span_id)");

                parameters.Add(new NpgsqlParameter($"@id_{i}_{index}", _idGenerator.GenerateId()));
                parameters.Add(new NpgsqlParameter($"@consumer_type_{i}_{index}", consumerTypes[index]));
            }
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id, trace_id, span_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    /// <summary>
    /// Produces a scheduled message to the database.
    /// </summary>
    /// <param name="message">The scheduled message to produce.</param>
    public void Produce(ScheduledMessage message)
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = _dbContext.Set<ConsumerMessage>();
        var insertId = _idGenerator.GenerateId();
        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType))
        {
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = currentTime,
                ConsumerType = consumerId,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Attempts = 0,
                InsertId = insertId,
                TraceId = null,
                SpanId = null
            });
        }
    }
}
