using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using O5Control.API.Models;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Moderation;

internal sealed class RemoteAdminBridge : IFeature
{
    private const string Source = "remote_admin";

    private readonly SanctionService _sanctions;
    private readonly PanelClient _client;
    private readonly MainThread _mainThread;
    private readonly Func<PluginConfig> _config;
    private readonly Func<Translation> _texts;

    public RemoteAdminBridge(SanctionService sanctions, PanelClient client, MainThread mainThread, Func<PluginConfig> config, Func<Translation> texts)
    {
        _sanctions = sanctions;
        _client = client;
        _mainThread = mainThread;
        _config = config;
        _texts = texts;
    }

    public void Register()
    {
        PlayerEvents.Banning += OnBanning;
        ServerEvents.BanIssuing += OnBanIssuing;
        PlayerEvents.Kicked += OnKicked;
        PlayerEvents.Muted += OnMuted;
        PlayerEvents.Unmuted += OnUnmuted;
    }

    public void Unregister()
    {
        PlayerEvents.Banning -= OnBanning;
        ServerEvents.BanIssuing -= OnBanIssuing;
        PlayerEvents.Kicked -= OnKicked;
        PlayerEvents.Muted -= OnMuted;
        PlayerEvents.Unmuted -= OnUnmuted;
    }

    private void OnBanning(PlayerBanningEventArgs ev)
    {
        if (!ev.IsAllowed || !_config().SyncRemoteAdminBans || ev.Duration <= 0)
            return;

        var message = new SanctionIssueMessage
        {
            Type = SanctionType.Ban,
            UserId = ev.PlayerId,
            PlayerName = ev.Player?.Nickname,
            Reason = string.IsNullOrWhiteSpace(ev.Reason) ? "No reason given" : ev.Reason,
            DurationSeconds = SanctionService.ToDurationSeconds(TimeSpan.FromSeconds(ev.Duration)),
            IssuerUserId = RealIssuer(ev.Issuer)?.UserId,
            IssuerName = RealIssuer(ev.Issuer)?.Nickname ?? "Server console",
            Source = Source,
        };

        if (!_client.IsConnected)
        {
            _sanctions.Record(message);
            return;
        }

        ev.IsAllowed = false;
        Forward(message, ev.Issuer);
    }

    private void OnBanIssuing(BanIssuingEventArgs ev)
    {
        if (!_config().SyncRemoteAdminBans || ev.BanType != BanHandler.BanType.UserId || !_client.IsConnected)
            return;

        var details = ev.BanDetails;
        var remaining = TimeSpan.FromTicks(Math.Max(0, details.Expires - DateTime.UtcNow.Ticks));

        ev.IsAllowed = false;
        Forward(new SanctionIssueMessage
        {
            Type = SanctionType.Ban,
            UserId = details.Id,
            PlayerName = details.OriginalName,
            Reason = string.IsNullOrWhiteSpace(details.Reason) ? "No reason given" : details.Reason,
            DurationSeconds = SanctionService.ToDurationSeconds(remaining),
            IssuerName = details.Issuer,
            Source = Source,
        }, null);
    }

    private void OnKicked(PlayerKickedEventArgs ev)
    {
        _sanctions.Record(new SanctionIssueMessage
        {
            Type = SanctionType.Kick,
            UserId = ev.Player.UserId,
            PlayerName = ev.Player.Nickname,
            Reason = string.IsNullOrWhiteSpace(ev.Reason) ? "No reason given" : ev.Reason,
            IssuerUserId = RealIssuer(ev.Issuer)?.UserId,
            IssuerName = RealIssuer(ev.Issuer)?.Nickname ?? "Server console",
            Source = Source,
        });
    }

    private void OnMuted(PlayerMutedEventArgs ev)
    {
        if (ev.IsIntercom)
            return;

        _sanctions.Record(new SanctionIssueMessage
        {
            Type = SanctionType.Mute,
            UserId = ev.Player.UserId,
            PlayerName = ev.Player.Nickname,
            Reason = "Muted from Remote Admin",
            IssuerUserId = RealIssuer(ev.Issuer)?.UserId,
            IssuerName = RealIssuer(ev.Issuer)?.Nickname ?? "Server console",
            Source = Source,
        });
    }

    private void OnUnmuted(PlayerUnmutedEventArgs ev)
    {
        if (ev.IsIntercom)
            return;

        _sanctions.RecordRevoke(new SanctionRevokeMessage
        {
            UserId = ev.Player.UserId,
            Type = SanctionType.Mute,
            IssuerUserId = RealIssuer(ev.Issuer)?.UserId,
            Source = Source,
        });
    }

    private static Player? RealIssuer(Player? issuer) => issuer is { IsHost: false } ? issuer : null;

    private async void Forward(SanctionIssueMessage message, Player? issuer)
    {
        var request = new SanctionRequest(message.Type, message.UserId, message.Reason)
        {
            PlayerName = message.PlayerName,
            Duration = message.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Issuer = issuer is { IsHost: false } ? issuer : null,
            IssuerName = message.IssuerName,
        };

        var result = await _sanctions.IssueAsync(request, Source);

        _mainThread.Post(() =>
        {
            if (result.Success)
            {
                if (issuer is not null)
                    Server.SendBroadcast(issuer, _texts().RemoteAdminBanSynced, _config().Broadcasts.StaffNotice);
                return;
            }

            Log.Warn($"Could not sync the ban of {message.UserId} ({result}). Applying it locally and recording it for later.");
            _sanctions.Apply(new Sanction
            {
                Id = $"local-{Guid.NewGuid():N}",
                Type = message.Type,
                UserId = message.UserId,
                PlayerName = message.PlayerName,
                Reason = message.Reason,
                IssuedBy = message.IssuerName,
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = message.DurationSeconds is { } remaining ? DateTimeOffset.UtcNow.AddSeconds(remaining) : null,
                Scope = SanctionScope.Instance,
            });
            _sanctions.Record(message);
        });
    }
}
