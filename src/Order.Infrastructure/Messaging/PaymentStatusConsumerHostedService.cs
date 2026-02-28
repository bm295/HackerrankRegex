using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Messaging.Contracts;
using Order.Infrastructure.Entities;
using Order.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Order.Infrastructure.Messaging;

public class PaymentStatusConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<PaymentStatusConsumerHostedService> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private const string ConsumerName = "order-api-payment-status";

    public PaymentStatusConsumerHostedService(IServiceScopeFactory scopeFactory, IOptions<RabbitMqOptions> options, ILogger<PaymentStatusConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.User,
            Password = _options.Pass,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

        var args = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", $"{_options.Exchange}.dlx" }
        };

        _channel.QueueDeclare("payment.status.q", durable: true, exclusive: false, autoDelete: false, arguments: args);
        _channel.QueueBind("payment.status.q", _options.Exchange, "payment.succeeded.v1");
        _channel.QueueBind("payment.status.q", _options.Exchange, "payment.failed.v1");
        _channel.BasicQos(0, _options.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume("payment.status.q", autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var routingKey = ea.RoutingKey;
        var body = Encoding.UTF8.GetString(ea.Body.Span);

        var messageId = Guid.TryParse(ea.BasicProperties.Headers?.ContainsKey("message-id") == true
            ? Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["message-id"])
            : string.Empty, out var parsed)
            ? parsed
            : Guid.NewGuid();

        var alreadyProcessed = await db.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Consumer == ConsumerName).ConfigureAwait(false);
        if (alreadyProcessed)
        {
            _channel!.BasicAck(ea.DeliveryTag, false);
            return;
        }

        db.InboxMessages.Add(new InboxMessage { MessageId = messageId, Consumer = ConsumerName, ReceivedOn = DateTime.UtcNow });

        if (routingKey == "payment.succeeded.v1")
        {
            var evt = JsonSerializer.Deserialize<PaymentSucceeded>(body);
            if (evt is not null)
            {
                await HandleSuccessAsync(db, evt).ConfigureAwait(false);
            }
        }
        else if (routingKey == "payment.failed.v1")
        {
            var evt = JsonSerializer.Deserialize<PaymentFailed>(body);
            if (evt is not null)
            {
                await HandleFailureAsync(db, evt).ConfigureAwait(false);
            }
        }

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        _channel!.BasicAck(ea.DeliveryTag, false);
    }

    private static Task HandleSuccessAsync(OrderDbContext db, PaymentSucceeded evt)
    {
        var order = db.Orders.Include(o => o.Payment).FirstOrDefault(o => o.Id == evt.OrderId);
        if (order?.Payment is null)
        {
            return Task.CompletedTask;
        }

        order.MarkPaid(evt.PaymentId);

        var orderPaid = new Order.Infrastructure.Entities.OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new OrderPaid(Guid.NewGuid(), DateTimeOffset.UtcNow, order.Id, evt.PaymentId, order.StoreId, evt.CorrelationId, evt.TraceParent)),
            Type = "order.paid.v1",
            TraceParent = evt.TraceParent,
            CorrelationId = evt.CorrelationId
        };

        db.OutboxMessages.Add(orderPaid);
        return Task.CompletedTask;
    }

    private static Task HandleFailureAsync(OrderDbContext db, PaymentFailed evt)
    {
        var order = db.Orders.Include(o => o.Payment).FirstOrDefault(o => o.Id == evt.OrderId);
        order?.MarkPaymentFailed(evt.PaymentId, evt.Reason);
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _channel?.Close();
        }
        catch (Exception)
        {
            // Best-effort shutdown: channel may already be closed.
        }

        try
        {
            _connection?.Close();
        }
        catch (Exception)
        {
            // Best-effort shutdown: connection may already be closed.
        }

        return base.StopAsync(cancellationToken);
    }
}
