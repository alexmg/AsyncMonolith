# AsyncMonolith ![Image](AsyncMonolith/logo.png)

AsyncMonolith is a lightweight dotnet library that facillitates simple asynchronous processes in monolithic dotnet apps.

# Overview

- Makes building event driven architectures simple
- Produce messages transactionally along with changes to your domain
- Messages stored in your DB context so you have full control over them
- Supports running multiple instances / versions of your application
- Schedule messages to be processed using Chron expressions
- Automatic message retries
- Automatically routes messages to multiple consumers

# Warnings

Async Monolith is not a replacement for a message broker, there are many reasons why you may require one including:
- Extremely high message throughput (Async Monolith will tax your DB)
- Message ordering (Not currently supported)
- Communicating between different services (It's in the name)

# Dev log

Make sure to check this table before updating the nuget package in your solution, you may be required to add an ef migration.
| Version      | Description | Requires Migration |
| ----------- | ----------- |----------- |
| 1.0.4      | Added poisoned message table   | Yes |
| 1.0.3      | Added mysql support   | Yes |
| 1.0.2      | Scheduled messages use Chron expressions   | Yes |
| 1.0.1      | Added Configurable settings    | No |
| 1.0.0      | Initial   | Yes |

# Quick start guide 
(for a more detailed example look at the Demo project)

```csharp

    // Add Db Sets
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
        public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
        public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;
    }

    // Register services
    builder.Services.AddLogging();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(), new AsyncMonolithSettings()
    {
        AttemptDelay = 10, // Seconds before a failed message is retried
        MaxAttempts = 5, // Number of times a failed message is retried 
        ProcessorMinDelay = 10, // Minimum millisecond delay before the next message is processed
        ProcessorMaxDelay = 1000, // Maximum millisecond delay before the next message is processed
        DbType = DbType.PostgreSql, // Type of database being used (use DbType.Ef if not supported)
    });
    builder.Services.AddControllers();

    // Define Consumer Payloads
    public class ValueSubmitted : IConsumerPayload
    {
        [JsonPropertyName("value")]
        public required double Value { get; set; }
    }

    // Define Consumers
    public class ValueSubmittedConsumer : BaseConsumer<ValueSubmitted>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ProducerService<ApplicationDbContext> _producerService;
    
        public ValueSubmittedConsumer(ApplicationDbContext dbContext, ProducerService<ApplicationDbContext> producerService)
        {
            _dbContext = dbContext;
            _producerService = producerService;
        }
    
        public override Task Consume(ValueSubmitted message, CancellationToken cancellationToken)
        {
            ...
        }
    }

    // Produce / schedule messages
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly ScheduledMessageService<ApplicationDbContext> _scheduledMessageService;

    _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    });

    _scheduledMessageService.Schedule(new ValueSubmitted
    {
        Value = Random.Shared.NextDouble() * 100
    }, "*/5 * * * * *", "UTC");
    await _dbContext.SaveChangesAsync(cancellationToken);
```

# Message Handling Guide

## Producing Messages

- **Save Changes**: Ensure that you call `SaveChangesAsync` after producing a message, unless you are producing a message inside a consumer, where it is called automatically.
- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.

## Scheduling Messages

- **Frequency**: Scheduled messages will be produced repeatedly at the frequency defined by the given chron expression in the given timezone
- **Save Changes**: Ensure that you call `SaveChangesAsync` after creating a scheduled message, unless you are producing a message inside a consumer, where it is called automatically.
- **Transactional Persistence**: Schedule messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Processing**: Schedule messages will be processed sequentially after they are made available by their chron job, at which point they will be turned into Consumer Messages and inserted into the 'consumer_messages' table to be handled by their respective consumers.

## Consuming Messages

- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for available messages to process.
  - The query takes place every second and incrementally speeds up for every consecutive message processed.
  - Once there are no messages left, it will return to sampling the table every second.
- **Automatic Save Changes**: Each consumer will call `SaveChangesAsync` automatically after the abstract `Consume` method returns.

## Changing Consumer Payload Schema

- **Backwards Compatibility**: When modifying consumer payload schemas, ensure changes are backwards compatible so that existing messages with the old schema can still be processed.
- **Schema Migration**:
  - If changes are not backwards compatible, make the changes in a copy of the `ConsumerPayload` (with a different class name) and update all consumers to operate on the new payload.
  - Once all messages with the old payload schema have been processed, you can safely delete the old payload schema and its associated consumers.

## Consumer Failures

- **Retry Logic**: Messages will be retried up to 'MaxAttempts' times (with a 'AttemptDelay' seconds between attempts) until they are moved to the 'poisoned_messages' table.
- **Manual Intervention**: If a message is moved to the 'poisoned_messages' table, it will need to be manually removed from the database or moved back to the 'consumer_messages' table to be retried. Note that the poisoned message will only be retried a single time unless you set 'attempts' back to 0.
- **Monitoring**: Periodically monitor the 'poisoned_messages' table to ensure there are not too many failed messages.
