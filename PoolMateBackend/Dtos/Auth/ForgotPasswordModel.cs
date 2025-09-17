using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Auth
{
    public class ForgotPasswordModel
    {
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }
    }
}
