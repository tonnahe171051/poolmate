using System;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdateLiveScoreRequest
    {
        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? RaceTo { get; set; }
        public string RowVersion { get; set; } = default!;
        public string? LockId { get; set; }
    }

    public class CompleteMatchRequest
    {
        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? RaceTo { get; set; }
        public string RowVersion { get; set; } = default!;
        public string? LockId { get; set; }
    }

    public class MatchScoreUpdateResponse
    {
        public MatchDto Match { get; set; } = default!;
        public string LockId { get; set; } = default!;
        public DateTimeOffset LockExpiresAt { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class TableTokenRequest
    {
        public int? LifetimeMinutes { get; set; }
    }

    public class TableTokenResponse
    {
        public string Token { get; set; } = default!;
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public class TableStatusDto
    {
        public int TableId { get; set; }
        public int TournamentId { get; set; }
        public TableStatus Status { get; set; }
        public int? CurrentMatchId { get; set; }
        public MatchStatus? CurrentMatchStatus { get; set; }
    }
}
