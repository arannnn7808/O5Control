using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MEC;

namespace O5Control.Core;

internal sealed class MainThread : IDisposable
{
    private const int MaxActionsPerFrame = 256;

    private readonly ConcurrentQueue<Action> _queue = new();
    private CoroutineHandle _handle;
    private bool _running;

    public void Start()
    {
        _running = true;
        _handle = Timing.RunCoroutine(Drain(), Segment.Update);
    }

    public void Post(Action action) => _queue.Enqueue(action);

    public void Dispose()
    {
        _running = false;
        Timing.KillCoroutines(_handle);

        while (_queue.TryDequeue(out _))
        {
        }
    }

    private IEnumerator<float> Drain()
    {
        while (_running)
        {
            var processed = 0;

            while (processed < MaxActionsPerFrame && _queue.TryDequeue(out var action))
            {
                processed++;

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Log.Error($"Main thread action failed: {exception}");
                }
            }

            yield return Timing.WaitForOneFrame;
        }
    }
}
