using System.Collections.Generic;
using System.Linq;

namespace O5Control.Features.Staff;

internal static class RemoteAdminExport
{
    public static RemoteAdminConfigMessage Read()
    {
        var handler = ServerStatic.PermissionsHandler;
        var message = new RemoteAdminConfigMessage();
        if (handler is null)
            return message;

        foreach (var pair in handler.Groups)
        {
            var group = pair.Value;
            message.Groups.Add(new RemoteAdminGroup
            {
                Key = pair.Key,
                BadgeText = group.BadgeText ?? pair.Key,
                BadgeColor = group.BadgeColor ?? "default",
                KickPower = group.KickPower,
                RequiredKickPower = group.RequiredKickPower,
                Cover = group.Cover,
                HiddenByDefault = group.HiddenByDefault,
                Permissions = handler.Permissions
                    .Where(permission => permission.Value != 0 && (group.Permissions & permission.Value) == permission.Value)
                    .Select(permission => permission.Key)
                    .ToList(),
            });
        }

        foreach (var member in handler.Members)
            message.Members.Add(new RemoteAdminMember { UserId = member.Key, Group = member.Value });

        return message;
    }
}

internal sealed class RemoteAdminConfigMessage
{
    public List<RemoteAdminGroup> Groups { get; set; } = new();

    public List<RemoteAdminMember> Members { get; set; } = new();
}

internal sealed class RemoteAdminGroup
{
    public string Key { get; set; } = string.Empty;

    public string BadgeText { get; set; } = string.Empty;

    public string BadgeColor { get; set; } = "default";

    public byte KickPower { get; set; }

    public byte RequiredKickPower { get; set; }

    public bool Cover { get; set; }

    public bool HiddenByDefault { get; set; }

    public List<string> Permissions { get; set; } = new();
}

internal sealed class RemoteAdminMember
{
    public string UserId { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;
}
