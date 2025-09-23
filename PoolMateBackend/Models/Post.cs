using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoolMate.Api.Models
{
    public class Post
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        
        public string? ImageUrl { get; set; }
        
        public string? ImagePublicId { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
    }
}
