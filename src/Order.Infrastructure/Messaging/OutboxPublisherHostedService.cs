using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Order.Infrastructure.Entities;
using Order.Infrastructure.Options;
using RabbitMQ.Client;

namespace Order.Infrastructure.Messaging;

public class OutboxPublisherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxPublisherHostedService> _logger;
    private IConnection? _connection;

    public OutboxPublisherHostedService(IServiceScopeFactory scopeFactory, IOptions<RabbitMqOptions> rabbitOptions, IOptions<OutboxOptions> outboxOptions, ILogger<OutboxPublisherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitOptions = rabbitOptions.Value;
        _options = outboxOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Host,
            UserName = _rabbitOptions.User,
            Password = _rabbitOptions.Pass,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        using var channel = _connection.CreateModel();
        channel.ExchangeDeclare(_rabbitOptions.Exchange, ExchangeType.Topic, durable: true);

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PollIntervalMs));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await PublishBatchAsync(channel, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PublishBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var messages = await db.OutboxMessages
            .Where(o => o.ProcessedOn == null)
            .OrderBy(o => o.OccurredOn)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(channel, message, cancellationToken).ConfigureAwait(false);
                message.ProcessedOn = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                message.Attempts++;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} type {Type}", message.Id, message.Type);
            }
        }
    }

    private Task PublishAsync(IModel channel, OutboxMessage message, CancellationToken cancellationToken)
    {
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.Headers = new Dictionary<string, object?>
        {
            { "traceparent", message.TraceParent },
            { "x-correlation-id", message.CorrelationId }
        };

        var body = Encoding.UTF8.GetBytes(message.Payload);
        channel.BasicPublish(_rabbitOptions.Exchange, message.Type, basicProperties: props, body: body);
        _logger.LogInformation("Published outbox message {MessageId} type {Type}", message.Id, message.Type);
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _connection?.Close();
        _connection?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
