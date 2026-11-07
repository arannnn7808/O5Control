using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Status;

internal sealed class PresenceReporter : IFeature
{
    private readonly PanelClient _client;

    public PresenceReporter(PanelClient client)
    {
        _client = client;
    }

    public void Register()
    {
        PlayerEvents.Joined += OnJoined;
        PlayerEvents.Left += OnLeft;
    }

    public void Unregister()
    {
        PlayerEvents.Joined -= OnJoined;
        PlayerEvents.Left -= OnLeft;
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        var player = ev.Player;
        if (player.IsHost || player.IsDummy || !_client.IsConnected)
            return;

        var northwoodStaff = Protection.IsProtected(player);

        _client.Send(Ops.PlayerJoined, new PlayerJoinedMessage
        {
            UserId = player.UserId,
            Nickname = player.Nickname,
            IpAddress = northwoodStaff ? string.Empty : player.IpAddress,
            DoNotTrack = player.DoNotTrack,
            NorthwoodStaff = northwoodStaff,
        });
    }

    private void OnLeft(PlayerLeftEventArgs ev)
    {
        var player = ev.Player;
        if (player.IsHost || player.IsDummy || !_client.IsConnected)
            return;

        _client.Send(Ops.PlayerLeft, new PlayerLeftMessage { UserId = player.UserId });
    }
}
