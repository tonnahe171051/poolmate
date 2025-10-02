
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public DateTime StartUtc { get; set; }
        public string? FlyerUrl { get; set; }
        public GameType GameType { get; set; }
        public int? BracketSizeEstimate { get; set; }
        public int? WinnersRaceTo { get; set; }
        public decimal? EntryFee { get; set; }
        public VenueDto? Venue { get; set; }
    }

    public class VenueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Address { get; set; }
        public string? City { get; set; }
    }
}
