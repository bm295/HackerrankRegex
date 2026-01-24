using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Abstractions;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Options;
using Order.Infrastructure.Repositories;

namespace Order.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SqlServer"));
        });

        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));
        services.Configure<ProcessingOptions>(configuration.GetSection("Processing"));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddHostedService<OutboxPublisherHostedService>();
        services.AddHostedService<PaymentStatusConsumerHostedService>();
        return services;
    }
}
