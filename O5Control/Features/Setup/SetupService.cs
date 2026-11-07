using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LabApi.Features;
using LabApi.Features.Wrappers;
using O5Control.Core;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.Features.Setup;

internal sealed class SetupService
{
    private static readonly Regex CodePattern = new("^O5-[A-Z2-9]{4}-[A-Z2-9]{4}$", RegexOptions.Compiled);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly Func<string> _apiUrl;
    private readonly CredentialStore _store;
    private readonly string _pluginVersion;

    public SetupService(Func<string> apiUrl, CredentialStore store, string pluginVersion)
    {
        _apiUrl = apiUrl;
        _store = store;
        _pluginVersion = pluginVersion;
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsValidCode(string code) => CodePattern.IsMatch(code);

    public async Task<SetupResult> RedeemAsync(string code, string? language)
    {
        var body = JsonSerializer.Serialize(new
        {
            code,
            port = Server.Port,
            pluginVersion = _pluginVersion,
            gameVersion = GameCore.Version.VersionString,
            labApiVersion = LabApiProperties.CompiledVersion,
        }, Json.Options);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await Http.PostAsync($"{_apiUrl().TrimEnd('/')}/v1/servers/setup", content).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return SetupResult.Fail($"Could not reach O5 Control: {exception.GetBaseException().Message}");
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return SetupResult.Fail(Describe(response.StatusCode, json));

            var redeemed = JsonSerializer.Deserialize<RedeemResponse>(json, Json.Options);
            if (redeemed is null || string.IsNullOrEmpty(redeemed.ServerId) || string.IsNullOrEmpty(redeemed.Secret))
                return SetupResult.Fail("O5 Control returned an incomplete answer.");

            _store.Save(new Credential { ServerId = redeemed.ServerId, Instance = redeemed.Instance.Slug, Secret = redeemed.Secret, Language = language });
            return SetupResult.Ok(redeemed.Instance, redeemed.ServerName);
        }
    }

    private static string Describe(HttpStatusCode status, string json)
    {
        try
        {
            var error = JsonSerializer.Deserialize<ErrorMessage>(json, Json.Options);
            if (!string.IsNullOrEmpty(error?.Message))
                return error!.Message;
        }
        catch (JsonException)
        {
        }

        return status switch
        {
            HttpStatusCode.NotFound => "That code does not exist.",
            HttpStatusCode.Gone => "That code has expired or was already used. Generate a new one in the panel.",
            (HttpStatusCode)429 => "Too many attempts. Wait a minute and try again.",
            _ => $"O5 Control answered {(int)status}.",
        };
    }

    private sealed class RedeemResponse
    {
        public string ServerId { get; set; } = string.Empty;

        public string ServerName { get; set; } = string.Empty;

        public string Secret { get; set; } = string.Empty;

        public InstanceInfo Instance { get; set; } = new();
    }
}

internal sealed class SetupResult
{
    private SetupResult(bool success, string? error, InstanceInfo? instance, string? serverName)
    {
        Success = success;
        Error = error;
        Instance = instance;
        ServerName = serverName;
    }

    public bool Success { get; }

    public string? Error { get; }

    public InstanceInfo? Instance { get; }

    public string? ServerName { get; }

    public static SetupResult Ok(InstanceInfo instance, string serverName) => new(true, null, instance, serverName);

    public static SetupResult Fail(string error) => new(false, error, null, null);
}
