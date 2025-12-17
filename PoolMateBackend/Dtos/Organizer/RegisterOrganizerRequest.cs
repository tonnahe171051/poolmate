using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Organizer
{
    public class RegisterOrganizerRequest
    {
        [Required(ErrorMessage = "Organization name is required")]
        [MaxLength(200)]
        public string OrganizationName { get; set; } = default!;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(200)]
        public string Email { get; set; } = default!;

        [MaxLength(300)]
        [Url(ErrorMessage = "Invalid URL format")]
        public string? FacebookPageUrl { get; set; }
    }
}
