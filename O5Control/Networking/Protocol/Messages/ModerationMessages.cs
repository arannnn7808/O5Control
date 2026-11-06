using System.Collections.Generic;
using O5Control.API.Models;

namespace O5Control.Networking.Protocol.Messages;

internal sealed class SanctionIssueMessage
{
    public SanctionType Type { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? PlayerName { get; set; }

    public string Reason { get; set; } = string.Empty;

    public long? DurationSeconds { get; set; }

    public SanctionScope Scope { get; set; } = SanctionScope.Instance;

    public string? IssuerUserId { get; set; }

    public string? IssuerName { get; set; }

    public string Source { get; set; } = "plugin";
}

internal sealed class SanctionRevokeMessage
{
    public string? SanctionId { get; set; }

    public string? UserId { get; set; }

    public SanctionType? Type { get; set; }

    public string Source { get; set; } = "plugin";

    public string? IssuerUserId { get; set; }

    public string? Reason { get; set; }
}

internal sealed class SanctionRevokedMessage
{
    public string SanctionId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public SanctionType Type { get; set; }
}

internal sealed class SanctionListMessage
{
    public string UserId { get; set; } = string.Empty;

    public bool ActiveOnly { get; set; }
}

internal sealed class SanctionListResult
{
    public List<Sanction> Sanctions { get; set; } = new();
}

internal sealed class BansSnapshotMessage
{
    public List<Sanction> Bans { get; set; } = new();
}

internal sealed class StaffSnapshotMessage
{
    public List<StaffAssignment> Staff { get; set; } = new();
}

internal sealed class StaffUnassignMessage
{
    public string UserId { get; set; } = string.Empty;
}
