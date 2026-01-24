using Shared.Common;

namespace Order.Application.Services;

public static class AntiPatternDemo
{
    public static async Task<IReadOnlyCollection<object>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<object>();

        // blocking Result/.Wait
        var blockingResult = await Task.Run(() =>
        {
            try
            {
                Task.Delay(50, cancellationToken).Wait(cancellationToken);
                return "No deadlock thanks to cancellation-aware wait";
            }
            catch (OperationCanceledException)
            {
                return "Blocking wait cancelled";
            }
        }, cancellationToken).ConfigureAwait(false);
        results.Add(new { antiPattern = "Blocking .Result/.Wait", note = blockingResult });

        // fire-and-forget
        var lostException = await SafeFireAndForgetAsync(cancellationToken).ConfigureAwait(false);
        results.Add(new { antiPattern = "Fire-and-forget", note = lostException });

        // forgetting cancellation token
        var forgotten = await ForgetCancellationAsync(cancellationToken).ConfigureAwait(false);
        results.Add(new { antiPattern = "Forgetting CancellationToken", note = forgotten });

        // async over sync
        results.Add(new { antiPattern = "Async-over-sync", note = "Avoid wrapping synchronous I/O in Task.Run unless needed for CPU offloading." });

        return results;
    }

    private static async Task<string> SafeFireAndForgetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(async () =>
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Captured and logged");
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"Observed exception: {ex.Message}";
        }

        return "Completed without swallowing exceptions";
    }

    private static async Task<string> ForgetCancellationAsync(CancellationToken cancellationToken)
    {
        using var cts = cancellationToken.LinkWithTimeout(TimeSpan.FromMilliseconds(30));
        try
        {
            await Task.Delay(1000, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return "Honored cancellation";
        }

        return "Should have cancelled";
    }
}
