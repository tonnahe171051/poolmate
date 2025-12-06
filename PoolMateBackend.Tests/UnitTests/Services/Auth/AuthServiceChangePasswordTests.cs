using Microsoft.AspNetCore.Identity;
using Moq;
using PoolMate.Api.Models;

namespace PoolMateBackend.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService.ChangePasswordAsync method.
/// </summary>
public class AuthServiceChangePasswordTests : AuthServiceTestBase
{
    #region ChangePasswordAsync Tests

    /// <summary>
    /// Test case: User not found - should return error and CheckPasswordAsync should NOT be called.
    /// </summary>
    [Fact]
    public async Task ChangePassword_UserNotFound_ReturnsError()
    {
        // Arrange
        var userId = "non-existent-user-id";
        var currentPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        MockUserManager
            .Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await Sut.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Equal("User not found", result.Message);

        // Verify CheckPasswordAsync was NOT called
        MockUserManager.Verify(
            x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never
        );
    }

    /// <summary>
    /// Test case: Wrong current password - should return error and ChangePasswordAsync should NOT be called.
    /// </summary>
    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsError()
    {
        // Arrange
        var userId = "user-id-123";
        var currentPassword = "WrongPassword";
        var newPassword = "NewPassword456!";

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        MockUserManager
            .Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, currentPassword))
            .ReturnsAsync(false);

        // Act
        var result = await Sut.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Equal("Current password is incorrect", result.Message);

        // Verify ChangePasswordAsync was NOT called
        MockUserManager.Verify(
            x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    /// <summary>
    /// Test case (Edge Case): Identity failed - new password is too weak.
    /// </summary>
    [Fact]
    public async Task ChangePassword_IdentityFailed_ReturnsError()
    {
        // Arrange
        var userId = "user-id-123";
        var currentPassword = "OldPassword123!";
        var newPassword = "weak";

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        var identityErrors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Password must be at least 6 characters." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Password must have at least one digit." }
        };

        MockUserManager
            .Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, currentPassword))
            .ReturnsAsync(true);

        MockUserManager
            .Setup(x => x.ChangePasswordAsync(user, currentPassword, newPassword))
            .ReturnsAsync(IdentityResult.Failed(identityErrors));

        // Act
        var result = await Sut.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Status);
        Assert.Contains("Password must be at least 6 characters.", result.Message);
        Assert.Contains("Password must have at least one digit.", result.Message);
    }

    /// <summary>
    /// Test case (Happy Path): All operations succeed - should return Ok.
    /// </summary>
    [Fact]
    public async Task ChangePassword_Success_ReturnsOk()
    {
        // Arrange
        var userId = "user-id-123";
        var currentPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        MockUserManager
            .Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, currentPassword))
            .ReturnsAsync(true);

        MockUserManager
            .Setup(x => x.ChangePasswordAsync(user, currentPassword, newPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await Sut.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Status);
        Assert.Equal("Password changed successfully", result.Message);

        // Verify ChangePasswordAsync was called exactly once
        MockUserManager.Verify(
            x => x.ChangePasswordAsync(user, currentPassword, newPassword),
            Times.Once
        );
    }

    #endregion
}

