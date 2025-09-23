using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Post
{
    public class CreatePostModel
    {
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        
        public string? ImagePublicId { get; set; }
        
        public string? ImageUrl { get; set; }
    }
}
