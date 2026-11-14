using System;
using System.Collections.Generic;

namespace O5Control.Networking.Protocol.Messages;

internal sealed class ConfigListMessage
{
    public List<ConfigFileInfo> Files { get; set; } = new();
}

internal sealed class ConfigFileInfo
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Scope { get; set; }

    public long Size { get; set; }

    public DateTime ModifiedAt { get; set; }
}

internal sealed class ConfigReadMessage
{
    public string Id { get; set; } = string.Empty;
}

internal sealed class ConfigFileMessage
{
    public string Id { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? Hash { get; set; }

    public string? Error { get; set; }
}

internal sealed class ConfigWriteMessage
{
    public string Id { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? Hash { get; set; }

    public string Issuer { get; set; } = string.Empty;
}

internal sealed class ConfigWriteResultMessage
{
    public string? Hash { get; set; }

    public bool Reloaded { get; set; }

    public string? Error { get; set; }
}
