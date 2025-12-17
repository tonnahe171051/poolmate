using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class Organizer
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        [Required, MaxLength(200)]
        public string OrganizationName { get; set; } = default!;

        [Required, MaxLength(200)]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [MaxLength(300)]
        public string? FacebookPageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
    }
}
