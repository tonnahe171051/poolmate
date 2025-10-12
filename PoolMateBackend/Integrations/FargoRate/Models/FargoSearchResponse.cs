namespace PoolMate.Api.Integrations.FargoRate.Models
{
    public class FargoSearchResponse
    {
        public List<FargoPlayerData> Value { get; set; } = new List<FargoPlayerData>();
    }

    public class FargoPlayerData
    {
        public string? Id { get; set; }
        public string? ReadableId { get; set; }
        public string? MembershipId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Location { get; set; }
        public string? Rating { get; set; }
        public string? Robustness { get; set; }
        public string? ProvisionalRating { get; set; }
        public string? EffectiveRating { get; set; }
        public int? MembershipNumber { get; set; }
        public string? ImageUrl { get; set; }
        public string? ImsId { get; set; }
        public string? ShareMatches { get; set; }
        public string? StatsOverall { get; set; }
        public string? StatsByRating { get; set; }
        public string? RatingHistory { get; set; }
    }
}
