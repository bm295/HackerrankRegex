using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Infrastructure;

namespace Integration.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _rabbitHost;

    public ApiFactory(string connectionString, string rabbitHost)
    {
        _connectionString = connectionString;
        _rabbitHost = rabbitHost;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = _connectionString,
                ["RabbitMQ:Host"] = _rabbitHost
            };
            config.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<OrderDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }
}
