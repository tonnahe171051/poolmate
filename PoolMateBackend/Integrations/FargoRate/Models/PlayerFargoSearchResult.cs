namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class PlayerFargoSearchResult
    {
        public int TournamentPlayerId { get; set; }
        public string OriginalName { get; set; } = string.Empty;
        public FargoPlayerResult? FargoResult { get; set; }
        public bool IsFound => FargoResult != null;
        public string? ErrorMessage { get; set; }
    }
}
