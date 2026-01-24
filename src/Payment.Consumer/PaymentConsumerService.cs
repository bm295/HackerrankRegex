using System.Text;
using System.Text.Json;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Consumer.Options;
using Payment.Consumer.Storage;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Payment.Consumer;

public class PaymentConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentConsumerService> _logger;
    private readonly ConsumerOptions _consumerOptions;
    private readonly IOptions<ResilienceOptions> _resilienceOptions;
    private readonly IOptions<Order.Infrastructure.Options.RabbitMqOptions> _rabbitOptions;
    private IConnection? _connection;
    private IModel? _channel;
    private BoundedChannelProcessor<BasicDeliverEventArgs>? _processor;
    private const string ConsumerName = "payment-consumer";

    public PaymentConsumerService(IServiceScopeFactory scopeFactory, IOptions<ConsumerOptions> consumerOptions, IOptions<ResilienceOptions> resilienceOptions, IOptions<Order.Infrastructure.Options.RabbitMqOptions> rabbitOptions, ILogger<PaymentConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _consumerOptions = consumerOptions.Value;
        _resilienceOptions = resilienceOptions;
        _rabbitOptions = rabbitOptions;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Value.Host,
            UserName = _rabbitOptions.Value.User,
            Password = _rabbitOptions.Value.Pass,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_rabbitOptions.Value.Exchange, ExchangeType.Topic, durable: true);

        var args = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", $"{_rabbitOptions.Value.Exchange}.dlx" }
        };

        _channel.QueueDeclare(_consumerOptions.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
        _channel.QueueDeclare(_consumerOptions.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueBind(_consumerOptions.QueueName, _rabbitOptions.Value.Exchange, "payment.requested.v1");
        _channel.QueueBind(_consumerOptions.QueueName, _rabbitOptions.Value.Exchange, "payment.requested.v2");
        _channel.BasicQos(0, _consumerOptions.PrefetchCount, false);

        _processor = new BoundedChannelProcessor<BasicDeliverEventArgs>(_consumerOptions.MaxConcurrency, _consumerOptions.Capacity, ProcessAsync, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(_consumerOptions.QueueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        return _processor!.EnqueueAsync(ea, CancellationToken.None).AsTask();
    }

    private async Task ProcessAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentConsumerDbContext>();
        var psp = scope.ServiceProvider.GetRequiredService<ExternalPspClient>();

        var headerBytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        if (ea.BasicProperties.Headers?.TryGetValue(MessageHeaders.MessageId, out var headerValue) == true && headerValue is byte[] headerArray)
        {
            headerBytes = headerArray;
        }

        var messageId = Guid.Parse(Encoding.UTF8.GetString(headerBytes));
        var traceParent = ea.BasicProperties.Headers?[MessageHeaders.TraceParent] is byte[] trace
            ? Encoding.UTF8.GetString(trace)
            : string.Empty;
        var correlationId = ea.BasicProperties.Headers?[MessageHeaders.CorrelationId] is byte[] corr
            ? Encoding.UTF8.GetString(corr)
            : string.Empty;

        var already = await db.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Consumer == ConsumerName, cancellationToken).ConfigureAwait(false);
        if (already)
        {
            Ack(ea);
            return;
        }

        db.InboxMessages.Add(new InboxMessage { MessageId = messageId, Consumer = ConsumerName, ReceivedOn = DateTime.UtcNow });

        var json = Encoding.UTF8.GetString(ea.Body.Span);
        PaymentRequestedV2? message = ea.RoutingKey.EndsWith(".v2")
            ? JsonSerializer.Deserialize<PaymentRequestedV2>(json)
            : MapV1(json);

        if (message is null)
        {
            Ack(ea);
            return;
        }

        var success = await psp.ChargeAsync(message, cancellationToken).ConfigureAwait(false);

        IntegrationMessage outgoing = success
            ? new PaymentSucceeded(Guid.NewGuid(), DateTimeOffset.UtcNow, message.OrderId, message.PaymentId, message.Amount, message.Method, correlationId, traceParent)
            : new PaymentFailed(Guid.NewGuid(), DateTimeOffset.UtcNow, message.OrderId, message.PaymentId, "PSP error", correlationId, traceParent);

        var payload = JsonSerializer.Serialize(outgoing, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.OutboxMessages.Add(new PaymentOutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Payload = payload,
            Type = success ? "payment.succeeded.v1" : "payment.failed.v1",
            TraceParent = traceParent,
            CorrelationId = correlationId
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Ack(ea);
    }

    private PaymentRequestedV2? MapV1(string json)
    {
        var v1 = JsonSerializer.Deserialize<PaymentRequestedV1>(json);
        if (v1 is null)
        {
            return null;
        }

        return new PaymentRequestedV2(v1.MessageId, v1.OccurredOn, v1.OrderId, v1.PaymentId, v1.Amount, v1.Method, "USD", string.Empty, v1.CorrelationId, v1.TraceParent);
    }

    private void Ack(BasicDeliverEventArgs ea) => _channel!.BasicAck(ea.DeliveryTag, false);

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
