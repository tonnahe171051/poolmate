namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class ApplyFargoRatingRequest
    {
        public int TournamentPlayerId { get; set; }
        public int? Rating { get; set; }
        public bool Apply { get; set; }
    }
}
