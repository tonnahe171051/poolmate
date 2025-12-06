using PoolMate.Api.Dtos.Auth;

namespace PoolMate.Api.Services
{
    public interface IAuthService
    {
        Task<(string Token, DateTime Exp, string UserId, string? UserName, string? Email, IList<string> Roles)?>
            LoginAsync(LoginModel model, CancellationToken ct = default);

        Task<Response> RegisterAsync(RegisterModel model, string baseUri, CancellationToken ct = default);

        Task<Response> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);

        // Task<Response> RegisterAdminAsync(RegisterModel model, CancellationToken ct = default);

        Task<Response> ForgotPasswordAsync(string email, string baseUri, CancellationToken ct = default);

        Task<Response> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default);

        Task<Response> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
    }
}
