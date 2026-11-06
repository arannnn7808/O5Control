using System;
using System.Collections.Generic;

namespace O5Control.API.Models;

public sealed class PlayerProfile
{
    public string UserId { get; set; } = string.Empty;

    public string? Nickname { get; set; }

    public string? DiscordId { get; set; }

    public string? DiscordName { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    public DateTimeOffset? LastSeen { get; set; }

    public int PlaytimeMinutes { get; set; }

    public List<string> Flags { get; set; } = new();

    public List<Sanction> ActiveSanctions { get; set; } = new();

    public bool IsLinked => DiscordId is not null;
}
