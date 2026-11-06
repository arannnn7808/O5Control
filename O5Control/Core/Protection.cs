using LabApi.Features.Wrappers;

namespace O5Control.Core;

internal static class Protection
{
    public static bool IsProtected(Player? player) => player is not null && player.IsNorthwoodStaff;

    public static bool IsProtected(CentralAuthPreauthFlags flags) => (flags & CentralAuthPreauthFlags.NorthwoodStaff) != 0;

    public static bool IsProtected(string userId) => Player.TryGet(userId, out var player) && IsProtected(player);
}
