using System;
using System.Text;
using CommandSystem;
using O5Control.Core;

namespace O5Control.Commands.Subcommands;

public sealed class StatusCommand : ICommand
{
    public string Command => "status";

    public string[] Aliases { get; } = { "info" };

    public string Description => "Shows the connection state.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandContext.TryGetRuntime(out var runtime, out response))
            return false;

        var credential = runtime.Credentials.Current;
        var builder = new StringBuilder()
            .Append("O5Control ").Append(runtime.Version).AppendLine()
            .Append("Linked: ").Append(credential is null ? "no (run o5 setup <code>)" : $"yes ({credential.Instance})").AppendLine()
            .Append("Connection: ").Append(runtime.Client.IsConnected ? "online" : runtime.Client.IsRunning ? "reconnecting" : "stopped").AppendLine()
            .Append("Server: ").Append(runtime.ServerName ?? "-").AppendLine()
            .Append("Language: ").Append(Translation.Name(runtime.Texts.Language)).Append(credential?.Language is null ? " (from the instance)" : " (set in o5 setup)").AppendLine()
            .Append("Active bans cached: ").Append(runtime.Bans.Count).AppendLine()
            .Append("Staff assignments: ").Append(runtime.Staff.Assignments.Count).AppendLine()
            .Append("Auto update: ").Append(runtime.Config.AllowAutoUpdate && runtime.Settings.AutoUpdate ? $"on ({runtime.Settings.UpdateChannel})" : "off");

        if (runtime.Updater.StagedVersion is { } staged)
            builder.AppendLine().Append("Pending update: ").Append(staged).Append(" (applies on restart)");

        response = builder.ToString();
        return true;
    }
}
