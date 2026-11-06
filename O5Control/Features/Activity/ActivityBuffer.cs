using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using MEC;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;

namespace O5Control.Features.Activity;

internal sealed class ActivityBuffer : IFeature
{
    private const int MaxBatchSize = 100;
    private const int MaxBuffered = 5000;
    private const float FlushIntervalSeconds = 2f;

    private readonly ConcurrentQueue<ActivityEntry> _queue = new();
    private readonly PanelClient _client;
    private CoroutineHandle _handle;
    private bool _running;
    private int _count;

    public ActivityBuffer(PanelClient client)
    {
        _client = client;
    }

    public void Register()
    {
        _running = true;
        _handle = Timing.RunCoroutine(FlushLoop(), Segment.Update);
    }

    public void Unregister()
    {
        _running = false;
        Timing.KillCoroutines(_handle);
        Flush();
    }

    public void Add(ActivityEntry entry)
    {
        _queue.Enqueue(entry);

        if (Interlocked.Increment(ref _count) > MaxBuffered && _queue.TryDequeue(out _))
            Interlocked.Decrement(ref _count);

        if (_count >= MaxBatchSize)
            Flush();
    }

    private IEnumerator<float> FlushLoop()
    {
        while (_running)
        {
            yield return Timing.WaitForSeconds(FlushIntervalSeconds);
            Flush();
        }
    }

    private void Flush()
    {
        if (!_client.IsConnected)
            return;

        while (!_queue.IsEmpty)
        {
            var batch = new List<ActivityEntry>(MaxBatchSize);

            while (batch.Count < MaxBatchSize && _queue.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _count);
                batch.Add(entry);
            }

            if (batch.Count == 0)
                return;

            _client.Send(Ops.ActivityBatch, new { entries = batch });
        }
    }
}
