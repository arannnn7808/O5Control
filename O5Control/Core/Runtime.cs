using System;
using System.Collections.Generic;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader;
using O5Control.API;
using O5Control.API.Models;
using O5Control.Features.Activity;
using O5Control.Features.Moderation;
using O5Control.Features.Remote;
using O5Control.Features.Setup;
using O5Control.Features.Staff;
using O5Control.Features.Status;
using O5Control.Features.Updates;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Core;

internal sealed class Runtime : IDisposable
{
    private readonly O5ControlPlugin _plugin;
    private readonly List<IFeature> _features = new();

    public Runtime(O5ControlPlugin plugin)
    {
        _plugin = plugin;
        Version = plugin.Version.ToString(3);

        MainThread = new MainThread();
        Router = new MessageRouter();
        Credentials = new CredentialStore(plugin.GetConfigDirectory().FullName);
        Client = new PanelClient(() => new Uri(Endpoints.Gateway), Credentials, MainThread, Router, Hello, $"O5Control/{Version}");

        Bans = new BanCache();
        Mutes = new MuteTracker(() => Texts, () => Config);
        Sanctions = new SanctionService(Client, Bans, Mutes, MainThread, () => Texts, () => Config);
        Staff = new StaffSyncService(() => Config);
        Activity = new ActivityBuffer(Client);
        Recorder = new ActivityRecorder(Activity, () => Config.Activity);
        Status = new StatusReporter(Client, () => Config);
        Remote = new RemoteControlService(Client);
        Updater = new AutoUpdater(Client, MainThread, () => Config, () => Settings.AutoUpdate, plugin.FilePath, plugin.Version);
        Setup = new SetupService(() => Endpoints.Api, Credentials, Version);

        _features.Add(new NorthwoodStaffGuard(() => Texts, () => Config));
        _features.Add(new JoinGuard(Bans, () => Config, () => Texts));
        _features.Add(Mutes);
        _features.Add(new RemoteAdminBridge(Sanctions, Client, MainThread, () => Config, () => Texts));
        _features.Add(Staff);
        _features.Add(Activity);
        _features.Add(Recorder);
        _features.Add(Status);
        _features.Add(new PresenceReporter(Client));
    }

    public string Version { get; }

    public PluginConfig Config => _plugin.Config;

    public Translation Texts => _plugin.Translation;

    public MainThread MainThread { get; }

    public MessageRouter Router { get; }

    public CredentialStore Credentials { get; }

    public PanelClient Client { get; }

    public BanCache Bans { get; }

    public MuteTracker Mutes { get; }

    public SanctionService Sanctions { get; }

    public StaffSyncService Staff { get; }

    public ActivityBuffer Activity { get; }

    public ActivityRecorder Recorder { get; }

    public StatusReporter Status { get; }

    public RemoteControlService Remote { get; }

    public AutoUpdater Updater { get; }

    public SetupService Setup { get; }

    public ServerSettings Settings { get; private set; } = new();

    public InstanceInfo? Instance { get; private set; }

    public string? ServerName { get; private set; }

    public void Start()
    {
        Log.DebugEnabled = Config.Debug;
        MainThread.Start();

        BindProtocol();
        Remote.Bind(Router);
        Updater.Bind(Router);

        foreach (var feature in _features)
            feature.Register();

        Client.Connected += OnConnected;
        Client.Disconnected += OnDisconnected;
        Client.Unauthorized += OnUnauthorized;

        if (Credentials.Load() is null)
        {
            Log.Warn("This server is not linked to O5 Control yet. Create a code in the panel and run: o5 setup <code>");
            return;
        }

        Client.Start();
    }

    public void Relink()
    {
        Credentials.Load();
        Client.Restart();
    }

    public void Dispose()
    {
        Client.Connected -= OnConnected;
        Client.Disconnected -= OnDisconnected;
        Client.Unauthorized -= OnUnauthorized;

        foreach (var feature in _features)
            feature.Unregister();

        Client.Dispose();
        MainThread.Dispose();
    }

    private void BindProtocol()
    {
        Router.On<WelcomeMessage>(Ops.Welcome, (welcome, _) => OnWelcome(welcome));
        Router.On<ServerSettings>(Ops.SettingsUpdate, (settings, _) => Settings = settings);
        Router.On<BansSnapshotMessage>(Ops.BansSnapshot, (snapshot, _) => Bans.Replace(snapshot.Bans));
        Router.On<Sanction>(Ops.SanctionApplied, (sanction, _) => Sanctions.Apply(sanction));
        Router.On<SanctionRevokedMessage>(Ops.SanctionRevoked, (revoked, _) => Sanctions.Revoke(revoked));
        Router.On<StaffSnapshotMessage>(Ops.StaffSnapshot, (snapshot, _) => Staff.ReplaceAll(snapshot.Staff));
        Router.On<StaffAssignment>(Ops.StaffAssign, (assignment, _) => Staff.Assign(assignment));
        Router.On<StaffUnassignMessage>(Ops.StaffUnassign, (message, _) => Staff.Unassign(message.UserId));
        Router.On(Ops.StaffExport, request => Client.Reply(request, Ops.StaffExport, RemoteAdminExport.Read()));
        Router.On<ErrorMessage>(Ops.Error, (error, _) => Log.Warn($"O5 Control: {error.Message} ({error.Code})"));
    }

    private void OnWelcome(WelcomeMessage welcome)
    {
        Instance = welcome.Instance;
        ServerName = welcome.ServerName;
        Settings = welcome.Settings;
        Bans.Replace(welcome.Bans);
        Mutes.Replace(welcome.Mutes);
        Staff.ReplaceAll(welcome.Staff);

        Log.Info($"Linked to {welcome.Instance.Name} ({welcome.Instance.Slug}) as '{welcome.ServerName}'. {Bans.Count} active bans, {Mutes.Count} mutes, {welcome.Staff.Count} staff.");
        Status.SendNow();
    }

    private void OnConnected()
    {
        Log.Info("Connected to O5 Control.");
        O5Events.OnConnected();
    }

    private void OnDisconnected(string reason)
    {
        Log.Warn($"Disconnected from O5 Control: {reason}. Reconnecting automatically.");
        O5Events.OnDisconnected(reason);
    }

    private void OnUnauthorized()
    {
        Log.Error("O5 Control rejected this server's credential. Link it again with: o5 setup <code>");
        O5Events.OnDisconnected("unauthorized");
    }

    private object Hello() => new HelloMessage
    {
        PluginVersion = Version,
        GameVersion = GameCore.Version.VersionString,
        LabApiVersion = LabApiProperties.CompiledVersion,
        Port = Server.Port,
        MaxPlayers = Server.MaxPlayers,
    };
}
