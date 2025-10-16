namespace PoolMate.Api.Dtos.Tournament
{
    public class BracketPreviewDto
    {
        public int TournamentId { get; set; }
        public bool IsMultiStage { get; set; }
        public StagePreviewDto Stage1 { get; set; } = default!;
        public StagePreviewDto? Stage2 { get; set; }
    }
}
