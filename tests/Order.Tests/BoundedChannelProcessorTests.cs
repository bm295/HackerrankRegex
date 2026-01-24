using System.Linq;
using System.Threading;
using FluentAssertions;
using Payment.Consumer;
using Xunit;

namespace Order.Tests;

public class BoundedChannelProcessorTests
{
    [Fact]
    public async Task Should_respect_max_concurrency()
    {
        var maxConcurrency = 3;
        var capacity = 6;
        var active = 0;
        var peak = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var processor = new BoundedChannelProcessor<int>(maxConcurrency, capacity, async (_, ct) =>
        {
            var current = Interlocked.Increment(ref active);
            peak = Math.Max(peak, current);
            await Task.Delay(50, ct).ConfigureAwait(false);
            Interlocked.Decrement(ref active);
        }, cts.Token);

        try
        {
            var tasks = Enumerable.Range(0, 10).Select(i => processor.EnqueueAsync(i, cts.Token).AsTask());
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await Task.Delay(200, cts.Token).ConfigureAwait(false); // allow processing

            peak.Should().BeLessOrEqualTo(maxConcurrency);
        }
        finally
        {
            await processor.DisposeAsync().ConfigureAwait(false);
        }
    }
}
