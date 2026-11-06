using System;
using CommandSystem;

namespace O5Control.Commands.Subcommands;

public sealed class ReconnectCommand : ICommand
{
    public string Command => "reconnect";

    public string[] Aliases { get; } = { "rc" };

    public string Description => "Closes and reopens the connection to the panel.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandContext.CanManage(sender, out response) || !CommandContext.TryGetRuntime(out var runtime, out response))
            return false;

        if (runtime.Credentials.Current is null)
        {
            response = "This server is not linked yet. Run: o5 setup <code>";
            return false;
        }

        runtime.Relink();
        response = "Reconnecting to O5 Control…";
        return true;
    }
}
