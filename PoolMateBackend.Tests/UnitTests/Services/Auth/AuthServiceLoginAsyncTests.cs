using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Integrations.Email;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.Auth
{
    /// <summary>
    /// Unit Tests for AuthService.LoginAsync
    /// Method: Solitary Unit Testing with Mocks
    /// </summary>
    public class AuthServiceLoginAsyncTests
    {
        // ============================================
        // SECTION 1: MOCK OBJECTS DECLARATION
        // ============================================
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IConfiguration> _mockConfiguration;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly AuthService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public AuthServiceLoginAsyncTests()
        {
            // Initialize Mock UserManager (requires special setup)
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Initialize Mock RoleManager (requires special setup)
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            var mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStore.Object, null!, null!, null!, null!);

            // Initialize Mock Configuration with JWT settings
            _mockConfiguration = new Mock<IConfiguration>();
            SetupJwtConfiguration();

            // Initialize Mock EmailSender
            var mockEmailSender = new Mock<IEmailSender>();

            // Inject Mocks into the Service (Dependency Injection)
            _sut = new AuthService(
                _mockUserManager.Object,
                mockRoleManager.Object,
                _mockConfiguration.Object,
                mockEmailSender.Object);
        }

        private void SetupJwtConfiguration()
        {
            _mockConfiguration.Setup(x => x["JWT:ValidIssuer"]).Returns("TestIssuer");
            _mockConfiguration.Setup(x => x["JWT:ValidAudience"]).Returns("TestAudience");
            _mockConfiguration.Setup(x => x["JWT:Secret"]).Returns("ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256Algorithm");
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Model is null

        /// <summary>
        /// Test Case #1: When model is null, throws ArgumentNullException
        /// </summary>
        /// <remarks>
        /// Input: model = null
        /// Expected: Throw ArgumentNullException
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenModelIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            LoginModel model = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.LoginAsync(model));
        }

        #endregion

        #region Test Case #2: Username null/empty/whitespace

        /// <summary>
        /// Test Case #2: When username is null, empty, or whitespace, throws InvalidOperationException
        /// </summary>
        /// <remarks>
        /// Input: model.Username = null, "", or "   "
        /// Expected: Throw InvalidOperationException("Invalid username or password.")
        /// </remarks>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task LoginAsync_WhenUsernameIsNullOrEmpty_ThrowsInvalidOperationException(string? username)
        {
            // Arrange
            var model = new LoginModel { Username = username, Password = "anyPassword" };
            _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoginAsync(model));
            Assert.Equal("Invalid username or password.", exception.Message);
        }

        #endregion

        #region Test Case #3: User not found

        /// <summary>
        /// Test Case #3: When user is not found in database, throws InvalidOperationException
        /// </summary>
        /// <remarks>
        /// Input: model.Username = "nonexistent", FindByNameAsync returns null
        /// Expected: Throw InvalidOperationException("Invalid username or password.")
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenUserNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var model = new LoginModel { Username = "nonexistent", Password = "password123" };
            _mockUserManager.Setup(x => x.FindByNameAsync("nonexistent"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoginAsync(model));
            Assert.Equal("Invalid username or password.", exception.Message);
        }

        #endregion

        #region Test Case #4: Lockout in future (Boundary - Above boundary - LOCKED)

        /// <summary>
        /// Test Case #4: When LockoutEnd is in the future (above boundary), user is locked
        /// </summary>
        /// <remarks>
        /// Input: user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1)
        /// Expected: Throw InvalidOperationException("This account has been locked. Please contact administrator.")
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenLockoutEndIsInFuture_ThrowsInvalidOperationException()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-id",
                UserName = "testuser",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1) // LOCKED - above boundary
            };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoginAsync(model));
            Assert.Equal("This account has been locked. Please contact administrator.", exception.Message);
        }

        #endregion

        #region Test Case #5: Lockout equals now (Boundary - At boundary - NOT LOCKED)

        /// <summary>
        /// Test Case #5: When LockoutEnd equals UtcNow (at boundary), user is NOT locked (because > not >=)
        /// </summary>
        /// <remarks>
        /// Input: user.LockoutEnd = DateTimeOffset.UtcNow (exactly), password correct, email confirmed
        /// Expected: Return valid token tuple
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenLockoutEndEqualsNow_ReturnsToken()
        {
            // Arrange
            var lockoutTime = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = lockoutTime, // At boundary - NOT locked (> not >=)
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            Assert.Equal("testuser", result.Value.UserName);
            Assert.NotEmpty(result.Value.Token);
        }

        #endregion

        #region Test Case #6: Lockout in past (Boundary - Below boundary - NOT LOCKED)

        /// <summary>
        /// Test Case #6: When LockoutEnd is in the past (below boundary), user is NOT locked
        /// </summary>
        /// <remarks>
        /// Input: user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1), password correct, email confirmed
        /// Expected: Return valid token tuple
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenLockoutEndIsInPast_ContinuesLogin()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1), // Below boundary - NOT locked
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            Assert.NotEmpty(result.Value.Token);
            Assert.True(result.Value.Exp > DateTime.UtcNow);
        }

        #endregion

        #region Test Case #7: LockoutEnd is null - Continues login

        /// <summary>
        /// Test Case #7: When LockoutEnd is null, login continues normally
        /// </summary>
        /// <remarks>
        /// Input: user.LockoutEnd = null, password correct, email confirmed
        /// Expected: Return valid token tuple
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenLockoutEndIsNull_ContinuesLogin()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = null, // LockoutEnd is null
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            Assert.Equal("testuser", result.Value.UserName);
            Assert.Equal("test@example.com", result.Value.Email);
            Assert.NotEmpty(result.Value.Token);
        }

        #endregion

        #region Test Case #8: Password incorrect

        /// <summary>
        /// Test Case #8: When password is incorrect, throws InvalidOperationException
        /// </summary>
        /// <remarks>
        /// Input: CheckPasswordAsync returns false
        /// Expected: Throw InvalidOperationException("Invalid username or password.")
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenPasswordIsIncorrect_ThrowsInvalidOperationException()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var model = new LoginModel { Username = "testuser", Password = "wrongpassword" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "wrongpassword")).ReturnsAsync(false);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoginAsync(model));
            Assert.Equal("Invalid username or password.", exception.Message);
        }

        #endregion

        #region Test Case #9: Email not confirmed

        /// <summary>
        /// Test Case #9: When email is not confirmed, throws InvalidOperationException
        /// </summary>
        /// <remarks>
        /// Input: user.EmailConfirmed = false, password correct
        /// Expected: Throw InvalidOperationException("Email is not confirmed.")
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenEmailNotConfirmed_ThrowsInvalidOperationException()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = null,
                EmailConfirmed = false // Email NOT confirmed
            };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoginAsync(model));
            Assert.Equal("Email is not confirmed.", exception.Message);
        }

        #endregion

        #region Test Case #10: Happy path - All valid, returns complete token data

        /// <summary>
        /// Test Case #10: Happy path - When all data is valid, returns complete token with correct data
        /// </summary>
        /// <remarks>
        /// Input: User exists, not locked, password correct, email confirmed, has roles
        /// Expected: Return tuple with Token, Exp, UserId, UserName, Email, Roles
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenAllValid_ReturnsTokenWithCorrectData()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            Assert.Equal("testuser", result.Value.UserName);
            Assert.Equal("test@example.com", result.Value.Email);
            Assert.Contains("Player", result.Value.Roles);
            Assert.NotEmpty(result.Value.Token);
            Assert.True(result.Value.Exp > DateTime.UtcNow);

            // Verify token is valid JWT
            var handler = new JwtSecurityTokenHandler();
            Assert.True(handler.CanReadToken(result.Value.Token));
        }

        #endregion

        #region Test Case #11: User has no roles - Returns empty roles

        /// <summary>
        /// Test Case #11: When user has no roles, returns token with empty roles list
        /// </summary>
        /// <remarks>
        /// Input: User valid, GetRolesAsync returns empty list
        /// Expected: Return tuple with Roles = []
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenUserHasNoRoles_ReturnsTokenWithEmptyRoles()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "testuser",
                Email = "test@example.com",
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var roles = new List<string>(); // Empty roles
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Value.Roles);
            Assert.NotEmpty(result.Value.Token);
        }

        #endregion

        #region Test Case #12: User has multiple roles - Returns all roles

        /// <summary>
        /// Test Case #12: When user has multiple roles, returns all roles in claims
        /// </summary>
        /// <remarks>
        /// Input: User valid, GetRolesAsync returns ["Admin", "Player"]
        /// Expected: Return tuple with all roles
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenUserHasMultipleRoles_ReturnsAllRoles()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "adminuser",
                Email = "admin@example.com",
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var roles = new List<string> { "Admin", "Player" }; // Multiple roles
            var model = new LoginModel { Username = "adminuser", Password = "password123" };

            _mockUserManager.Setup(x => x.FindByNameAsync("adminuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Value.Roles.Count);
            Assert.Contains("Admin", result.Value.Roles);
            Assert.Contains("Player", result.Value.Roles);

            // Verify JWT contains all role claims
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(result.Value.Token);
            var roleClaims = token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            Assert.Contains("Admin", roleClaims);
            Assert.Contains("Player", roleClaims);
        }

        #endregion

        #region Test Case #13: Username with whitespace - Trims and finds user

        /// <summary>
        /// Test Case #13: When username has leading/trailing whitespace, trims before finding user
        /// </summary>
        /// <remarks>
        /// Input: model.Username = "  validuser  ", user exists
        /// Expected: Trim username before finding, return token
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenUsernameHasWhitespace_TrimsAndFindsUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = "validuser",
                Email = "valid@example.com",
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "  validuser  ", Password = "password123" }; // Whitespace around

            // Setup expects trimmed username
            _mockUserManager.Setup(x => x.FindByNameAsync("validuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            
            // Verify FindByNameAsync was called with trimmed username
            _mockUserManager.Verify(x => x.FindByNameAsync("validuser"), Times.Once);
        }

        #endregion

        #region Test Case #14: UserName and Email are null - Returns empty strings in claims

        /// <summary>
        /// Test Case #14: When user.UserName and user.Email are null, returns token with empty strings in claims
        /// </summary>
        /// <remarks>
        /// Input: user.UserName = null, user.Email = null (but Id exists)
        /// Expected: Return token with claims containing empty string for Name and Email
        /// </remarks>
        [Fact]
        public async Task LoginAsync_WhenUserNameOrEmailIsNull_ReturnsTokenWithEmptyStringsInClaims()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-123",
                UserName = null, // null UserName
                Email = null,    // null Email
                LockoutEnd = null,
                EmailConfirmed = true
            };
            var roles = new List<string> { "Player" };
            var model = new LoginModel { Username = "testuser", Password = "password123" };

            // FindByNameAsync can still find user (by some internal mechanism or ID mapping)
            _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var result = await _sut.LoginAsync(model);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user-123", result.Value.UserId);
            Assert.Null(result.Value.UserName); // The method returns user.UserName directly
            Assert.Null(result.Value.Email);    // The method returns user.Email directly

            // Verify JWT token contains empty strings in claims (not null)
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(result.Value.Token);
            
            var nameClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            var emailClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            
            Assert.NotNull(nameClaim);
            Assert.Equal(string.Empty, nameClaim.Value); // ?? string.Empty applied
            Assert.NotNull(emailClaim);
            Assert.Equal(string.Empty, emailClaim.Value); // ?? string.Empty applied
        }

        #endregion
    }
}

