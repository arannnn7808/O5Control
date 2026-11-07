using System.Collections.Generic;
using O5Control.API.Models;

namespace O5Control.Networking.Protocol.Messages;

internal sealed class HelloMessage
{
    public string PluginVersion { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public string LabApiVersion { get; set; } = string.Empty;

    public ushort Port { get; set; }

    public int MaxPlayers { get; set; }
}

internal sealed class WelcomeMessage
{
    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public InstanceInfo Instance { get; set; } = new();

    public ServerSettings Settings { get; set; } = new();

    public List<Sanction> Bans { get; set; } = new();

    public List<Sanction> Mutes { get; set; } = new();

    public List<StaffAssignment> Staff { get; set; } = new();
}

internal sealed class InstanceInfo
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;
}

internal sealed class ServerSettings
{
    public bool AutoUpdate { get; set; } = true;

    public string UpdateChannel { get; set; } = "stable";

    public bool StaffSync { get; set; } = true;

    public bool ChatLogs { get; set; } = true;

    public bool Maintenance { get; set; }
}

internal sealed class ErrorMessage
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
