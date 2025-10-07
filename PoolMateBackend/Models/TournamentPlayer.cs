using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class TournamentPlayer
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = default!;

        public int? PlayerId { get; set; }
        public Player? Player { get; set; } = default!;

        public int? Seed { get; set; }
        public TournamentPlayerStatus Status { get; set; } = TournamentPlayerStatus.Unconfirmed;

        //for snapshot
        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = default!;
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        [MaxLength(2)]
        public string? Country { get; set; }
        public string? City { get; set; }
        public int? SkillLevel { get; set; }

    }
}
