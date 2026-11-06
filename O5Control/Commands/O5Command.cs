using System;
using System.Linq;
using System.Text;
using CommandSystem;
using O5Control.Commands.Subcommands;

namespace O5Control.Commands;

[CommandHandler(typeof(GameConsoleCommandHandler))]
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class O5Command : ParentCommand
{
    public O5Command() => LoadGeneratedCommands();

    public override string Command => "o5";

    public override string[] Aliases { get; } = { "o5control" };

    public override string Description => "Manage the link between this server and O5 Control.";

    public override void LoadGeneratedCommands()
    {
        RegisterCommand(new SetupCommand());
        RegisterCommand(new StatusCommand());
        RegisterCommand(new ReconnectCommand());
        RegisterCommand(new UnlinkCommand());
        RegisterCommand(new LangCommand());
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var builder = new StringBuilder("O5 Control commands:");

        foreach (var command in AllCommands.OrderBy(command => command.Command))
            builder.Append("\n  o5 ").Append(command.Command.PadRight(10)).Append(command.Description);

        response = builder.ToString();
        return true;
    }
}
