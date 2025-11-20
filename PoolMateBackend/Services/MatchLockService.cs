using System;
using Microsoft.Extensions.Caching.Memory;

namespace PoolMate.Api.Services
{
    public interface IMatchLockService
    {
        MatchLockResult AcquireOrRefresh(int matchId, string ownerId, string? existingLockId);
        void Release(int matchId, string lockId, string ownerId);
        MatchLockSnapshot? GetCurrent(int matchId);
    }

    public sealed record MatchLockResult(bool Granted, string LockId, DateTimeOffset ExpiresAt, bool IsNew);

    public sealed record MatchLockSnapshot(string LockId, string OwnerId, DateTimeOffset ExpiresAt);

    public class MatchLockService : IMatchLockService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _lockDuration = TimeSpan.FromSeconds(45);
        private readonly object _syncRoot = new();

        public MatchLockService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public MatchLockResult AcquireOrRefresh(int matchId, string ownerId, string? existingLockId)
        {
            var cacheKey = GetKey(matchId);
            var now = DateTimeOffset.UtcNow;

            lock (_syncRoot)
            {
                if (!_cache.TryGetValue(cacheKey, out MatchLockSnapshot? snapshot) || snapshot is null || snapshot.ExpiresAt <= now)
                {
                    var newLock = new MatchLockSnapshot(existingLockId ?? Guid.NewGuid().ToString("N"), ownerId, now.Add(_lockDuration));
                    SetSnapshot(cacheKey, newLock);
                    return new MatchLockResult(true, newLock.LockId, newLock.ExpiresAt, true);
                }

                if (snapshot.OwnerId == ownerId && (existingLockId is null || snapshot.LockId == existingLockId))
                {
                    var refreshed = snapshot with { ExpiresAt = now.Add(_lockDuration) };
                    SetSnapshot(cacheKey, refreshed);
                    return new MatchLockResult(true, refreshed.LockId, refreshed.ExpiresAt, false);
                }

                return new MatchLockResult(false, snapshot.LockId, snapshot.ExpiresAt, false);
            }
        }

        public void Release(int matchId, string lockId, string ownerId)
        {
            var cacheKey = GetKey(matchId);

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(cacheKey, out MatchLockSnapshot? snapshot) &&
                    snapshot is not null &&
                    snapshot.OwnerId == ownerId && snapshot.LockId == lockId)
                {
                    _cache.Remove(cacheKey);
                }
            }
        }

        public MatchLockSnapshot? GetCurrent(int matchId)
        {
            var cacheKey = GetKey(matchId);
            if (_cache.TryGetValue(cacheKey, out MatchLockSnapshot? snapshot) && snapshot is not null)
            {
                if (snapshot.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    return snapshot;
                }

                _cache.Remove(cacheKey);
            }

            return null;
        }

        private void SetSnapshot(string cacheKey, MatchLockSnapshot snapshot)
        {
            _cache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = snapshot.ExpiresAt
            });
        }

        private static string GetKey(int matchId) => $"match-lock:{matchId}";
    }
}
