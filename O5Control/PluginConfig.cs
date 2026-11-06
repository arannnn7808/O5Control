using System.ComponentModel;

namespace O5Control;

public sealed class PluginConfig
{
    [Description("Enables verbose logging in the server console.")]
    public bool Debug { get; set; }

    [Description("Sends bans issued from Remote Admin to O5 Control so they apply to the whole instance.")]
    public bool SyncRemoteAdminBans { get; set; } = true;

    [Description("Rejects players with an active O5 Control ban before they finish joining.")]
    public bool EnforceBansOnJoin { get; set; } = true;

    [Description("Applies staff groups assigned from the panel and Discord role sync.")]
    public bool SyncStaffGroups { get; set; } = true;

    [Description("Activity sent to the panel. Disable any category you do not want to share.")]
    public ActivityConfig Activity { get; set; } = new();

    [Description("Seconds between status updates (players, TPS, round).")]
    public int StatusIntervalSeconds { get; set; } = 5;

    [Description("Allows the panel to install plugin updates. The panel decides when; this is a local veto.")]
    public bool AllowAutoUpdate { get; set; } = true;

    [Description("Seconds each broadcast shown to players stays on screen. The texts live in translations.yml.")]
    public BroadcastConfig Broadcasts { get; set; } = new();
}

public sealed class BroadcastConfig
{
    public ushort Warn { get; set; } = 8;

    public ushort Mute { get; set; } = 6;

    public ushort StaffNotice { get; set; } = 5;
}

public sealed class ActivityConfig
{
    public bool AdminChat { get; set; } = true;

    public bool Commands { get; set; } = true;

    public bool Kills { get; set; } = true;

    public bool Connections { get; set; } = true;

    public bool Reports { get; set; } = true;

    public bool Rounds { get; set; } = true;

    public bool RoleChanges { get; set; } = true;
}
