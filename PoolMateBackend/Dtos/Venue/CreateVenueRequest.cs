namespace PoolMate.Api.Dtos.Venue
{
    public class CreateVenueRequest
    {
        public string Name { get; set; } = default!;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }  // ISO-3166 alpha-2: "VN"
    }
}
