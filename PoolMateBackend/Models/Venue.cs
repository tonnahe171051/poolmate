using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class Venue
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = default!;    

        [MaxLength(160)]
        public string? Address { get; set; }           

        [MaxLength(100)]
        public string? City { get; set; }                

        [MaxLength(2)]
        public string? Country { get; set; }         

        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
    }
}

