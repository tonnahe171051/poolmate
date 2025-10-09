using PoolMate.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Tournament
{
    public class UpdateTournamentPlayerModel
    {
        public string? DisplayName { get; set; }
        public string? Nickname { get; set; }
        
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }
        
        public string? Phone { get; set; }
        
        [MaxLength(2)]
        public string? Country { get; set; }
        
        public string? City { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Skill level must be a non-negative integer")]
        public int? SkillLevel { get; set; }
        
        public int? Seed { get; set; }
        
        public TournamentPlayerStatus? Status { get; set; }
    }
}
