using System.Threading.Channels;

namespace Payment.Consumer;

public sealed class BoundedChannelProcessor<T> : IAsyncDisposable
{
    private readonly Channel<T> _channel;
    private readonly List<Task> _workers = new();
    private readonly Func<T, CancellationToken, Task> _handler;

    public BoundedChannelProcessor(int maxConcurrency, int capacity, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        _handler = handler;
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        for (var i = 0; i < maxConcurrency; i++)
        {
            _workers.Add(Task.Run(() => WorkerAsync(cancellationToken), cancellationToken));
        }
    }

    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(item, cancellationToken);

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                await _handler(item, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await Task.WhenAll(_workers).ConfigureAwait(false);
    }
}
