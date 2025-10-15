namespace PoolMate.Api.Models
{
    public class TournamentStage
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = default!;

        // 1 or 2
        public int StageNo { get; set; }                    // Unique per tournament

        // stage format: Stage 1 = Double, Stage 2 = Single 
        public BracketType Type { get; set; }               
        public StageStatus Status { get; set; } = StageStatus.NotStarted;

        // Snapshot cấu hình tại thời điểm tạo stage
        public int? AdvanceCount { get; set; }              
        public BracketOrdering Ordering { get; set; }      

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Match> Matches { get; set; } = new List<Match>();
    }
}
