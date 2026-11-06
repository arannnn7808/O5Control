using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using O5Control.API;
using O5Control.API.Models;

namespace O5Control.Example;

public sealed class ExamplePlugin : Plugin
{
    public override string Name => "O5Control.Example";

    public override string Description => "Shows how to use the O5 Control API from another plugin.";

    public override string Author => "O5 Control";

    public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);

    public override LoadPriority Priority => LoadPriority.Low;

    public override void Enable()
    {
        O5Events.Connected += OnConnected;
        O5Events.SanctionApplied += OnSanctionApplied;
        PlayerEvents.Joined += OnJoined;
    }

    public override void Disable()
    {
        O5Events.Connected -= OnConnected;
        O5Events.SanctionApplied -= OnSanctionApplied;
        PlayerEvents.Joined -= OnJoined;
    }

    private static void OnConnected() => Logger.Info($"O5 Control is online for {O5.InstanceName}.");

    private static void OnSanctionApplied(Sanction sanction) =>
        Logger.Info($"{sanction.Type} applied to {sanction.PlayerName ?? sanction.UserId}: {sanction.Reason}");

    private static async void OnJoined(PlayerJoinedEventArgs ev)
    {
        var player = ev.Player;
        if (!O5.IsConnected)
            return;

        var profile = await O5.GetPlayerAsync(player.UserId);
        if (profile.Success && profile.Value!.IsLinked)
            Server.SendBroadcast(player, $"Welcome back, {profile.Value.DiscordName}!", 5);
    }
}
