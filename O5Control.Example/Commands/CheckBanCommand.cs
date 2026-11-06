using System;
using CommandSystem;
using O5Control.API;

namespace O5Control.Example.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class CheckBanCommand : ICommand, IUsageProvider
{
    public string Command => "checkban";

    public string[] Aliases { get; } = Array.Empty<string>();

    public string Description => "Checks whether a user id is banned in this O5 Control instance.";

    public string[] Usage { get; } = { "UserId" };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (arguments.Count < 1)
        {
            response = "Usage: checkban <userid>";
            return false;
        }

        var userId = arguments.At(0);
        response = O5.TryGetBan(userId, out var ban) && ban is not null
            ? $"{userId} is banned: {ban.Reason} (expires {ban.ExpiresAt?.ToString("u") ?? "never"})"
            : $"{userId} has no active ban.";

        return true;
    }
}
