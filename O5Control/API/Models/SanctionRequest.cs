using System;
using LabApi.Features.Wrappers;

namespace O5Control.API.Models;

public sealed class SanctionRequest
{
    public SanctionRequest(SanctionType type, string userId, string reason)
    {
        Type = type;
        UserId = userId;
        Reason = reason;
    }

    public SanctionType Type { get; }

    public string UserId { get; }

    public string Reason { get; }

    public string? PlayerName { get; set; }

    public TimeSpan? Duration { get; set; }

    public SanctionScope Scope { get; set; } = SanctionScope.Instance;

    public Player? Issuer { get; set; }

    public string? IssuerName { get; set; }

    public static SanctionRequest For(SanctionType type, Player target, string reason) =>
        new(type, target.UserId, reason) { PlayerName = target.Nickname };
}
