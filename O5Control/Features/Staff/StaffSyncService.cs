using System;
using System.Collections.Generic;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using O5Control.API;
using O5Control.API.Models;
using O5Control.Core;

namespace O5Control.Features.Staff;

internal sealed class StaffSyncService : IFeature
{
    private readonly Dictionary<string, StaffAssignment> _assignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedByUs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<PluginConfig> _config;

    public StaffSyncService(Func<PluginConfig> config)
    {
        _config = config;
    }

    public IReadOnlyDictionary<string, StaffAssignment> Assignments => _assignments;

    public void Register() => PlayerEvents.Joined += OnJoined;

    public void Unregister() => PlayerEvents.Joined -= OnJoined;

    public bool TryGet(string userId, out StaffAssignment? assignment) => _assignments.TryGetValue(userId, out assignment);

    public void ReplaceAll(IEnumerable<StaffAssignment> assignments)
    {
        var incoming = new Dictionary<string, StaffAssignment>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in assignments)
            incoming[assignment.UserId] = assignment;

        foreach (var userId in new List<string>(_assignments.Keys))
        {
            if (!incoming.ContainsKey(userId))
                Unassign(userId);
        }

        foreach (var assignment in incoming.Values)
            Assign(assignment);
    }

    public void Assign(StaffAssignment assignment)
    {
        if (string.IsNullOrEmpty(assignment.UserId))
            return;

        _assignments[assignment.UserId] = assignment;

        if (Player.TryGet(assignment.UserId, out var player))
            Apply(player, assignment);

        O5Events.OnStaffAssigned(assignment);
    }

    public void Unassign(string userId)
    {
        if (!_assignments.Remove(userId))
            return;

        if (_appliedByUs.Remove(userId) && Player.TryGet(userId, out var player))
            player.UserGroup = ServerStatic.PermissionsHandler.GetUserGroup(userId);

        O5Events.OnStaffUnassigned(userId);
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        if (_assignments.TryGetValue(ev.Player.UserId, out var assignment))
            Apply(ev.Player, assignment);
    }

    private void Apply(Player player, StaffAssignment assignment)
    {
        if (!_config().SyncStaffGroups || player.IsHost || player.IsNorthwoodStaff)
            return;

        player.UserGroup = ToUserGroup(assignment);
        _appliedByUs.Add(player.UserId);
        Log.Debug($"Applied staff role '{assignment.RoleName}' to {player.LogName}.");
    }

    private static UserGroup ToUserGroup(StaffAssignment assignment) => new()
    {
        Name = $"o5:{assignment.RoleId}",
        BadgeText = assignment.BadgeText,
        BadgeColor = string.IsNullOrWhiteSpace(assignment.BadgeColor) ? "default" : assignment.BadgeColor,
        KickPower = assignment.KickPower,
        RequiredKickPower = assignment.RequiredKickPower,
        Cover = assignment.Cover,
        HiddenByDefault = assignment.HiddenByDefault,
        Shared = false,
        Permissions = ToPermissions(assignment.Permissions),
    };

    private static ulong ToPermissions(IEnumerable<string> names)
    {
        ulong permissions = 0;

        foreach (var name in names)
        {
            if (name == "*")
                return ulong.MaxValue;

            if (Enum.TryParse<PlayerPermissions>(name, true, out var permission))
                permissions |= (ulong)permission;
            else
                Log.Debug($"Unknown permission '{name}' ignored.");
        }

        return permissions;
    }
}
