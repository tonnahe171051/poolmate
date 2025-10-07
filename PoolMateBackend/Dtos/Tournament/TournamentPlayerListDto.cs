using PoolMate.Api.Models;

namespace PoolMate.Api.Dtos.Tournament
{
    public class TournamentPlayerListDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Country { get; set; }
        public int? Seed { get; set; }
        public int? SkillLevel { get; set; }
        public TournamentPlayerStatus Status { get; set; }
        public int? PlayerId { get; set; } 
    }
}
