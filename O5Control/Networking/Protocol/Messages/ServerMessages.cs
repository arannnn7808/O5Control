using System.Collections.Generic;

namespace O5Control.Networking.Protocol.Messages;

internal sealed class StatusMessage
{
    public int PlayerCount { get; set; }

    public int MaxPlayers { get; set; }

    public double Tps { get; set; }

    public bool RoundStarted { get; set; }

    public bool RoundEnded { get; set; }

    public int RoundSeconds { get; set; }

    public List<OnlinePlayer> Players { get; set; } = new();
}

internal sealed class OnlinePlayer
{
    public string UserId { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public int PlayerId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Team { get; set; } = string.Empty;

    public string? Group { get; set; }
}

internal sealed class PlayerJoinedMessage
{
    public string UserId { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public bool DoNotTrack { get; set; }

    public bool NorthwoodStaff { get; set; }
}

internal sealed class PlayerLeftMessage
{
    public string UserId { get; set; } = string.Empty;
}

internal sealed class PlayerKickMessage
{
    public string UserId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

internal sealed class PlayerMessageMessage
{
    public string UserId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public ushort Duration { get; set; } = 6;
}

internal sealed class PlayerLookupMessage
{
    public string UserId { get; set; } = string.Empty;
}

internal sealed class CommandRunMessage
{
    public string Command { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
}

internal sealed class CommandResultMessage
{
    public string Output { get; set; } = string.Empty;
}

internal sealed class ServerBroadcastMessage
{
    public string Text { get; set; } = string.Empty;

    public ushort Duration { get; set; } = 8;

    public bool ClearPrevious { get; set; }
}
