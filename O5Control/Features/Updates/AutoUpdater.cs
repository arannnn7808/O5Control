using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LabApi.Features.Wrappers;
using O5Control.Core;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Updates;

internal sealed class AutoUpdater
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly PanelClient _client;
    private readonly MainThread _mainThread;
    private readonly Func<PluginConfig> _config;
    private readonly Func<bool> _panelAllows;
    private readonly string _pluginPath;
    private readonly Version _currentVersion;
    private string? _staged;

    public AutoUpdater(PanelClient client, MainThread mainThread, Func<PluginConfig> config, Func<bool> panelAllows, string pluginPath, Version currentVersion)
    {
        _client = client;
        _mainThread = mainThread;
        _config = config;
        _panelAllows = panelAllows;
        _pluginPath = pluginPath;
        _currentVersion = currentVersion;
    }

    public string? StagedVersion => _staged;

    public void Bind(MessageRouter router) => router.On<UpdateAvailableMessage>(Ops.UpdateAvailable, (message, _) => Offer(message));

    private void Offer(UpdateAvailableMessage update)
    {
        if (!Version.TryParse(update.Version, out var version) || version <= _currentVersion || update.Version == _staged)
            return;

        if (!_config().AllowAutoUpdate || (!_panelAllows() && !update.Mandatory))
        {
            Report(update.Version, "declined", "Automatic updates are disabled on this server.");
            return;
        }

        if (string.IsNullOrEmpty(_pluginPath) || !Uri.TryCreate(update.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            Report(update.Version, "failed", "Invalid update source.");
            return;
        }

        _ = DownloadAsync(update, url);
    }

    private async Task DownloadAsync(UpdateAvailableMessage update, Uri url)
    {
        var temporary = _pluginPath + ".download";

        try
        {
            Report(update.Version, "downloading", null);
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);

            if (update.Size > 0 && bytes.LongLength != update.Size)
                throw new InvalidDataException($"Size mismatch ({bytes.LongLength} != {update.Size}).");

            if (!string.Equals(Sha256(bytes), update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Checksum mismatch.");

            File.WriteAllBytes(temporary, bytes);
            File.Copy(_pluginPath, _pluginPath + ".bak", true);
            File.Delete(_pluginPath);
            File.Move(temporary, _pluginPath);

            _staged = update.Version;
            _mainThread.Post(() => ScheduleRestart(update.Version));
        }
        catch (Exception exception)
        {
            if (File.Exists(temporary))
                File.Delete(temporary);

            Log.Error($"Update to {update.Version} failed: {exception.Message}");
            Report(update.Version, "failed", exception.Message);
        }
    }

    private void ScheduleRestart(string version)
    {
        if (Player.Count == 0)
        {
            Log.Info($"O5Control {version} installed. Restarting now: the server is empty.");
            Report(version, "restarting", null);
            Server.Restart();
            return;
        }

        ServerStatic.StopNextRound = ServerStatic.NextRoundAction.Restart;
        Log.Info($"O5Control {version} installed. The server will restart when the round ends.");
        Report(version, "staged", "Restart scheduled at round end.");
    }

    private void Report(string version, string state, string? detail) =>
        _client.Send(Ops.UpdateState, new UpdateStateMessage { Version = version, State = state, Detail = detail });

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
