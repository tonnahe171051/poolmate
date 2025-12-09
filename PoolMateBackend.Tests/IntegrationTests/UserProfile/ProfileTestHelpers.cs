using Microsoft.AspNetCore.Identity;
using PoolMate.Api.Data;
using PoolMate.Api.Models;

namespace PoolMateBackend.Tests.IntegrationTests.UserProfile;

/// <summary>
/// Helper class for creating test data for Profile Management integration tests.
/// Provides reusable methods to create users with profile data.
/// </summary>
public static class ProfileTestHelpers
{
    /// <summary>
    /// Creates a new user with complete profile information.
    /// </summary>
    public static async Task<ApplicationUser> CreateUserWithProfileAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName,
        string? phoneNumber = null,
        string? nickname = null,
        string? city = null,
        string? country = null,
        string? avatarUrl = null,
        string? avatarPublicId = null)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Nickname = nickname,
            City = city,
            Country = country,
            ProfilePicture = avatarUrl,
            AvatarPublicId = avatarPublicId
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    /// <summary>
    /// Creates a basic user with minimal profile data.
    /// </summary>
    public static async Task<ApplicationUser> CreateBasicUserAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        string email,
        string password = "Test@123")
    {
        return await CreateUserWithProfileAsync(
            db,
            userManager,
            email,
            password,
            "Test",
            "User",
            phoneNumber: "+84123456789"
        );
    }

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    public static async Task<ApplicationUser?> GetUserByEmailAsync(
        UserManager<ApplicationUser> userManager,
        string email)
    {
        return await userManager.FindByEmailAsync(email);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public static async Task<ApplicationUser?> GetUserByIdAsync(
        UserManager<ApplicationUser> userManager,
        string userId)
    {
        return await userManager.FindByIdAsync(userId);
    }
}

