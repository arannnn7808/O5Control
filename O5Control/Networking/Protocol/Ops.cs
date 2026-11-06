namespace O5Control.Networking.Protocol;

internal static class Ops
{
    public const string Hello = "hello";
    public const string Welcome = "welcome";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Error = "error";

    public const string Status = "server.status";

    public const string SanctionIssue = "sanction.issue";
    public const string SanctionRevoke = "sanction.revoke";
    public const string SanctionRecord = "sanction.record";
    public const string SanctionList = "sanction.list";
    public const string SanctionApplied = "sanction.applied";
    public const string SanctionRevoked = "sanction.revoked";
    public const string BansSnapshot = "bans.snapshot";

    public const string PlayerJoined = "player.joined";
    public const string PlayerLeft = "player.left";
    public const string PlayerLookup = "player.lookup";
    public const string PlayerKick = "player.kick";
    public const string PlayerMessage = "player.message";

    public const string StaffSnapshot = "staff.snapshot";
    public const string StaffAssign = "staff.assign";
    public const string StaffUnassign = "staff.unassign";
    public const string StaffExport = "staff.export";

    public const string ActivityBatch = "activity.batch";

    public const string CommandRun = "command.run";
    public const string Broadcast = "server.broadcast";
    public const string RoundRestart = "round.restart";

    public const string UpdateAvailable = "update.available";
    public const string UpdateState = "update.state";

    public const string SettingsUpdate = "settings.update";
}
