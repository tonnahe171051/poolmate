
using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UserTournamentListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public TournamentStatus Status { get; set; }
        public int TotalPlayers { get; set; }
        public GameType GameType { get; set; }
        public BracketType BracketType { get; set; }
        public VenueDto? Venue { get; set; }
    }
}

