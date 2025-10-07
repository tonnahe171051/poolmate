using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class AddTournamentPlayersPerLineModel
    {
        public string Lines { get; set; } = string.Empty;
        public TournamentPlayerStatus? DefaultStatus { get; set; } 
    }
}
