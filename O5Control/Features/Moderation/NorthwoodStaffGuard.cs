using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using O5Control.Core;

namespace O5Control.Features.Moderation;

internal sealed class NorthwoodStaffGuard : IFeature
{
    private readonly Func<Translation> _texts;
    private readonly Func<PluginConfig> _config;

    public NorthwoodStaffGuard(Func<Translation> texts, Func<PluginConfig> config)
    {
        _texts = texts;
        _config = config;
    }

    public void Register()
    {
        PlayerEvents.Banning += OnBanning;
        PlayerEvents.Kicking += OnKicking;
        PlayerEvents.Muting += OnMuting;
    }

    public void Unregister()
    {
        PlayerEvents.Banning -= OnBanning;
        PlayerEvents.Kicking -= OnKicking;
        PlayerEvents.Muting -= OnMuting;
    }

    private void OnBanning(PlayerBanningEventArgs ev)
    {
        if (!Protection.IsProtected(ev.Player))
            return;

        ev.IsAllowed = false;
        Deny(ev.Issuer, ev.Player!);
    }

    private void OnKicking(PlayerKickingEventArgs ev)
    {
        if (!Protection.IsProtected(ev.Player))
            return;

        ev.IsAllowed = false;
        Deny(ev.Issuer, ev.Player);
    }

    private void OnMuting(PlayerMutingEventArgs ev)
    {
        if (!Protection.IsProtected(ev.Player))
            return;

        ev.IsAllowed = false;
        Deny(ev.Issuer, ev.Player);
    }

    private void Deny(Player? issuer, Player target)
    {
        Log.Info($"Blocked a sanction against Northwood staff member {target.LogName}.");

        if (issuer is { IsHost: false })
            Server.SendBroadcast(issuer, _texts().NorthwoodStaffProtected, _config().Broadcasts.StaffNotice);
    }
}
