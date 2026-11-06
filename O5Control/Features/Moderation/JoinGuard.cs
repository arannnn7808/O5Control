using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using O5Control.Core;

namespace O5Control.Features.Moderation;

internal sealed class JoinGuard : IFeature
{
    private readonly BanCache _bans;
    private readonly Func<PluginConfig> _config;
    private readonly Func<Translation> _texts;

    public JoinGuard(BanCache bans, Func<PluginConfig> config, Func<Translation> texts)
    {
        _bans = bans;
        _config = config;
        _texts = texts;
    }

    public void Register() => PlayerEvents.PreAuthenticating += OnPreAuthenticating;

    public void Unregister() => PlayerEvents.PreAuthenticating -= OnPreAuthenticating;

    private void OnPreAuthenticating(PlayerPreAuthenticatingEventArgs ev)
    {
        var config = _config();
        if (!config.EnforceBansOnJoin || Protection.IsProtected(ev.Flags) || !_bans.TryGet(ev.UserId, out var ban) || ban is null)
            return;

        var expires = ban.ExpiresAt?.UtcDateTime.Ticks ?? DateTime.MaxValue.Ticks;
        ev.RejectBanned(SanctionService.Format("{reason}", ban, _texts()), expires);
        Log.Debug($"Rejected {ev.UserId}: banned ({ban.Id}).");
    }
}
