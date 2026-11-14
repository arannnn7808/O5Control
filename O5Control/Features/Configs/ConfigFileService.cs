using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LabApi.Features.Wrappers;
using LabApi.Loader;
using LabApi.Loader.Features.Paths;
using O5Control.Core;
using O5Control.Features.Staff;
using O5Control.Networking;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;
using RemoteAdmin;

namespace O5Control.Features.Configs;

internal sealed class ConfigFileService
{
    private const int MaxBytes = 256 * 1024;

    private static readonly UTF8Encoding Utf8 = new(false);
    private const int MaxDepth = 3;
    private const int MaxFilesPerFolder = 200;

    private static readonly string[] PluginExtensions = { ".yml", ".yaml", ".json", ".txt" };
    private static readonly string[] PreferredNames = { "config.yml", "translations.yml" };

    private readonly PanelClient _client;
    private readonly StaffSyncService _staff;
    private readonly string _hidden;

    public ConfigFileService(PanelClient client, StaffSyncService staff, string hiddenFile)
    {
        _client = client;
        _staff = staff;
        _hidden = Path.GetFullPath(hiddenFile);
    }

    public void Bind(MessageRouter router)
    {
        router.On(Ops.ConfigList, request => _client.Reply(request, Ops.ConfigList, List()));
        router.On<ConfigReadMessage>(Ops.ConfigRead, (message, request) => _client.Reply(request, Ops.ConfigRead, Read(message)));
        router.On<ConfigWriteMessage>(Ops.ConfigWrite, (message, request) => _client.Reply(request, Ops.ConfigWrite, Write(message)));
    }

    private ConfigListMessage List()
    {
        var message = new ConfigListMessage();

        foreach (var entry in Discover())
        {
            try
            {
                var info = new FileInfo(entry.Path);
                message.Files.Add(new ConfigFileInfo
                {
                    Id = entry.Id,
                    Kind = entry.Kind,
                    Owner = entry.Owner,
                    Name = entry.Name,
                    Scope = entry.Scope,
                    Size = info.Length,
                    ModifiedAt = info.LastWriteTimeUtc,
                });
            }
            catch (Exception exception)
            {
                Log.Debug($"Skipped {entry.Path}: {exception.Message}");
            }
        }

        return message;
    }

    private ConfigFileMessage Read(ConfigReadMessage message)
    {
        var entry = Find(message.Id);
        if (entry is null)
            return new ConfigFileMessage { Id = message.Id, Error = "not_found" };

        try
        {
            if (new FileInfo(entry.Path).Length > MaxBytes)
                return new ConfigFileMessage { Id = message.Id, Error = "too_large" };

            var content = File.ReadAllText(entry.Path, Utf8);
            return new ConfigFileMessage { Id = message.Id, Content = content, Hash = Hash(content) };
        }
        catch (Exception exception)
        {
            Log.Warn($"Could not read {entry.Path}: {exception.Message}");
            return new ConfigFileMessage { Id = message.Id, Error = "io_error" };
        }
    }

    private ConfigWriteResultMessage Write(ConfigWriteMessage message)
    {
        var entry = Find(message.Id);
        if (entry is null)
            return new ConfigWriteResultMessage { Error = "not_found" };

        if (message.Content is null)
            return new ConfigWriteResultMessage { Error = "invalid" };

        var bytes = Utf8.GetBytes(message.Content);
        if (bytes.Length > MaxBytes)
            return new ConfigWriteResultMessage { Error = "too_large" };

        try
        {
            if (!string.IsNullOrEmpty(message.Hash) && message.Hash != Hash(File.ReadAllText(entry.Path, Utf8)))
                return new ConfigWriteResultMessage { Error = "conflict" };

            var temporary = entry.Path + ".o5tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Copy(temporary, entry.Path, true);
            File.Delete(temporary);
        }
        catch (Exception exception)
        {
            Log.Warn($"Could not write {entry.Path}: {exception.Message}");
            return new ConfigWriteResultMessage { Error = "io_error" };
        }

        Log.Info($"{entry.Owner}/{entry.Name} edited from O5 Control by {message.Issuer}.");
        var reloaded = entry.Kind == "game" && ReloadGameConfigs();
        return new ConfigWriteResultMessage { Hash = Hash(message.Content), Reloaded = reloaded };
    }

