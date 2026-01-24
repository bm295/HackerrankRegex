namespace Shared.Common;

public static class CancellationTokenExtensions
{
    public static CancellationTokenSource LinkWithTimeout(this CancellationToken token, TimeSpan timeout)
    {
        var timeoutCts = new CancellationTokenSource(timeout);
        return CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
    }
}
