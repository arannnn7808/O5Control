using System;
using System.Collections.Generic;
using LabApi.Features.Wrappers;

namespace O5Control.Features.Activity;

internal sealed class ActivityEntry
{
    public string Type { get; set; } = string.Empty;

    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;

    public ActivityActor? Actor { get; set; }

    public ActivityActor? Target { get; set; }

    public string? Text { get; set; }

    public Dictionary<string, string>? Data { get; set; }
}

internal sealed class ActivityActor
{
    public string? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Role { get; set; }

    public static ActivityActor? From(Player? player)
    {
        if (player is null)
            return null;

        if (player.IsHost)
            return new ActivityActor { Name = "Server" };

        return new ActivityActor
        {
            UserId = player.DoNotTrack ? null : player.UserId,
            Name = player.Nickname,
            Role = player.Role.ToString(),
        };
    }

    public static ActivityActor From(CommandSender sender) => new()
    {
        UserId = sender.SenderId,
        Name = sender.Nickname,
    };
}

internal static class ActivityTypes
{
    public const string AdminChat = "admin_chat";
    public const string Command = "command";
    public const string Kill = "kill";
    public const string Join = "join";
    public const string Leave = "leave";
    public const string Report = "report";
    public const string CheaterReport = "cheater_report";
    public const string RoundStart = "round_start";
    public const string RoundEnd = "round_end";
    public const string RoleChange = "role_change";
    public const string Custom = "custom";
}
