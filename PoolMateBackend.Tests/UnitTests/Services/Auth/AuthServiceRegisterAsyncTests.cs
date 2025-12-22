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
    /// Unit Tests for AuthService.RegisterAsync
    /// Method: Solitary Unit Testing with Mocks
    /// </summary>
    public class AuthServiceRegisterAsyncTests
    {
        // ============================================
        // SECTION 1: MOCK OBJECTS DECLARATION
        // ============================================
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IEmailSender> _mockEmailSender;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly AuthService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public AuthServiceRegisterAsyncTests()
        {
            // Initialize Mock UserManager (requires special setup)
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Initialize Mock RoleManager (requires special setup)
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStore.Object, null!, null!, null!, null!);

            // Initialize Mock Configuration with JWT settings (for AuthService constructor)
            _mockConfiguration = new Mock<IConfiguration>();
            SetupJwtConfiguration();

            // Initialize Mock EmailSender
            _mockEmailSender = new Mock<IEmailSender>();

            // Inject Mocks into the Service (Dependency Injection)
            _sut = new AuthService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _mockConfiguration.Object,
                _mockEmailSender.Object);
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

        #region Test Case #1: User already exists

        /// <summary>
        /// Test Case #1: When username already exists, returns error
        /// </summary>
        /// <remarks>
        /// Input: model.Username = "existinguser", FindByNameAsync returns existing user
        /// Expected: Response.Error("User already exists!")
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenUserAlreadyExists_ReturnsError()
        {
            // Arrange
            var existingUser = new ApplicationUser { Id = "existing-id", UserName = "existinguser" };
            var model = new RegisterModel 
            { 
                Username = "existinguser", 
                Email = "test@test.com", 
                Password = "Pass123!" 
            };

            _mockUserManager.Setup(x => x.FindByNameAsync("existinguser"))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Error", result.Status);
            Assert.Equal("User already exists!", result.Message);

            // Verify CreateAsync was NOT called
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Test Case #2: Create user fails (single error)

        /// <summary>
        /// Test Case #2: When CreateAsync fails with single error, returns error with description
        /// </summary>
        /// <remarks>
        /// Input: CreateAsync returns IdentityResult.Failed with "Password too weak"
        /// Expected: Response.Error("Password too weak")
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenCreateUserFails_ReturnsErrorWithDescription()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "weak" 
            };

            _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
                .ReturnsAsync((ApplicationUser?)null);

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "weak"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Error", result.Status);
            Assert.Equal("Password too weak", result.Message);

            // Verify AddToRoleAsync was NOT called (user creation failed)
            _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Test Case #3: Create user fails (multiple errors)

        /// <summary>
        /// Test Case #3: When CreateAsync fails with multiple errors, returns joined error messages
        /// </summary>
        /// <remarks>
        /// Input: CreateAsync returns failed with ["Password too weak", "Invalid email format"]
        /// Expected: Response.Error("Password too weak; Invalid email format")
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenCreateUserFailsWithMultipleErrors_ReturnsJoinedErrors()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "invalid", 
                Password = "weak" 
            };

            _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
                .ReturnsAsync((ApplicationUser?)null);

            var errors = new[]
            {
                new IdentityError { Description = "Password too weak" },
                new IdentityError { Description = "Invalid email format" }
            };
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(errors));

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Error", result.Status);
            Assert.Equal("Password too weak; Invalid email format", result.Message);
        }

        #endregion

        #region Test Case #4: Role not exists - creates role

        /// <summary>
        /// Test Case #4: When PLAYER role does not exist, creates role and returns Ok
        /// </summary>
        /// <remarks>
        /// Input: RoleExistsAsync returns false
        /// Expected: Response.Ok(...), verify CreateAsync(IdentityRole) was called
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenRoleNotExists_CreatesRoleAndReturnsOk()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };

            SetupSuccessfulRegistrationMocks(model);

            // Override: Role does NOT exist
            _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
                .ReturnsAsync(false);
            _mockRoleManager.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Success", result.Status);
            
            // Verify CreateAsync for role was called with PLAYER role
            _mockRoleManager.Verify(
                x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.PLAYER)), 
                Times.Once);
        }

        #endregion

        #region Test Case #5: Role exists - skips role creation

        /// <summary>
        /// Test Case #5: When PLAYER role already exists, skips role creation and returns Ok
        /// </summary>
        /// <remarks>
        /// Input: RoleExistsAsync returns true
        /// Expected: Response.Ok(...), verify CreateAsync(IdentityRole) was NOT called
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenRoleExists_SkipsRoleCreationAndReturnsOk()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };

            SetupSuccessfulRegistrationMocks(model);

            // Role EXISTS
            _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Success", result.Status);
            
            // Verify CreateAsync for role was NOT called
            _mockRoleManager.Verify(x => x.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
        }

        #endregion

        #region Test Case #6: Happy path - complete flow

        /// <summary>
        /// Test Case #6: When all valid, returns success message
        /// </summary>
        /// <remarks>
        /// Input: New user, valid password, role exists
        /// Expected: Response.Ok("User created. Please check your email to confirm.")
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_WhenAllValid_ReturnsOkMessage()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };

            SetupSuccessfulRegistrationMocks(model);

            // Act
            var result = await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.Equal("Success", result.Status);
            Assert.Equal("User created. Please check your email to confirm.", result.Message);
        }

        #endregion

        #region Test Case #7: Verify user properties

        /// <summary>
        /// Test Case #7: Verify ApplicationUser is created with correct properties
        /// </summary>
        /// <remarks>
        /// Input: Valid model { Username = "newuser", Email = "new@test.com", Password = "Pass123!" }
        /// Expected: ApplicationUser has UserName, Email, SecurityStamp not null/empty
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_VerifyUserCreatedWithCorrectProperties()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };
            ApplicationUser? capturedUser = null;

            _mockUserManager.Setup(x => x.FindByNameAsync("newuser"))
                .ReturnsAsync((ApplicationUser?)null);

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((user, _) => capturedUser = user)
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER))
                .ReturnsAsync(IdentityResult.Success);

            _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("test-token");

            _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
                .ReturnsAsync(true);

            _mockEmailSender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.RegisterAsync(model, "https://example.com");

            // Assert
            Assert.NotNull(capturedUser);
            Assert.Equal("newuser", capturedUser.UserName);
            Assert.Equal("new@test.com", capturedUser.Email);
            Assert.NotNull(capturedUser.SecurityStamp);
            Assert.NotEmpty(capturedUser.SecurityStamp);
        }

        #endregion

        #region Test Case #8: Verify AddToRoleAsync called with PLAYER role

        /// <summary>
        /// Test Case #8: Verify AddToRoleAsync is called with UserRoles.PLAYER
        /// </summary>
        /// <remarks>
        /// Input: Valid registration
        /// Expected: AddToRoleAsync(user, UserRoles.PLAYER) is called exactly once
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_VerifyAddToRoleAsyncCalledWithPlayerRole()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };

            SetupSuccessfulRegistrationMocks(model);

            // Act
            await _sut.RegisterAsync(model, "https://example.com");

            // Assert - Verify AddToRoleAsync was called with PLAYER role
            _mockUserManager.Verify(
                x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER), 
                Times.Once);
        }

        #endregion

        #region Test Case #9: Verify SendEmailConfirmationAsync called

        /// <summary>
        /// Test Case #9: Verify email confirmation is sent after successful registration
        /// </summary>
        /// <remarks>
        /// Input: Valid registration with baseUri = "https://example.com"
        /// Expected: Email is sent with correct user email and confirmation URL
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_VerifySendEmailConfirmationAsyncCalled()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };
            var baseUri = "https://example.com";

            SetupSuccessfulRegistrationMocks(model);

            // Act
            await _sut.RegisterAsync(model, baseUri);

            // Assert - Verify email was sent to correct address
            _mockEmailSender.Verify(
                x => x.SendAsync(
                    "new@test.com",                    // To email
                    "Confirm your email",              // Subject
                    It.Is<string>(body => body.Contains("Hi newuser") && body.Contains(baseUri)), // Body contains user name and base URI
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Test Case #10 (Additional): Verify GenerateEmailConfirmationTokenAsync called

        /// <summary>
        /// Test Case #10: Verify GenerateEmailConfirmationTokenAsync is called
        /// </summary>
        /// <remarks>
        /// Input: Valid registration
        /// Expected: GenerateEmailConfirmationTokenAsync is called for the created user
        /// </remarks>
        [Fact]
        public async Task RegisterAsync_VerifyGenerateEmailConfirmationTokenAsyncCalled()
        {
            // Arrange
            var model = new RegisterModel 
            { 
                Username = "newuser", 
                Email = "new@test.com", 
                Password = "Pass123!" 
            };

            SetupSuccessfulRegistrationMocks(model);

            // Act
            await _sut.RegisterAsync(model, "https://example.com");

            // Assert - Verify token generation was called
            _mockUserManager.Verify(
                x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()), 
                Times.Once);
        }

        #endregion

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Sets up all mocks for a successful registration flow
        /// </summary>
        private void SetupSuccessfulRegistrationMocks(RegisterModel model)
        {
            // User does not exist
            _mockUserManager.Setup(x => x.FindByNameAsync(model.Username))
                .ReturnsAsync((ApplicationUser?)null);

            // User creation succeeds
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Success);

            // Add to role succeeds
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), UserRoles.PLAYER))
                .ReturnsAsync(IdentityResult.Success);

            // Generate email token
            _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("test-confirmation-token");

            // Role exists
            _mockRoleManager.Setup(x => x.RoleExistsAsync(UserRoles.PLAYER))
                .ReturnsAsync(true);

            // Email sender
            _mockEmailSender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
    }
}

