using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService auth) : ControllerBase
{
    
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterModel model, CancellationToken ct)
    {
        var baseUri = $"{Request.Scheme}://{Request.Host}"; 
        var res = await auth.RegisterAsync(model, baseUri, ct);
        return res.Status == "Success" ? Ok(res) : BadRequest(res);
    }

    [HttpPost("register-admin")]
    [Authorize(Roles = UserRoles.ADMIN)] 
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterModel model, CancellationToken ct)
    {
        var res = await auth.RegisterAdminAsync(model, ct); 
        return res.Status == "Success" ? Ok(res) : BadRequest(res);
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return BadRequest("Missing userId or token");

        var res = await auth.ConfirmEmailAsync(userId, token, ct);
        return res.Status == "Success" ? Ok(res) : BadRequest(res);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel model, CancellationToken ct)
    {
        try
        {
            var r = await auth.LoginAsync(model, ct);
            if (r is null) return Unauthorized();

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
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model, CancellationToken ct)
    {
        var baseUri = $"{Request.Scheme}://{Request.Host}";
        var res = await auth.ForgotPasswordAsync(model.Email!, baseUri, ct);
        return res.Status == "Success" ? Ok(res) : BadRequest(res);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.UserId) || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.NewPassword))
            return BadRequest("Missing required fields");

        var res = await auth.ResetPasswordAsync(model.UserId, model.Token, model.NewPassword, ct);
        return res.Status == "Success" ? Ok(res) : BadRequest(res);
    }

}
