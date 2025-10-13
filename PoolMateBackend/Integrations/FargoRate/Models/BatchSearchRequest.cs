namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class BatchSearchRequest
    {
        public int TournamentPlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
    }
}
