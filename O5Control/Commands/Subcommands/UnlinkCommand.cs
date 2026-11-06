using System;
using CommandSystem;

namespace O5Control.Commands.Subcommands;

public sealed class UnlinkCommand : ICommand, IUsageProvider
{
    public string Command => "unlink";

    public string[] Aliases { get; } = Array.Empty<string>();

    public string Description => "Removes this server's credential. The panel keeps its history.";

    public string[] Usage { get; } = { "confirm" };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandContext.CanManage(sender, out response) || !CommandContext.TryGetRuntime(out var runtime, out response))
            return false;

        if (arguments.Count < 1 || !string.Equals(arguments.At(0), "confirm", StringComparison.OrdinalIgnoreCase))
        {
            response = "This disconnects the server from O5 Control. Run 'o5 unlink confirm' to continue.";
            return false;
        }

        runtime.Client.Stop();
        runtime.Credentials.Clear();
        response = "Unlinked. Run 'o5 setup <code>' to link it again.";
        return true;
    }
}
