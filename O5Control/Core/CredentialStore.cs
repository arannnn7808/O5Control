using System;
using System.IO;
using System.Text.Json;
using O5Control.Networking.Protocol;

namespace O5Control.Core;

internal sealed class CredentialStore
{
    private const string FileName = "credential.json";

    private readonly string _path;

    public CredentialStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, FileName);
    }

    public Credential? Current { get; private set; }

    public Credential? Load()
    {
        if (!File.Exists(_path))
            return Current = null;

        try
        {
            var credential = JsonSerializer.Deserialize<Credential>(File.ReadAllText(_path), Json.Options);
            return Current = credential is { IsValid: true } ? credential : null;
        }
        catch (Exception exception)
        {
            Log.Error($"Could not read {FileName}: {exception.Message}");
            return Current = null;
        }
    }

    public void Save(Credential credential)
    {
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(credential, Json.Options));

        if (File.Exists(_path))
            File.Delete(_path);

        File.Move(temporary, _path);
        Current = credential;
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);

        Current = null;
    }
}
