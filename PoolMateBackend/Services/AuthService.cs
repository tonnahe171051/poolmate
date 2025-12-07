﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Integrations.Email;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IConfiguration _cfg;
    private readonly IEmailSender _email;

    public AuthService(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IConfiguration config,
        IEmailSender emailSender)
    {
        _users = users;
        _roles = roles;
        _cfg = config;
        _email = emailSender;
    }

    // LOGIN
    public async Task<(string Token, DateTime Exp, string UserId, string? UserName, string? Email, IList<string> Roles)?>
        LoginAsync(LoginModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        
        var username = model.Username?.Trim();

        var user = await _users.FindByNameAsync(username);
        if (user is null)
            throw new InvalidOperationException("Invalid username or password.");
        
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("This account has been locked. Please contact administrator.");
        }

        if (!await _users.CheckPasswordAsync(user, model.Password))
            throw new InvalidOperationException("Invalid username or password.");

        if (!user.EmailConfirmed)
            throw new InvalidOperationException("Email is not confirmed.");

        var userRoles = await _users.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty), 
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var r in userRoles) claims.Add(new Claim(ClaimTypes.Role, r));

        var token = BuildJwt(claims);
        return (new JwtSecurityTokenHandler().WriteToken(token), token.ValidTo, user.Id, user.UserName, user.Email, userRoles);
    }

    // REGISTER USER
    public async Task<Response> RegisterAsync(RegisterModel model, string baseUri, CancellationToken ct = default)
    {
        if (await _users.FindByNameAsync(model.Username) is not null)
            return Response.Error("User already exists!");

        var user = new ApplicationUser { UserName = model.Username, Email = model.Email, SecurityStamp = Guid.NewGuid().ToString() };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return Response.Error(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (!await _roles.RoleExistsAsync(UserRoles.PLAYER))
            await _roles.CreateAsync(new IdentityRole(UserRoles.PLAYER));
        await _users.AddToRoleAsync(user, UserRoles.PLAYER);

        await SendEmailConfirmationAsync(user, baseUri, ct);

        return Response.Ok("User created. Please check your email to confirm.");
    }

    // CONFIRM EMAIL
    public async Task<Response> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
    {
        var u = await _users.FindByIdAsync(userId);
        if (u is null)
            return Response.Error("User not found");

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await _users.ConfirmEmailAsync(u, decoded);

        if (result.Succeeded) return Response.Ok("Email confirmed successfully.");

        var msg = string.Join("; ", result.Errors.Select(e => e.Description));
        return Response.Error(string.IsNullOrWhiteSpace(msg) ? "Invalid or expired token." : msg);
    }
    
    // FORGOT PASSWORD - Send reset password email
    public async Task<Response> ForgotPasswordAsync(string email, string baseUri, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
            return Response.Ok("If an account with that email exists, a password reset link has been sent.");

        if (!user.EmailConfirmed)
            return Response.Error("Email is not confirmed. Please confirm your email first.");

        await SendPasswordResetEmailAsync(user, baseUri, ct);

        return Response.Ok("If an account with that email exists, a password reset link has been sent.");
    }

    // RESET PASSWORD - Confirm password reset with token
    public async Task<Response> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
            return Response.Error("User not found");

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await _users.ResetPasswordAsync(user, decoded, newPassword);

        if (result.Succeeded)
            return Response.Ok("Password has been reset successfully.");

        var msg = string.Join("; ", result.Errors.Select(e => e.Description));
        return Response.Error(string.IsNullOrWhiteSpace(msg) ? "Invalid or expired token." : msg);
    }

    // CHANGE PASSWORD - User đổi mật khẩu khi đã đăng nhập
    public async Task<Response> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
            return Response.Error("User not found");

        // Verify current password
        if (!await _users.CheckPasswordAsync(user, currentPassword))
            return Response.Error("Current password is incorrect");

        var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Response.Error(errors);
        }

        return Response.Ok("Password changed successfully");
    }
    
    // ===== helper =====

    private async Task SendPasswordResetEmailAsync(ApplicationUser user, string baseUri, CancellationToken ct)
    {
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = $"{baseUri}/api/auth/reset-password?userId={user.Id}&token={tokenEncoded}";

        await _email.SendAsync(user.Email!, "Reset your password",
            $"Hi {user.UserName},\nTo reset your password, please click: {resetUrl}", ct);
    }


    private async Task SendEmailConfirmationAsync(ApplicationUser u, string baseUri, CancellationToken ct)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(u);
        var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmUrl = $"{baseUri}/api/auth/confirm-email?userId={u.Id}&token={tokenEncoded}";

        await _email.SendAsync(u.Email!, "Confirm your email",
            $"Hi {u.UserName},\nPlease confirm by clicking: {confirmUrl}", ct);
    }

    private JwtSecurityToken BuildJwt(IEnumerable<Claim> claims)
    {
        var issuer = _cfg["JWT:ValidIssuer"] ?? throw new InvalidOperationException("Missing JWT:ValidIssuer");
        var audience = _cfg["JWT:ValidAudience"] ?? throw new InvalidOperationException("Missing JWT:ValidAudience");
        var secret = _cfg["JWT:Secret"] ?? throw new InvalidOperationException("Missing JWT:Secret");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            expires: DateTime.UtcNow.AddHours(3),
            claims: claims,
            signingCredentials: creds
        );
    }
}
