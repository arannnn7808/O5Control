using System.Text.Json;

namespace O5Control.Networking.Protocol;

internal sealed class Envelope
{
    public string Op { get; set; } = string.Empty;

    public string? Id { get; set; }

    public string? Re { get; set; }

    public JsonElement D { get; set; }

    public T? Read<T>() => Json.Read<T>(D);
}

internal sealed class OutgoingEnvelope
{
    public OutgoingEnvelope(string op, object? data, string? id = null, string? re = null)
    {
        Op = op;
        D = data;
        Id = id;
        Re = re;
    }

    public string Op { get; }

    public string? Id { get; }

    public string? Re { get; }

    public object? D { get; }
}
