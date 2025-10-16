namespace PoolMate.Api.Dtos.Tournament
{
    public class RoundPreviewDto
    {
        public int RoundNo { get; set; }
        public List<MatchPreviewDto> Matches { get; set; } = new();
    }
}
