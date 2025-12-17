using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth, IConfiguration config) : ControllerBase
{
    
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterModel model, CancellationToken ct)
    {
        try
        {
            var frontendUrl = config["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
            var res = await auth.RegisterAsync(model, frontendUrl, ct);
            
            if (res.Status == "Success")
                return Ok(new { message = res.Message });
            
            return BadRequest(new { message = res.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Missing userId or token" });

            var res = await auth.ConfirmEmailAsync(userId, token, ct);
            
            if (res.Status == "Success")
                return Ok(new { message = res.Message });
            
            return BadRequest(new { message = res.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel model, CancellationToken ct)
    {
        try
        {
            var r = await auth.LoginAsync(model, ct);
            if (r is null)
                return Unauthorized(new { message = "Invalid email or password." });

            return Ok(new
            {
                token = r.Value.Token,
                expiration = r.Value.Exp,
                userId = r.Value.UserId,
                userName = r.Value.UserName,
                userEmail = r.Value.Email,
                roles = r.Value.Roles
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Email is not confirmed." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model, CancellationToken ct)
    {
        try
        {
            var frontendUrl = config["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
            var res = await auth.ForgotPasswordAsync(model.Email!, frontendUrl, ct);
            
            if (res.Status == "Success")
                return Ok(new { message = res.Message });
            
            return BadRequest(new { message = res.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.UserId) || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.NewPassword))
                return BadRequest(new { message = "Missing required fields" });

            var res = await auth.ResetPasswordAsync(model.UserId, model.Token, model.NewPassword, ct);
            
            if (res.Status == "Success")
                return Ok(new { message = res.Message });
            
            return BadRequest(new { message = res.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize] 
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
                return BadRequest(new { message = "Missing required fields" });

            // Lấy userId từ JWT token
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token" });

            var res = await auth.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword, ct);
            
            if (res.Status == "Success")
                return Ok(new { message = res.Message });
            
            return BadRequest(new { message = res.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

}
