using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using O5Control.API.Models;

namespace O5Control.Features.Moderation;

internal sealed class BanCache
{
    private readonly ConcurrentDictionary<string, Sanction> _byUser = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byUser.Count;

    public IReadOnlyCollection<Sanction> All => _byUser.Values.Where(ban => ban.IsActive).ToList();

    public void Replace(IEnumerable<Sanction> bans)
    {
        _byUser.Clear();

        foreach (var ban in bans)
            Upsert(ban);
    }

    public void Upsert(Sanction ban)
    {
        if (ban.Type != SanctionType.Ban || !ban.IsActive || string.IsNullOrEmpty(ban.UserId))
            return;

        _byUser.AddOrUpdate(ban.UserId, ban, (_, existing) => Outlasts(ban, existing) ? ban : existing);
    }

    public void Remove(string sanctionId)
    {
        foreach (var pair in _byUser)
        {
            if (pair.Value.Id == sanctionId)
                _byUser.TryRemove(pair.Key, out _);
        }
    }

    public bool TryGet(string userId, out Sanction? ban)
    {
        if (_byUser.TryGetValue(userId, out ban))
        {
            if (ban.IsActive)
                return true;

            _byUser.TryRemove(userId, out _);
        }

        ban = null;
        return false;
    }

    private static bool Outlasts(Sanction candidate, Sanction existing)
    {
        if (!existing.IsActive || candidate.ExpiresAt is null)
            return true;

        return existing.ExpiresAt is not null && candidate.ExpiresAt > existing.ExpiresAt;
    }
}
