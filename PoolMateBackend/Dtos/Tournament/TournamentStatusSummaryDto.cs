using System;
using System.Collections.Generic;
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentStatusSummaryDto
    {
        public int TournamentId { get; set; }
        public TournamentStatus Status { get; set; }
        public bool IsStarted { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public TimeSpan? Runtime { get; set; }
        public double CompletionPercent { get; set; }
        public MatchBreakdownDto Matches { get; set; } = new();
        public TableBreakdownDto Tables { get; set; } = new();
        public TournamentPlacementDto? Champion { get; set; }
        public TournamentPlacementDto? RunnerUp { get; set; }
        public List<TournamentPlacementDto> AdditionalPlacements { get; set; } = new();
        public int ActivePlayers { get; set; }
        public int EliminatedPlayers { get; set; }
    }

    public class MatchBreakdownDto
    {
        public int Total { get; set; }
        public int WinnersSide { get; set; }
        public int LosersSide { get; set; }
        public int KnockoutSide { get; set; }
        public int FinalsSide { get; set; }
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int NotStarted { get; set; }
        public int Scheduled { get; set; }
    }

    public class TableBreakdownDto
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int InUse { get; set; }
        public int Closed { get; set; }
        public int AssignedTables { get; set; }
        public int MatchesOnWinnersSide { get; set; }
        public int MatchesOnLosersSide { get; set; }
        public int MatchesOnKnockoutSide { get; set; }
        public int MatchesOnFinalsSide { get; set; }
    }

    public class TournamentPlacementDto
    {
        public string Placement { get; set; } = default!;
        public int PlacementRank { get; set; }
        public int TournamentPlayerId { get; set; }
        public string DisplayName { get; set; } = default!;
        public int? Seed { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
    }
}
