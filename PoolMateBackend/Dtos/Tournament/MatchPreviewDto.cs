namespace PoolMate.Api.Dtos.Tournament
{
    public class MatchPreviewDto
    {
        public int PositionInRound { get; set; }       
        public string? P1Name { get; set; }            // allow null (BYE)
        public int? P1Seed { get; set; }
        public string? P2Name { get; set; }
        public int? P2Seed { get; set; }
    }
}
