using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class CompleteStageRequest
    {
        public Stage2CreationRequest? Stage2 { get; set; }
    }

    public class Stage2CreationRequest
    {
        public BracketCreationType Type { get; set; } = BracketCreationType.Automatic;
        public List<ManualSlotAssignment>? ManualAssignments { get; set; }
    }

    public class StageCompletionResultDto
    {
        public int StageNo { get; set; }
        public StageStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool CreatedStage2 { get; set; }
        public int? Stage2Id { get; set; }
        public bool TournamentCompleted { get; set; }
    }

    public class TournamentPlayerStatsDto
    {
        public int TournamentPlayerId { get; set; }
        public string DisplayName { get; set; } = default!;
        public int? Seed { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int RacksWon { get; set; }
        public int RacksLost { get; set; }
        public int? LastStageNo { get; set; }
        public bool IsEliminated { get; set; }
        public int? PlacementRank { get; set; }
        public string? PlacementLabel { get; set; }
    }
}
