using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Consumer.Storage;
using RabbitMQ.Client;

namespace Payment.Consumer;

public class PaymentOutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<Order.Infrastructure.Options.RabbitMqOptions> _rabbitOptions;
    private readonly IOptions<Order.Infrastructure.Options.OutboxOptions> _options;
    private readonly ILogger<PaymentOutboxPublisher> _logger;
    private IConnection? _connection;

    public PaymentOutboxPublisher(IServiceScopeFactory scopeFactory, IOptions<Order.Infrastructure.Options.RabbitMqOptions> rabbitOptions, IOptions<Order.Infrastructure.Options.OutboxOptions> options, ILogger<PaymentOutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitOptions = rabbitOptions;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Value.Host,
            UserName = _rabbitOptions.Value.User,
            Password = _rabbitOptions.Value.Pass
        };
        _connection = factory.CreateConnection();
        using var channel = _connection.CreateModel();
        channel.ExchangeDeclare(_rabbitOptions.Value.Exchange, ExchangeType.Topic, durable: true);

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.Value.PollIntervalMs));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await PublishAsync(channel, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentConsumerDbContext>();
        var batch = await db.OutboxMessages
            .Where(o => o.ProcessedOn == null)
            .OrderBy(o => o.OccurredOn)
            .Take(_options.Value.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in batch)
        {
            try
            {
                var props = channel.CreateBasicProperties();
                props.Persistent = true;
                props.Headers = new Dictionary<string, object?>
                {
                    { "traceparent", message.TraceParent },
                    { "x-correlation-id", message.CorrelationId },
                    { "message-id", message.Id.ToString() }
                };

                channel.BasicPublish(_rabbitOptions.Value.Exchange, message.Type, props, Encoding.UTF8.GetBytes(message.Payload));
                message.ProcessedOn = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                message.Attempts++;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Failed to publish payment outbox message {Id}", message.Id);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
