using System;
using System.Text.Json.Serialization;

namespace O5Control.API.Models;

public sealed class Sanction
{
    public string Id { get; set; } = string.Empty;

    public SanctionType Type { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? PlayerName { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? IssuedBy { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public SanctionScope Scope { get; set; }

    public bool Revoked { get; set; }

    [JsonIgnore]
    public bool IsPermanent => ExpiresAt is null && Type is SanctionType.Ban or SanctionType.Mute;

    [JsonIgnore]
    public bool IsActive => !Revoked && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);

    [JsonIgnore]
    public TimeSpan? Remaining => ExpiresAt is { } expires ? expires - DateTimeOffset.UtcNow : null;
}
