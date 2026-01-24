using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationAndLogging(this IServiceCollection services)
    {
        services.AddSingleton<CorrelationContext>();
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        return services;
    }
}
