using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;
using O5Control.API;

namespace O5Control.Example.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class NetBanCommand : ICommand, IUsageProvider
{
    public string Command => "netban";

    public string[] Aliases { get; } = Array.Empty<string>();

    public string Description => "Bans a player on every server of the instance through O5 Control.";

    public string[] Usage { get; } = { "PlayerId", "Hours (0 = permanent)", "Reason" };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission(PlayerPermissions.BanningUpToDay))
        {
            response = "You are not allowed to ban.";
            return false;
        }

        if (arguments.Count < 3 || !int.TryParse(arguments.At(0), out var playerId) || !int.TryParse(arguments.At(1), out var hours))
        {
            response = "Usage: netban <player id> <hours, 0 = permanent> <reason>";
            return false;
        }

        if (!Player.TryGet(playerId, out var target))
        {
            response = "Player not found.";
            return false;
        }

        var reason = string.Join(" ", arguments.Skip(2));
        var duration = hours > 0 ? TimeSpan.FromHours(hours) : (TimeSpan?)null;
        Ban(target, reason, duration, Player.Get(sender), sender);

        response = $"Sending ban for {target.Nickname} to O5 Control…";
        return true;
    }

    private static async void Ban(Player target, string reason, TimeSpan? duration, Player? issuer, ICommandSender sender)
    {
        var result = await O5.BanAsync(target, reason, duration, issuer);

        sender.Respond(result.Success
            ? $"Banned {target.Nickname} ({result.Value!.Id})."
            : $"Ban failed: {result.Error}", result.Success);
    }
}
