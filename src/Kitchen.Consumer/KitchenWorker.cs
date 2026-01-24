using System.Text;
using System.Text.Json;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kitchen.Consumer;

public class KitchenWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KitchenWorker> _logger;
    private readonly Order.Infrastructure.Options.RabbitMqOptions _rabbitOptions;
    private IConnection? _connection;
    private IModel? _channel;
    private const string ConsumerName = "kitchen-consumer";

    public KitchenWorker(IServiceScopeFactory scopeFactory, ILogger<KitchenWorker> logger, IOptions<Order.Infrastructure.Options.RabbitMqOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _rabbitOptions = options.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Host,
            UserName = _rabbitOptions.User,
            Password = _rabbitOptions.Pass,
            DispatchConsumersAsync = true
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_rabbitOptions.Exchange, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare("kitchen.ticket.q", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind("kitchen.ticket.q", _rabbitOptions.Exchange, "order.paid.v1");
        _channel.BasicQos(0, _rabbitOptions.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume("kitchen.ticket.q", autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var messageId = Guid.TryParse(ea.BasicProperties.Headers?["message-id"] as string, out var parsed)
            ? parsed
            : Guid.NewGuid();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KitchenDbContext>();

        var exists = await db.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Consumer == ConsumerName).ConfigureAwait(false);
        if (exists)
        {
            _channel!.BasicAck(ea.DeliveryTag, false);
            return;
        }

        db.InboxMessages.Add(new Storage.InboxMessage { MessageId = messageId, Consumer = ConsumerName, ReceivedOn = DateTime.UtcNow });
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        var payload = Encoding.UTF8.GetString(ea.Body.Span);
        var evt = JsonSerializer.Deserialize<OrderPaid>(payload);
        _logger.LogInformation("Kitchen ticket created for order {OrderId} payment {PaymentId}", evt?.OrderId, evt?.PaymentId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Task.Delay(50, cts.Token).ConfigureAwait(false);

        _channel!.BasicAck(ea.DeliveryTag, false);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
