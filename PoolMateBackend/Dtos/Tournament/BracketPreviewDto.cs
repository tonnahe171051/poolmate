namespace PoolMate.Api.Dtos.Tournament
{
    public class BracketPreviewDto
    {
        public int TournamentId { get; set; }
        public bool IsMultiStage { get; set; }
        public StageDto Stage1 { get; set; } = default!;
        public StageDto? Stage2 { get; set; }
    }
}
