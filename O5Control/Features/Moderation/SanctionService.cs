using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabApi.Features.Wrappers;
using O5Control.API;
using O5Control.API.Models;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Moderation;

internal sealed class SanctionService
{
    private const int RememberedSanctions = 512;
    private static readonly TimeSpan PermanentThreshold = TimeSpan.FromDays(3650);

    private readonly PanelClient _client;
    private readonly BanCache _bans;
    private readonly MuteTracker _mutes;
    private readonly MainThread _mainThread;
    private readonly Func<Translation> _texts;
    private readonly Func<PluginConfig> _config;
    private readonly HashSet<string> _applied = new();
    private readonly Queue<string> _appliedOrder = new();

    public SanctionService(PanelClient client, BanCache bans, MuteTracker mutes, MainThread mainThread, Func<Translation> texts, Func<PluginConfig> config)
    {
        _client = client;
        _bans = bans;
        _mutes = mutes;
        _mainThread = mainThread;
        _texts = texts;
        _config = config;
    }

    public async Task<O5Result<Sanction>> IssueAsync(SanctionRequest request, string source)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return O5Result<Sanction>.Fail(O5ErrorCodes.InvalidArgument, "A user id is required.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return O5Result<Sanction>.Fail(O5ErrorCodes.InvalidArgument, "A reason is required.");

        if (Protection.IsProtected(request.UserId))
            return O5Result<Sanction>.Fail(O5ErrorCodes.Protected, "Northwood staff cannot be sanctioned.");

        if (!_client.IsConnected)
            return O5Result<Sanction>.Fail(O5ErrorCodes.NotConnected, "This server is not connected to O5 Control.");

        try
        {
            var reply = await _client.RequestAsync(Ops.SanctionIssue, ToMessage(request, source)).ConfigureAwait(false);
            var sanction = reply.Read<Sanction>();
            if (sanction is null)
                return O5Result<Sanction>.Fail(O5ErrorCodes.Internal, "The panel returned an empty sanction.");

            _mainThread.Post(() => Apply(sanction));
            return O5Result<Sanction>.Ok(sanction);
        }
        catch (Exception exception)
        {
            return Failure<Sanction>(exception);
        }
    }

    public async Task<O5Result> RevokeAsync(string sanctionId, string? issuerUserId, string? reason)
    {
        if (!_client.IsConnected)
            return O5Result.Fail(O5ErrorCodes.NotConnected, "This server is not connected to O5 Control.");

        try
        {
            var message = new SanctionRevokeMessage { SanctionId = sanctionId, IssuerUserId = issuerUserId, Reason = reason, Source = "api" };
            await _client.RequestAsync(Ops.SanctionRevoke, message).ConfigureAwait(false);
            return O5Result.Ok();
        }
        catch (Exception exception)
        {
            return Failure<object>(exception);
        }
    }

    public async Task<O5Result<IReadOnlyList<Sanction>>> ListAsync(string userId, bool activeOnly)
    {
        if (!_client.IsConnected)
            return O5Result<IReadOnlyList<Sanction>>.Fail(O5ErrorCodes.NotConnected, "This server is not connected to O5 Control.");

        try
        {
            var reply = await _client.RequestAsync(Ops.SanctionList, new SanctionListMessage { UserId = userId, ActiveOnly = activeOnly }).ConfigureAwait(false);
            IReadOnlyList<Sanction> sanctions = reply.Read<SanctionListResult>()?.Sanctions ?? new List<Sanction>();
            return O5Result<IReadOnlyList<Sanction>>.Ok(sanctions);
        }
        catch (Exception exception)
        {
            return Failure<IReadOnlyList<Sanction>>(exception);
        }
    }

    public void Record(SanctionIssueMessage message) => _client.Send(Ops.SanctionRecord, message);

    public void RecordRevoke(SanctionRevokeMessage message) => _client.Send(Ops.SanctionRevoke, message);

    public void Apply(Sanction sanction)
    {
        if (!Remember(sanction.Id))
            return;

        var messages = _texts();
        var durations = _config().Broadcasts;
        Player.TryGet(sanction.UserId, out var player);

        if (Protection.IsProtected(player))
        {
            Log.Info($"Ignored {sanction.Type} {sanction.Id}: {player!.LogName} is Northwood staff.");
            return;
        }

        switch (sanction.Type)
        {
            case SanctionType.Ban:
                _bans.Upsert(sanction);
                player?.Disconnect(Format(messages.BanScreen, sanction, messages));
                break;

            case SanctionType.Kick:
                player?.Disconnect(Format(messages.KickScreen, sanction, messages));
                break;

            case SanctionType.Mute:
                _mutes.Add(sanction);
                if (player is not null)
                    Server.SendBroadcast(player, Format(messages.MuteBroadcast, sanction, messages), durations.Mute);
                break;

            case SanctionType.Warn:
                if (player is not null)
                    Server.SendBroadcast(player, Format(messages.WarnBroadcast, sanction, messages), durations.Warn);
                break;
        }

        O5Events.OnSanctionApplied(sanction);
    }

    public void Revoke(SanctionRevokedMessage revoked)
    {
        _bans.Remove(revoked.SanctionId);

        if (revoked.Type == SanctionType.Mute)
            _mutes.Revoke(revoked.SanctionId, revoked.UserId);

        O5Events.OnSanctionRevoked(revoked.UserId, revoked.Type);
    }

    public static string Format(string template, Sanction sanction, Translation messages)
    {
        var expires = sanction.ExpiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'") ?? messages.PermanentLabel;

        return template
            .Replace("{reason}", sanction.Reason)
            .Replace("{expires}", expires)
            .Replace("{issuer}", sanction.IssuedBy ?? "O5 Control");
    }

    public static long? ToDurationSeconds(TimeSpan? duration) =>
        duration is { } value && value < PermanentThreshold ? (long)value.TotalSeconds : null;

    private static SanctionIssueMessage ToMessage(SanctionRequest request, string source) => new()
    {
        Type = request.Type,
        UserId = request.UserId,
        PlayerName = request.PlayerName,
        Reason = request.Reason,
        DurationSeconds = ToDurationSeconds(request.Duration),
        Scope = request.Scope,
        IssuerUserId = request.Issuer?.UserId,
        IssuerName = request.IssuerName ?? request.Issuer?.Nickname,
        Source = source,
    };

    private bool Remember(string sanctionId)
    {
        if (string.IsNullOrEmpty(sanctionId))
            return true;

        if (!_applied.Add(sanctionId))
            return false;

        _appliedOrder.Enqueue(sanctionId);
        if (_appliedOrder.Count > RememberedSanctions)
            _applied.Remove(_appliedOrder.Dequeue());

        return true;
    }

    private static O5Result<T> Failure<T>(Exception exception) => exception switch
    {
        PanelException panel => O5Result<T>.Fail(panel.Code, panel.Message),
        TimeoutException => O5Result<T>.Fail(O5ErrorCodes.Timeout, "O5 Control did not answer in time."),
        InvalidOperationException => O5Result<T>.Fail(O5ErrorCodes.NotConnected, exception.Message),
        _ => O5Result<T>.Fail(O5ErrorCodes.Internal, exception.GetBaseException().Message),
    };
}
