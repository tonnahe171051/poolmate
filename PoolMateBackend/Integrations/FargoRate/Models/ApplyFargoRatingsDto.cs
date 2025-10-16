namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class ApplyFargoRatingsDto
    {
        public int TournamentId { get; set; } 
        public List<ApplyFargoRatingRequest> Requests { get; set; } = new();
    }
}
