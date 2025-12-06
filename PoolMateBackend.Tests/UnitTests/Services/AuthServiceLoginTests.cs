using Moq;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;

namespace PoolMateBackend.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService.LoginAsync method.
/// </summary>
public class AuthServiceLoginTests : AuthServiceTestBase
{
    #region LoginAsync Tests

    /// <summary>
    /// Test case: User not found - should throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task Login_UserNotFound_ThrowsException()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "nonexistentuser",
            Password = "password123"
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.LoginAsync(loginModel)
        );

        Assert.Equal("Invalid username or password.", exception.Message);
    }

    /// <summary>
    /// Test case: Account locked - should throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task Login_AccountLocked_ThrowsException()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "lockeduser",
            Password = "password123"
        };

        var lockedUser = new ApplicationUser
        {
            Id = "user-id-123",
            UserName = "lockeduser",
            Email = "locked@example.com",
            LockoutEnd = DateTimeOffset.UtcNow.AddDays(1) // Locked until tomorrow
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("lockeduser"))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.LoginAsync(loginModel)
        );

        Assert.Equal("This account has been locked. Please contact administrator.", exception.Message);
    }

    /// <summary>
    /// Test case: Wrong password - should throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task Login_WrongPassword_ThrowsException()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "testuser",
            Password = "wrongpassword"
        };

        var user = new ApplicationUser
        {
            Id = "user-id-123",
            UserName = "testuser",
            Email = "test@example.com",
            LockoutEnd = null
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("testuser"))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, "wrongpassword"))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.LoginAsync(loginModel)
        );

        Assert.Equal("Invalid username or password.", exception.Message);
    }

    /// <summary>
    /// Test case: Email not confirmed - should throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task Login_EmailNotConfirmed_ThrowsException()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "testuser",
            Password = "correctpassword"
        };

        var user = new ApplicationUser
        {
            Id = "user-id-123",
            UserName = "testuser",
            Email = "test@example.com",
            EmailConfirmed = false,
            LockoutEnd = null
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("testuser"))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, "correctpassword"))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.LoginAsync(loginModel)
        );

        Assert.Equal("Email is not confirmed.", exception.Message);
    }

    /// <summary>
    /// Test case: Successful login - should return token and user info (Happy Path)
    /// </summary>
    [Fact]
    public async Task Login_Success_ReturnsToken()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "testuser",
            Password = "correctpassword"
        };

        var user = new ApplicationUser
        {
            Id = "user-id-123",
            UserName = "testuser",
            Email = "test@example.com",
            EmailConfirmed = true,
            LockoutEnd = null
        };

        var userRoles = new List<string> { "Player", "Organizer" };

        MockUserManager
            .Setup(x => x.FindByNameAsync("testuser"))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, "correctpassword"))
            .ReturnsAsync(true);

        MockUserManager
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(userRoles);

        // Act
        var result = await Sut.LoginAsync(loginModel);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Value.Token);
        Assert.NotEmpty(result.Value.Token);
        Assert.Equal("user-id-123", result.Value.UserId);
        Assert.Equal("testuser", result.Value.UserName);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal(userRoles, result.Value.Roles);
        Assert.True(result.Value.Exp > DateTime.UtcNow);
    }

    #endregion
}

