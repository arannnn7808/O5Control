using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using O5Control.Networking.Protocol;

namespace O5Control.Networking;

internal sealed class PendingRequests
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Envelope>> _pending = new();

    public Task<Envelope> Register(string id, TimeSpan timeout)
    {
        var source = new TaskCompletionSource<Envelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = source;

        var cancellation = new CancellationTokenSource(timeout);
        cancellation.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var expired))
                expired.TrySetException(new TimeoutException());

            cancellation.Dispose();
        });

        return source.Task;
    }

    public bool TryComplete(Envelope reply)
    {
        if (reply.Re is null || !_pending.TryRemove(reply.Re, out var source))
            return false;

        source.TrySetResult(reply);
        return true;
    }

    public void Cancel(string id)
    {
        if (_pending.TryRemove(id, out var source))
            source.TrySetCanceled();
    }

    public void FailAll(Exception reason)
    {
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var source))
                source.TrySetException(reason);
        }
    }
}
