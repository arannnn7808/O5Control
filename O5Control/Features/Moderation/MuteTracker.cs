using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MEC;
using O5Control.API.Models;
using O5Control.Core;
using VoiceChat;

namespace O5Control.Features.Moderation;

internal sealed class MuteTracker : IFeature
{
    private const float CheckIntervalSeconds = 5f;

    private readonly Dictionary<string, Sanction> _byUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<Translation> _texts;
    private readonly Func<PluginConfig> _config;
    private CoroutineHandle _handle;
    private bool _running;

    public MuteTracker(Func<Translation> texts, Func<PluginConfig> config)
    {
        _texts = texts;
        _config = config;
    }

    public int Count => _byUser.Count;

    public void Register()
    {
        PlayerEvents.Joined += OnJoined;
        _running = true;
        _handle = Timing.RunCoroutine(ExpiryLoop(), Segment.Update);
    }

    public void Unregister()
    {
        PlayerEvents.Joined -= OnJoined;
        _running = false;
        Timing.KillCoroutines(_handle);
    }

    public void Replace(IEnumerable<Sanction> mutes)
    {
        var previous = _byUser.Keys.ToList();
        _byUser.Clear();

        foreach (var mute in mutes)
            Store(mute);

        foreach (var userId in previous.Where(userId => !_byUser.ContainsKey(userId)))
            Unmute(userId, false);

        foreach (var userId in _byUser.Keys)
            Mute(userId);
    }

    public void Add(Sanction mute)
    {
        if (Store(mute))
            Mute(mute.UserId);
    }

    public void Revoke(string sanctionId, string userId)
    {
        if (_byUser.TryGetValue(userId, out var current) && current.Id != sanctionId && current.IsActive)
            return;

        _byUser.Remove(userId);
        Unmute(userId, false);
    }

    public bool IsMuted(string userId) => _byUser.TryGetValue(userId, out var mute) && mute.IsActive;

    private bool Store(Sanction mute)
    {
        if (mute.Type != SanctionType.Mute || !mute.IsActive || string.IsNullOrEmpty(mute.UserId))
            return false;

        if (_byUser.TryGetValue(mute.UserId, out var existing) && existing.IsActive && !Outlasts(mute, existing))
            return true;

        _byUser[mute.UserId] = mute;
        return true;
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        if (ev.Player.IsHost || ev.Player.IsDummy)
            return;

        if (IsMuted(ev.Player.UserId))
        {
            SetMuted(ev.Player, true);
            return;
        }

        if (_byUser.Remove(ev.Player.UserId))
            Unmute(ev.Player.UserId, false);
    }

    private IEnumerator<float> ExpiryLoop()
    {
        while (_running)
        {
            yield return Timing.WaitForSeconds(CheckIntervalSeconds);

            var expired = _byUser.Values.Where(mute => !mute.IsActive).Select(mute => mute.UserId).ToList();
            foreach (var userId in expired)
            {
                _byUser.Remove(userId);
                Unmute(userId, true);
            }
        }
    }

    private static void Mute(string userId)
    {
        if (Player.TryGet(userId, out var player) && !Protection.IsProtected(player))
            SetMuted(player, true);
    }

    private void Unmute(string userId, bool notify)
    {
        if (VoiceChatMutes.QueryLocalMute(userId))
            VoiceChatMutes.RevokeLocalMute(userId);

        if (!Player.TryGet(userId, out var player))
            return;

        SetMuted(player, false);

        var message = _texts().MuteExpiredBroadcast;
        if (notify && !string.IsNullOrWhiteSpace(message))
            Server.SendBroadcast(player, message, _config().Broadcasts.Mute);
    }

    private static void SetMuted(Player player, bool muted)
    {
        var flags = VoiceChatMutes.GetFlags(player.ReferenceHub);
        var updated = muted ? flags | VcMuteFlags.LocalRegular : flags & ~VcMuteFlags.LocalRegular;
        if (updated != flags)
            VoiceChatMutes.SetFlags(player.ReferenceHub, updated);
    }

    private static bool Outlasts(Sanction candidate, Sanction existing) =>
        candidate.ExpiresAt is null || (existing.ExpiresAt is not null && candidate.ExpiresAt > existing.ExpiresAt);
}
