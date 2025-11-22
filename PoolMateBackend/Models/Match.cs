using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class Match
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = default!;

        public int StageId { get; set; }
        public TournamentStage Stage { get; set; } = default!;

        public BracketSide Bracket { get; set; }           
        public int RoundNo { get; set; }                   // 1,2,3…
        public int PositionInRound { get; set; }           

        public int? Player1TpId { get; set; }
        public TournamentPlayer? Player1Tp { get; set; }
        public int? Player2TpId { get; set; }
        public TournamentPlayer? Player2Tp { get; set; }    

    public MatchSlotSourceType? Player1SourceType { get; set; }
    public int? Player1SourceMatchId { get; set; }
    public MatchSlotSourceType? Player2SourceType { get; set; }
    public int? Player2SourceMatchId { get; set; }

        public int? TableId { get; set; }
        public TournamentTable? Table { get; set; }// TournamentTable.Id
        public DateTime? ScheduledUtc { get; set; }

        public int? RaceTo { get; set; }
        public MatchStatus Status { get; set; } = MatchStatus.NotStarted;

        // Result
        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? WinnerTpId { get; set; }
        public TournamentPlayer? WinnerTp { get; set; }

        // bracket
        public int? NextWinnerMatchId { get; set; }
        public int? NextLoserMatchId { get; set; }         

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
    }
}
