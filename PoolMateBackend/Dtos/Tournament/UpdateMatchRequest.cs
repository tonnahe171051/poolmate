namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdateMatchRequest
    {
        public int? TableId { get; set; }
        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? RaceTo { get; set; }
        public int? WinnerTpId { get; set; }
        public DateTime? ScheduledUtc { get; set; }
    }
}
