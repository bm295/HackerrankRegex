using System.Net.Http.Json;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Consumer.Options;
using Shared.Common;
using Shared.Observability;

namespace Payment.Consumer;

public sealed class ExternalPspClient
{
    private readonly HttpClient _client;
    private readonly ILogger<ExternalPspClient> _logger;
    private readonly ResilienceOptions _options;

    public ExternalPspClient(HttpClient client, IOptions<ResilienceOptions> options, ILogger<ExternalPspClient> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ChargeAsync(PaymentRequestedV2 payment, CancellationToken cancellationToken)
    {
        using var cts = cancellationToken.LinkWithTimeout(TimeSpan.FromMilliseconds(_options.TimeoutMs));
        var response = await _client.PostAsJsonAsync("/api/v1/external/psp", new { payment.OrderId, payment.PaymentId, payment.Amount }, cts.Token).ConfigureAwait(false);
        _logger.LogInformation("PSP responded {StatusCode} for payment {PaymentId}", response.StatusCode, payment.PaymentId);
        return response.IsSuccessStatusCode;
    }
}
