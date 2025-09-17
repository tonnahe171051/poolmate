using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Dtos.Auth
{
    public class ResetPasswordModel
    {
        [Required(ErrorMessage = "User ID is required")]
        public string? UserId { get; set; }

        [Required(ErrorMessage = "Token is required")]
        public string? Token { get; set; }

        [Required(ErrorMessage = "New password is required")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }
    }
}
