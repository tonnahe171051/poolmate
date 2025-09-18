using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.UserProfile
{
    public class UpdateProfileModel
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Nickname { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? AvatarPublicId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
    }
}
