using System;
using System.Collections.Generic;
using O5Control.Core;
using O5Control.Networking.Protocol;

namespace O5Control.Networking;

internal sealed class MessageRouter
{
    private readonly Dictionary<string, Action<Envelope>> _handlers = new(StringComparer.Ordinal);

    public void On(string op, Action<Envelope> handler) => _handlers[op] = handler;

    public void On<T>(string op, Action<T, Envelope> handler) where T : class
    {
        _handlers[op] = envelope =>
        {
            var payload = envelope.Read<T>();
            if (payload is null)
            {
                Log.Warn($"Ignored '{op}' with an empty payload.");
                return;
            }

            handler(payload, envelope);
        };
    }

    public void Dispatch(Envelope envelope)
    {
        if (!_handlers.TryGetValue(envelope.Op, out var handler))
        {
            Log.Debug($"No handler for '{envelope.Op}'.");
            return;
        }

        try
        {
            handler(envelope);
        }
        catch (Exception exception)
        {
            Log.Error($"Handler for '{envelope.Op}' failed: {exception}");
        }
    }
}
