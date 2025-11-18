namespace PoolMate.Api.Dtos.Tournament
{
    public class CorrectMatchResultRequest
    {
        public int WinnerTpId { get; set; }
        public int? ScoreP1 { get; set; }
        public int? ScoreP2 { get; set; }
        public int? RaceTo { get; set; }
    }
}
