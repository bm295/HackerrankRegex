using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Payment.Consumer;
using Payment.Consumer.Options;
using Polly;
using Polly.Extensions.Http;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true);

builder.Services.Configure<Order.Infrastructure.Options.RabbitMqOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<Order.Infrastructure.Options.OutboxOptions>(builder.Configuration.GetSection("Outbox"));
builder.Services.Configure<ConsumerOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<ResilienceOptions>(builder.Configuration.GetSection("Resilience"));

builder.Services.AddDbContext<PaymentConsumerDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.AddHttpClient<ExternalPspClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
}).AddPolicyHandler((sp, _) =>
{
    var opts = sp.GetRequiredService<IOptions<ResilienceOptions>>().Value;
    var retry = HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(opts.RetryCount, retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt + Random.Shared.Next(0, 50)));
    var circuit = HttpPolicyExtensions.HandleTransientHttpError()
        .CircuitBreakerAsync(opts.CircuitBreakerFailures, TimeSpan.FromSeconds(opts.CircuitBreakerWindowSeconds));
    var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(opts.TimeoutMs));
    return Policy.WrapAsync(retry, circuit, timeout);
});

builder.Services.AddHostedService<PaymentConsumerService>();
builder.Services.AddHostedService<PaymentOutboxPublisher>();

var app = builder.Build();
await app.RunAsync().ConfigureAwait(false);
