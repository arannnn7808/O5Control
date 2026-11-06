using System.Collections.Generic;

namespace O5Control.API.Models;

public sealed class StaffAssignment
{
    public string UserId { get; set; } = string.Empty;

    public string RoleId { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string BadgeText { get; set; } = string.Empty;

    public string BadgeColor { get; set; } = "default";

    public byte KickPower { get; set; }

    public byte RequiredKickPower { get; set; }

    public bool Cover { get; set; } = true;

    public bool HiddenByDefault { get; set; }

    public List<string> Permissions { get; set; } = new();

    public string Source { get; set; } = "panel";
}
