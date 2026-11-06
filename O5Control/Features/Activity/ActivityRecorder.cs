using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Enums;
using LabApi.Features.Wrappers;
using O5Control.Core;
using PlayerRoles;

namespace O5Control.Features.Activity;

internal sealed class ActivityRecorder : IFeature
{
    private static readonly HashSet<string> PrivateCommands = new(StringComparer.OrdinalIgnoreCase) { "o5", "o5control" };

    private readonly ActivityBuffer _buffer;
    private readonly Func<ActivityConfig> _config;

    public ActivityRecorder(ActivityBuffer buffer, Func<ActivityConfig> config)
    {
        _buffer = buffer;
        _config = config;
    }

    public void Register()
    {
        ServerEvents.SentAdminChat += OnAdminChat;
        ServerEvents.CommandExecuted += OnCommandExecuted;
        ServerEvents.RoundStarted += OnRoundStarted;
        ServerEvents.RoundEnded += OnRoundEnded;
        PlayerEvents.Death += OnDeath;
        PlayerEvents.Joined += OnJoined;
        PlayerEvents.Left += OnLeft;
        PlayerEvents.ReportedPlayer += OnReportedPlayer;
        PlayerEvents.ReportedCheater += OnReportedCheater;
        PlayerEvents.ChangedRole += OnChangedRole;
    }

    public void Unregister()
    {
        ServerEvents.SentAdminChat -= OnAdminChat;
        ServerEvents.CommandExecuted -= OnCommandExecuted;
        ServerEvents.RoundStarted -= OnRoundStarted;
        ServerEvents.RoundEnded -= OnRoundEnded;
        PlayerEvents.Death -= OnDeath;
        PlayerEvents.Joined -= OnJoined;
        PlayerEvents.Left -= OnLeft;
        PlayerEvents.ReportedPlayer -= OnReportedPlayer;
        PlayerEvents.ReportedCheater -= OnReportedCheater;
        PlayerEvents.ChangedRole -= OnChangedRole;
    }

    public void Custom(string category, string message, Player? actor)
    {
        _buffer.Add(new ActivityEntry
        {
            Type = ActivityTypes.Custom,
            Actor = ActivityActor.From(actor),
            Text = message,
            Data = new Dictionary<string, string> { ["category"] = category },
        });
    }

    private void OnAdminChat(SentAdminChatEventArgs ev)
    {
        if (!_config().AdminChat)
            return;

        _buffer.Add(new ActivityEntry
        {
            Type = ActivityTypes.AdminChat,
            Actor = ActivityActor.From(ev.Sender),
            Text = ev.Message,
        });
    }

    private void OnCommandExecuted(CommandExecutedEventArgs ev)
    {
        if (!_config().Commands || ev.Sender is null || ev.CommandType == CommandType.Client || PrivateCommands.Contains(ev.CommandName))
            return;

        _buffer.Add(new ActivityEntry
        {
            Type = ActivityTypes.Command,
            Actor = ActivityActor.From(ev.Sender),
            Text = ev.Arguments.Count > 0 ? $"{ev.CommandName} {string.Join(" ", ev.Arguments)}" : ev.CommandName,
            Data = new Dictionary<string, string>
            {
                ["source"] = ev.CommandType.ToString(),
                ["success"] = ev.ExecutedSuccessfully ? "true" : "false",
                ["response"] = CleanResponse(ev.Response),
            },
        });
    }

    private static string CleanResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        var plain = Regex.Replace(response, "<[^>]+>", string.Empty).Trim();
        return plain.Length > 300 ? plain.Substring(0, 300) : plain;
    }

    private void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (!_config().RoleChanges || ev.Player.IsHost || ev.Player.IsDummy)
            return;

        if (ev.ChangeReason is RoleChangeReason.Died or RoleChangeReason.Destroyed)
            return;

        var newRole = ev.NewRole.RoleTypeId;
        if (newRole == RoleTypeId.None || newRole == ev.OldRole)
            return;

        _buffer.Add(new ActivityEntry
        {
            Type = ActivityTypes.RoleChange,
            Actor = ActivityActor.From(ev.Player),
            Data = new Dictionary<string, string>
            {
                ["from"] = ev.OldRole.ToString(),
                ["to"] = newRole.ToString(),
                ["reason"] = ev.ChangeReason.ToString(),
            },
        });
    }

    private void OnDeath(PlayerDeathEventArgs ev)
    {
        if (!_config().Kills)
            return;

        var attacker = ev.Attacker;
        var teamkill = attacker is not null && attacker != ev.Player && attacker.Faction == ev.OldRole.GetFaction();

        _buffer.Add(new ActivityEntry
        {
            Type = ActivityTypes.Kill,
            Actor = ActivityActor.From(attacker),
            Target = new ActivityActor
            {
                UserId = ev.Player.DoNotTrack ? null : ev.Player.UserId,
                Name = ev.Player.Nickname,
                Role = ev.OldRole.ToString(),
            },
            Text = ev.DamageHandler.ServerLogsText,
            Data = new Dictionary<string, string> { ["teamkill"] = teamkill ? "true" : "false" },
        });
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        if (_config().Connections)
            _buffer.Add(new ActivityEntry { Type = ActivityTypes.Join, Actor = ActivityActor.From(ev.Player) });
    }

    private void OnLeft(PlayerLeftEventArgs ev)
    {
        if (_config().Connections)
            _buffer.Add(new ActivityEntry { Type = ActivityTypes.Leave, Actor = ActivityActor.From(ev.Player) });
    }

    private void OnReportedPlayer(PlayerReportedPlayerEventArgs ev) => Report(ActivityTypes.Report, ev.Player, ev.Target, ev.Reason);

    private void OnReportedCheater(PlayerReportedCheaterEventArgs ev) => Report(ActivityTypes.CheaterReport, ev.Player, ev.Target, ev.Reason);

    private void Report(string type, Player? reporter, Player? target, string reason)
    {
        if (!_config().Reports)
            return;

        _buffer.Add(new ActivityEntry
        {
            Type = type,
            Actor = ActivityActor.From(reporter),
            Target = ActivityActor.From(target),
            Text = reason,
        });
    }

    private void OnRoundStarted()
    {
        if (_config().Rounds)
            _buffer.Add(new ActivityEntry { Type = ActivityTypes.RoundStart, Data = new Dictionary<string, string> { ["players"] = Player.Count.ToString() } });
    }

    private void OnRoundEnded(RoundEndedEventArgs ev)
    {
        if (_config().Rounds)
            _buffer.Add(new ActivityEntry { Type = ActivityTypes.RoundEnd, Data = new Dictionary<string, string> { ["winner"] = ev.LeadingTeam.ToString() } });
    }
}
