using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabApi.Features.Wrappers;
using O5Control.API.Models;
using O5Control.Core;
using O5Control.Networking.Protocol;
using O5Control.Networking.Protocol.Messages;

namespace O5Control.API;

public static class O5
{
    public static bool IsAvailable => Runtime is not null;

    public static bool IsConnected => Runtime?.Client.IsConnected ?? false;

    public static string? InstanceSlug => Runtime?.Instance?.Slug;

    public static string? InstanceName => Runtime?.Instance?.Name;

    public static string? ServerName => Runtime?.ServerName;

    public static string? PluginVersion => Runtime?.Version;

    private static Runtime? Runtime => O5ControlPlugin.Instance?.Runtime;

    public static bool IsBanned(string userId) => TryGetBan(userId, out _);

    public static bool TryGetBan(string userId, out Sanction? ban)
    {
        ban = null;
        return Runtime is { } runtime && runtime.Bans.TryGet(userId, out ban);
    }

    public static IReadOnlyCollection<Sanction> ActiveBans => Runtime?.Bans.All ?? Array.Empty<Sanction>();

    public static bool TryGetStaff(string userId, out StaffAssignment? assignment)
    {
        assignment = null;
        return Runtime is { } runtime && runtime.Staff.TryGet(userId, out assignment);
    }

    public static Task<O5Result<Sanction>> IssueAsync(SanctionRequest request) =>
        Runtime is { } runtime ? runtime.Sanctions.IssueAsync(request, "api") : Unavailable<Sanction>();

    public static Task<O5Result<Sanction>> BanAsync(Player target, string reason, TimeSpan? duration = null, Player? issuer = null, SanctionScope scope = SanctionScope.Instance)
    {
        var request = SanctionRequest.For(SanctionType.Ban, target, reason);
        request.Duration = duration;
        request.Issuer = issuer;
        request.Scope = scope;
        return IssueAsync(request);
    }

    public static Task<O5Result<Sanction>> BanAsync(string userId, string reason, TimeSpan? duration = null, Player? issuer = null, SanctionScope scope = SanctionScope.Instance) =>
        IssueAsync(new SanctionRequest(SanctionType.Ban, userId, reason) { Duration = duration, Issuer = issuer, Scope = scope });

    public static Task<O5Result<Sanction>> MuteAsync(Player target, string reason, TimeSpan? duration = null, Player? issuer = null)
    {
        var request = SanctionRequest.For(SanctionType.Mute, target, reason);
        request.Duration = duration;
        request.Issuer = issuer;
        return IssueAsync(request);
    }

    public static Task<O5Result<Sanction>> WarnAsync(Player target, string reason, Player? issuer = null)
    {
        var request = SanctionRequest.For(SanctionType.Warn, target, reason);
        request.Issuer = issuer;
        return IssueAsync(request);
    }

    public static Task<O5Result<Sanction>> KickAsync(Player target, string reason, Player? issuer = null)
    {
        var request = SanctionRequest.For(SanctionType.Kick, target, reason);
        request.Issuer = issuer;
        return IssueAsync(request);
    }

    public static Task<O5Result> RevokeAsync(string sanctionId, Player? issuer = null, string? reason = null) =>
        Runtime is { } runtime
            ? runtime.Sanctions.RevokeAsync(sanctionId, issuer?.UserId, reason)
            : Task.FromResult(O5Result.Fail(O5ErrorCodes.NotConnected, "O5Control is not loaded."));

    public static Task<O5Result<IReadOnlyList<Sanction>>> GetSanctionsAsync(string userId, bool activeOnly = false) =>
        Runtime is { } runtime ? runtime.Sanctions.ListAsync(userId, activeOnly) : Unavailable<IReadOnlyList<Sanction>>();

    public static async Task<O5Result<PlayerProfile>> GetPlayerAsync(string userId)
    {
        if (Runtime is not { } runtime)
            return O5Result<PlayerProfile>.Fail(O5ErrorCodes.NotConnected, "O5Control is not loaded.");

        if (!runtime.Client.IsConnected)
            return O5Result<PlayerProfile>.Fail(O5ErrorCodes.NotConnected, "This server is not connected to O5 Control.");

        try
        {
            var reply = await runtime.Client.RequestAsync(Ops.PlayerLookup, new PlayerLookupMessage { UserId = userId }).ConfigureAwait(false);
            var profile = reply.Read<PlayerProfile>();
            return profile is null
                ? O5Result<PlayerProfile>.Fail(O5ErrorCodes.Internal, "The panel returned an empty profile.")
                : O5Result<PlayerProfile>.Ok(profile);
        }
        catch (Exception exception)
        {
            return O5Result<PlayerProfile>.Fail(exception is TimeoutException ? O5ErrorCodes.Timeout : O5ErrorCodes.Internal, exception.Message);
        }
    }

    public static void Log(string category, string message, Player? actor = null) =>
        Runtime?.Recorder.Custom(category, message, actor);

    private static Task<O5Result<T>> Unavailable<T>() =>
        Task.FromResult(O5Result<T>.Fail(O5ErrorCodes.NotConnected, "O5Control is not loaded."));
}
