using Microsoft.AspNetCore.Identity;

namespace PoolMate.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Nickname { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? ProfilePicture { get; set; }
        public string? AvatarPublicId { get; set; }
        
        // Dashboard tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
