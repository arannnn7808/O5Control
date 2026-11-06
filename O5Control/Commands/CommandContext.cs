using CommandSystem;
using O5Control.Core;

namespace O5Control.Commands;

internal static class CommandContext
{
    public static bool TryGetRuntime(out Runtime runtime, out string response)
    {
        var current = O5ControlPlugin.Instance?.Runtime;
        runtime = current!;
        response = current is null ? "O5Control is not running." : string.Empty;
        return current is not null;
    }

    public static bool CanManage(ICommandSender sender, out string response)
    {
        if (sender.CheckPermission(PlayerPermissions.ServerConsoleCommands))
        {
            response = string.Empty;
            return true;
        }

        response = "You need the ServerConsoleCommands permission.";
        return false;
    }
}
