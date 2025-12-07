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

    // [NEW] Case biên quan trọng: Đã từng bị khóa nhưng giờ ĐÃ HẾT HẠN -> Phải đăng nhập được
    /// <summary>
    /// Test case: Lockout expired (past date) - should allow login
    /// </summary>
    [Fact]
    public async Task Login_LockoutExpired_ReturnsToken()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "expireduser",
            Password = "correctpassword"
        };

        var user = new ApplicationUser
        {
            Id = "user-id-expired",
            UserName = "expireduser",
            Email = "expired@example.com",
            EmailConfirmed = true,
            // Setup: Thời gian khóa là 10 phút trước -> Đã hết hạn
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-10) 
        };

        MockUserManager
            .Setup(x => x.FindByNameAsync("expireduser"))
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, "correctpassword"))
            .ReturnsAsync(true);

        MockUserManager
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Player" });

        // Act
        var result = await Sut.LoginAsync(loginModel);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Value.Token);
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

    // [NEW] Case biên Input: Username có khoảng trắng -> Hệ thống phải tự Trim
    /// <summary>
    /// Test case: Username has whitespace - should trim and succeed
    /// </summary>
    [Fact]
    public async Task Login_UsernameWithSpaces_ReturnsToken()
    {
        // Arrange
        var loginModel = new LoginModel
        {
            Username = "  testuser  ", // Input có dấu cách
            Password = "correctpassword"
        };

        var user = new ApplicationUser
        {
            UserName = "testuser", // DB sạch
            EmailConfirmed = true
        };

        // Mock: Nếu code service không gọi .Trim(), nó sẽ tìm "  testuser  " và mock này sẽ fail
        MockUserManager
            .Setup(x => x.FindByNameAsync("testuser")) 
            .ReturnsAsync(user);

        MockUserManager
            .Setup(x => x.CheckPasswordAsync(user, "correctpassword"))
            .ReturnsAsync(true);

        MockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        // Act
        var result = await Sut.LoginAsync(loginModel);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Value.Token);
    }

    // [NEW] Case biên Input: Input là Null -> Phải báo lỗi tham số (không được crash)
    /// <summary>
    /// Test case: Input model is null - should throw ArgumentNullException
    /// </summary>
    [Fact]
    public async Task Login_NullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        // Giả sử code Sut.LoginAsync(null) sẽ ném ra ArgumentNullException
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Sut.LoginAsync(null)
        );
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