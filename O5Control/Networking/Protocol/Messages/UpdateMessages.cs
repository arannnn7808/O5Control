namespace O5Control.Networking.Protocol.Messages;

internal sealed class UpdateAvailableMessage
{
    public string Version { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }

    public bool Mandatory { get; set; }
}

internal sealed class UpdateStateMessage
{
    public string Version { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string? Detail { get; set; }
}
