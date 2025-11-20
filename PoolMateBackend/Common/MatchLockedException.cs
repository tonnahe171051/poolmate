using System;

namespace PoolMate.Api.Common
{
    public class MatchLockedException : Exception
    {
        public MatchLockedException(string lockId, DateTimeOffset expiresAt)
            : base("Match is currently being updated by another device.")
        {
            LockId = lockId;
            ExpiresAt = expiresAt;
        }

        public string LockId { get; }
        public DateTimeOffset ExpiresAt { get; }
    }
}
