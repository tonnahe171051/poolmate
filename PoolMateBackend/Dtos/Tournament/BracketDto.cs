using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class BracketDto
    {
        public int TournamentId { get; set; }
        public bool IsMultiStage { get; set; }
        public List<StageDto> Stages { get; set; } = new();
    }

    public class StageDto
    {
        public int StageNo { get; set; }
        public BracketType Type { get; set; }
        public BracketOrdering Ordering { get; set; }
        public int BracketSize { get; set; }
        public List<BracketSideDto> Brackets { get; set; } = new();
    }

    public class BracketSideDto
    {
        public BracketSide BracketSide { get; set; }
        public List<RoundDto> Rounds { get; set; } = new();
    }

    public class RoundDto
    {
        public int RoundNo { get; set; }
        public List<MatchDto> Matches { get; set; } = new();
    }

    public class MatchDto
    {
        public int Id { get; set; }
        public int RoundNo { get; set; }
        public int PositionInRound { get; set; }
        public BracketSide Bracket { get; set; }
        public MatchStatus Status { get; set; }

        public PlayerDto? Player1 { get; set; }
        public PlayerDto? Player2 { get; set; }
        public PlayerDto? Winner { get; set; }
        
        public DateTime? ScheduledUtc { get; set; }
        public string? ScheduledDisplay { get; set; } // "Oct 21, 11:00h"

        public int? TableId { get; set; }
        public string? TableLabel { get; set; }

        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? RaceTo { get; set; }

        public int? NextWinnerMatchId { get; set; }
        public int? NextLoserMatchId { get; set; }

        public bool IsBye { get; set; } //handle BYE matches
    }

    public class PlayerDto
    {
        public int TpId { get; set; }
        public string Name { get; set; } = default!;
        public int? Seed { get; set; }
        public string? Country { get; set; } 
        public int? FargoRating { get; set; }
    }
}
