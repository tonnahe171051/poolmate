using System;
using PoolMate.Api.Dtos.Tournament;

namespace PoolMate.Api.Common
{
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(MatchDto latestMatch)
            : base("The match was modified by another client.")
        {
            LatestMatch = latestMatch;
        }

        public MatchDto LatestMatch { get; }
    }
}
