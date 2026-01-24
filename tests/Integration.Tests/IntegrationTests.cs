using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Infrastructure;

namespace Integration.Tests;

public class IntegrationTests : IAsyncLifetime
{
    private readonly MsSqlTestcontainer _sql;
    private readonly RabbitMqTestcontainer _rabbit;
    private ApiFactory? _factory;

    public IntegrationTests()
    {
        _sql = new TestcontainersBuilder<MsSqlTestcontainer>()
            .WithDatabase(new MsSqlTestcontainerConfiguration
            {
                Password = "Your_password123"
            })
            .WithPortBinding(14340, 1433)
            .Build();

        _rabbit = new TestcontainersBuilder<RabbitMqTestcontainer>()
            .WithMessageBroker(new RabbitMqTestcontainerConfiguration
            {
                Username = "guest",
                Password = "guest"
            })
            .WithPortBinding(56740, 5672)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync().ConfigureAwait(false);
        await _rabbit.StartAsync().ConfigureAwait(false);
        _factory = new ApiFactory(_sql.ConnectionString, _rabbit.Hostname);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _sql.DisposeAsync().ConfigureAwait(false);
        await _rabbit.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Idempotent_payments_return_same_result()
    {
        var client = _factory!.CreateClient();
        var orderResponse = await client.PostAsJsonAsync("/api/v1/orders", new
         {
             storeId = "store-1",
             items = new[] { new { sku = "burger", qty = 1, price = 9.99m } },
             note = "no onions"
        }).ConfigureAwait(false);

        var created = await orderResponse.Content.ReadFromJsonAsync<dynamic>().ConfigureAwait(false);
        var orderId = Guid.Parse((string)created!.orderId.ToString());

        var headers = new Dictionary<string, string> { { "Idempotency-Key", "abc123" } };
        var payment1 = await PostPaymentAsync(client, orderId, headers).ConfigureAwait(false);
        var payment2 = await PostPaymentAsync(client, orderId, headers).ConfigureAwait(false);

        payment1.PaymentId.Should().Be(payment2.PaymentId);
        payment1.Status.Should().Be(payment2.Status);

        using var scope = _factory.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync().ConfigureAwait(false);
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task Outbox_is_published_and_marked_processed()
    {
        var client = _factory!.CreateClient();
        var orderResponse = await client.PostAsJsonAsync("/api/v1/orders", new
         {
             storeId = "store-2",
             items = new[] { new { sku = "pizza", qty = 2, price = 12.50m } }
        }).ConfigureAwait(false);
        var created = await orderResponse.Content.ReadFromJsonAsync<dynamic>().ConfigureAwait(false);
        var orderId = Guid.Parse((string)created!.orderId.ToString());

        var headers = new Dictionary<string, string> { { "Idempotency-Key", "xyz789" } };
        await PostPaymentAsync(client, orderId, headers).ConfigureAwait(false);

        var scope = _factory.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < TimeSpan.FromSeconds(10))
            {
                var processed = await db.OutboxMessages.Where(o => o.ProcessedOn != null).CountAsync().ConfigureAwait(false);
                if (processed > 0)
                {
                    processed.Should().BeGreaterOrEqualTo(1);
                    return;
                }

                await Task.Delay(200).ConfigureAwait(false);
            }

            throw new TimeoutException("Outbox not processed in time");
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Inbox_deduplicates_duplicate_messages()
    {
        var messageId = Guid.NewGuid();
        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Hostname,
            DispatchConsumersAsync = true
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("fnb.events", ExchangeType.Topic, durable: true);
        channel.QueueDeclare("payment.requested.q", durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind("payment.requested.q", "fnb.events", "payment.requested.v1");

        var evt = new PaymentRequestedV1(messageId, DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), 10m, "card", Guid.NewGuid().ToString(), string.Empty);
        var payload = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var props = channel.CreateBasicProperties();
        props.Headers = new Dictionary<string, object> { { "message-id", Encoding.UTF8.GetBytes(messageId.ToString()) } };

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        channel.BasicPublish("fnb.events", "payment.requested.v1", props, body);
        channel.BasicPublish("fnb.events", "payment.requested.v1", props, body);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var processor = new Payment.Consumer.BoundedChannelProcessor<global::RabbitMQ.Client.Events.BasicDeliverEventArgs>(
            1, 2, (_, _) => Task.CompletedTask, cts.Token);

        using var scope = _factory!.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.InboxMessages.Add(new Order.Infrastructure.Entities.InboxMessage { MessageId = messageId, Consumer = "test", ReceivedOn = DateTime.UtcNow });
        await db.SaveChangesAsync().ConfigureAwait(false);

        var duplicate = new Order.Infrastructure.Entities.InboxMessage { MessageId = messageId, Consumer = "test", ReceivedOn = DateTime.UtcNow };
        await db.InboxMessages.AddAsync(duplicate).ConfigureAwait(false);
        var thrown = await Record.ExceptionAsync(() => db.SaveChangesAsync()).ConfigureAwait(false);
        thrown.Should().NotBeNull();

        await processor.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<(Guid PaymentId, string Status)> PostPaymentAsync(HttpClient client, Guid orderId, IDictionary<string, string> headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(new { amount = 10m, method = "card" })
        };

        request.Headers.Add("Idempotency-Key", headers["Idempotency-Key"]);
        var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<dynamic>().ConfigureAwait(false);
        return (Guid.Parse((string)payment!.paymentId.ToString()), (string)payment.status.ToString());
    }
}
