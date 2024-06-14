﻿using System.Text;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.MsSql;

public sealed class MsSqlProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public MsSqlProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public async Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null, CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);
        var payloadType = typeof(TK).Name;
        insertId ??= _idGenerator.GenerateId();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<SqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter),
            new("@payload_type", payloadType),
            new("@payload", payload),
            new("@insert_id", insertId)
        };

        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        for (var index = 0; index < consumerTypes.Count; index++)
        {
            if (sqlBuilder.Length > 0)
            {
                sqlBuilder.Append(", ");
            }

            sqlBuilder.Append(
                $@"(@id_{index}, @created_at, @available_after, 0, @consumer_type_{index}, @payload_type, @payload, @insert_id)");

            parameters.Add(new SqlParameter($"@id_{index}", _idGenerator.GenerateId()));
            parameters.Add(new SqlParameter($"@consumer_type_{index}", consumerTypes[index]));
        }

        var sql = $@"
    MERGE INTO consumer_messages AS target
    USING (VALUES {sqlBuilder}) AS source (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id)
    ON target.insert_id = source.insert_id
    WHEN NOT MATCHED BY TARGET THEN 
        INSERT (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id)
        VALUES (source.id, source.created_at, source.available_after, source.attempts, source.consumer_type, source.payload_type, source.payload, source.insert_id);";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    public async Task ProduceList<TK>(List<TK> messages, long? availableAfter = null, CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var sqlBuilder = new StringBuilder();
        var parameters = new List<SqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter)
        };

        var payloadType = typeof(TK).Name;
        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var insertId = _idGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);
            parameters.Add(new SqlParameter($"@insert_id_{i}", insertId));
            parameters.Add(new SqlParameter($"@payload_type_{i}", payloadType));
            parameters.Add(new SqlParameter($"@payload_{i}", payload));

            for (var index = 0; index < consumerTypes.Count; index++)
            {
                if (sqlBuilder.Length > 0)
                {
                    sqlBuilder.Append(", ");
                }

                sqlBuilder.Append(
                    $@"(@id_{i}_{index}, @created_at, @available_after, 0, @consumer_type_{i}_{index}, @payload_type_{i}, @payload_{i}, @insert_id_{i})");

                parameters.Add(new SqlParameter($"@id_{i}_{index}", _idGenerator.GenerateId()));
                parameters.Add(new SqlParameter($"@consumer_type_{i}_{index}", consumerTypes[index]));
            }
        }

        var sql = $@"
            MERGE INTO consumer_messages AS target
            USING (VALUES {sqlBuilder}) AS source (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id)
            ON target.insert_id = source.insert_id
            WHEN NOT MATCHED BY TARGET THEN 
                INSERT (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id)
                VALUES (source.id, source.created_at, source.available_after, source.attempts, source.consumer_type, source.payload_type, source.payload, source.insert_id);";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

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
                InsertId = insertId
            });
        }
    }
}