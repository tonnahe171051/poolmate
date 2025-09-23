using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Post
{
    public class UpdatePostModel
    {
        [MaxLength(2000)]
        public string? Content { get; set; }
        
        public string? ImagePublicId { get; set; }
        
        public string? ImageUrl { get; set; }
    }
}
