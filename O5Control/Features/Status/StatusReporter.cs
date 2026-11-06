using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using MEC;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Status;

internal sealed class StatusReporter : IFeature
{
    private readonly PanelClient _client;
    private readonly Func<PluginConfig> _config;
    private CoroutineHandle _handle;
    private bool _running;

    public StatusReporter(PanelClient client, Func<PluginConfig> config)
    {
        _client = client;
        _config = config;
    }

    public void Register()
    {
        _running = true;
        _handle = Timing.RunCoroutine(Loop(), Segment.Update);
    }

    public void Unregister()
    {
        _running = false;
        Timing.KillCoroutines(_handle);
    }

    public void SendNow()
    {
        if (!_client.IsConnected || !IsServerReady())
            return;

        try
        {
            _client.Send(Ops.Status, Snapshot());
        }
        catch (Exception exception)
        {
            Log.Debug($"Status snapshot skipped: {exception.Message}");
        }
    }

    private IEnumerator<float> Loop()
    {
        while (_running)
        {
            yield return Timing.WaitForSeconds(Math.Max(2, _config().StatusIntervalSeconds));
            SendNow();
        }
    }

    private static bool IsServerReady() => ReferenceHub.TryGetHostHub(out var host) && host != null;

    private static T Read<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    private static StatusMessage Snapshot()
    {
        var players = Read(OnlinePlayers, new List<OnlinePlayer>());

        return new StatusMessage
        {
            PlayerCount = players.Count,
            MaxPlayers = Read(() => Server.MaxPlayers, 0),
            Tps = Read(() => Math.Round(Server.Tps, 1), 0),
            RoundStarted = Read(() => Round.IsRoundStarted, false),
            RoundEnded = Read(() => Round.IsRoundEnded, false),
            RoundSeconds = Read(() => (int)Round.Duration.TotalSeconds, 0),
            Players = players,
        };
    }

    private static List<OnlinePlayer> OnlinePlayers() => Player.ReadyList
        .Where(player => player is not null && !player.IsHost && !player.IsDummy)
        .Select(player => new OnlinePlayer
        {
            UserId = player.UserId,
            Nickname = player.Nickname,
            PlayerId = player.PlayerId,
            Role = player.Role.ToString(),
            Team = player.Team.ToString(),
            Group = player.GroupName,
        })
        .ToList();
}