    private bool ReloadGameConfigs()
    {
        try
        {
            if (!CommandProcessor.RemoteAdminCommandHandler.TryGetCommand("reloadconfig", out var command))
                return false;

            var success = command.Execute(new ArraySegment<string>(Array.Empty<string>()), ServerConsole.Scs, out var response);
            Log.Debug($"reloadconfig: {response}");
            _staff.Reapply();
            return success;
        }
        catch (Exception exception)
        {
            Log.Warn($"Could not reload the game configs: {exception.Message}");
            return false;
        }
    }

    private Entry? Find(string id) => string.IsNullOrEmpty(id) ? null : Discover().FirstOrDefault(entry => entry.Id == id);

    private List<Entry> Discover()
    {
        var entries = new List<Entry>();

        AddGame(entries, "gameplay", PathManager.GameplayConfigPath);
        AddGame(entries, "remoteadmin", PathManager.RAConfigPath);
        AddGame(entries, "reservedslots", PathManager.ReservedSlotsConfigPath);
        AddGame(entries, "whitelist", PathManager.WhitelistConfigPath);
        AddGame(entries, "sharing", PathManager.SharingConfigPath);
        AddGame(entries, "userbans", PathManager.UserIdBansPath);
        AddGame(entries, "ipbans", PathManager.IpBansPath);
        AddGameFolder(entries, PathManager.GameplayConfigPath);
        AddGameFolder(entries, PathManager.RAConfigPath);

        var root = PathManager.Configs.FullName;
        var port = Server.Port.ToString();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var plugins = PluginLoader.Plugins.Keys
            .OrderBy(plugin => plugin is O5ControlPlugin ? 0 : 1)
            .ThenBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in plugins)
        {
            var name = plugin.Name;
            if (!IsSafeName(name) || !seen.Add(name))
                continue;

            AddPlugin(entries, name, "port", Path.Combine(Path.Combine(root, port), name));
            AddPlugin(entries, name, "global", Path.Combine(Path.Combine(root, "global"), name));
        }

        return entries;
    }

    private static void AddGame(List<Entry> entries, string key, string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || entries.Any(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)))
            return;

        entries.Add(new Entry($"game/{key}", "game", "SCP: Secret Laboratory", Path.GetFileName(path), null, path!));
    }

    private static void AddGameFolder(List<Entry> entries, string? anyFile)
    {
        var directory = string.IsNullOrEmpty(anyFile) ? null : Path.GetDirectoryName(anyFile);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        try
        {
            foreach (var file in Directory.GetFiles(directory!, "*.txt").OrderBy(file => file, StringComparer.OrdinalIgnoreCase).Take(MaxFilesPerFolder))
                AddGame(entries, $"file/{Path.GetFileName(file)}", file);
        }
        catch (Exception exception)
        {
            Log.Debug($"Skipped {directory}: {exception.Message}");
        }
    }

    private void AddPlugin(List<Entry> entries, string plugin, string scope, string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var root = Path.GetFullPath(directory);
        var files = new List<string>();
        Collect(root, 0, files);

        var ordered = files
            .Select(file => file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(name => name.Contains('/') ? 1 : 0)
            .ThenBy(Rank)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in ordered)
            entries.Add(new Entry($"plugin/{scope}/{plugin}/{name}", "plugin", plugin, name, scope, Path.Combine(root, name)));
    }

    private void Collect(string directory, int depth, List<string> files)
    {
        try
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                if (files.Count >= MaxFilesPerFolder)
                    return;

                if (!PluginExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(Path.GetFullPath(file), _hidden, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (new FileInfo(file).Length > MaxBytes)
                    continue;

                files.Add(file);
            }

            if (depth >= MaxDepth)
                return;

            foreach (var child in Directory.GetDirectories(directory).OrderBy(child => child, StringComparer.OrdinalIgnoreCase))
                Collect(child, depth + 1, files);
        }
        catch (Exception exception)
        {
            Log.Debug($"Skipped {directory}: {exception.Message}");
        }
    }

    private static int Rank(string name)
    {
        var index = Array.FindIndex(PreferredNames, preferred => string.Equals(preferred, name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? PreferredNames.Length : index;
    }

    private static bool IsSafeName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." && name!.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string Hash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Utf8.GetBytes(content));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private sealed class Entry
    {
        public Entry(string id, string kind, string owner, string name, string? scope, string path)
        {
            Id = id;
            Kind = kind;
            Owner = owner;
            Name = name;
            Scope = scope;
            Path = path;
        }

        public string Id { get; }

        public string Kind { get; }

        public string Owner { get; }

        public string Name { get; }

        public string? Scope { get; }

        public string Path { get; }
    }
}
