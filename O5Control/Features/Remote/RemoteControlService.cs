using LabApi.Features.Wrappers;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Remote;

internal sealed class RemoteControlService
{
    private readonly PanelClient _client;

    public RemoteControlService(PanelClient client)
    {
        _client = client;
    }

    public void Bind(MessageRouter router)
    {
        router.On<CommandRunMessage>(Ops.CommandRun, RunCommand);
        router.On<ServerBroadcastMessage>(Ops.Broadcast, (message, _) => Server.SendBroadcast(message.Text, message.Duration, shouldClearPrevious: message.ClearPrevious));
        router.On<PlayerKickMessage>(Ops.PlayerKick, (message, _) => Kick(message));
        router.On<PlayerMessageMessage>(Ops.PlayerMessage, (message, _) => Message(message));
        router.On(Ops.RoundRestart, _ => Round.Restart());
    }

    private void RunCommand(CommandRunMessage message, Envelope request)
    {
        if (string.IsNullOrWhiteSpace(message.Command))
            return;

        Log.Info($"Panel command from {message.Issuer}: {message.Command}");
        var output = Server.RunCommand(message.Command);
        _client.Reply(request, Ops.CommandRun, new CommandResultMessage { Output = output });
    }

    private static void Kick(PlayerKickMessage message)
    {
        if (Player.TryGet(message.UserId, out var player) && !Protection.IsProtected(player))
            player.Disconnect(message.Reason);
    }

    private static void Message(PlayerMessageMessage message)
    {
        if (Player.TryGet(message.UserId, out var player))
            Server.SendBroadcast(player, message.Text, message.Duration);
    }
}
