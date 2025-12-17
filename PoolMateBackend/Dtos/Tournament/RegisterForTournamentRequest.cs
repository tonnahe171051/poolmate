using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Tournament
{
    public class RegisterForTournamentRequest
    {
        [Required(ErrorMessage = "Display name is required")]
        [MaxLength(200)]
        public string DisplayName { get; set; } = default!;

        [MaxLength(100)]
        public string? Nickname { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone format")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(2)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }
    }
}
