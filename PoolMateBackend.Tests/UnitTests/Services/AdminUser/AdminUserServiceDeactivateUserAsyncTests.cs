using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.AdminUser
{
    /// <summary>
    /// Unit Tests for AdminUserService.DeactivateUserAsync
    /// Method: Solitary Unit Testing with Mocks
    /// Total Test Cases: 12 (based on DeactivateUserAsync_TestCases.md)
    /// </summary>
    public class AdminUserServiceDeactivateUserAsyncTests : IDisposable
    {
        // ============================================
        // SECTION 1: FIELDS
        // ============================================
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<ILogger<AdminUserService>> _mockLogger;
        private readonly ApplicationDbContext _dbContext;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly AdminUserService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public AdminUserServiceDeactivateUserAsyncTests()
        {
            // Mock UserManager
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Mock RoleManager
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStore.Object, null!, null!, null!, null!);

            // Mock Logger
            _mockLogger = new Mock<ILogger<AdminUserService>>();

            // Use InMemory Database for ApplicationDbContext
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _dbContext = new ApplicationDbContext(options);

            // Inject dependencies into the Service
            _sut = new AdminUserService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _dbContext,
                _mockLogger.Object);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Admin deactivates self - Returns error

        /// <summary>
        /// Test Case #1: When admin tries to deactivate themselves, returns error
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenAdminDeactivatesSelf_ReturnsError()
        {
            // Arrange
            var userId = "admin-123";
            var adminId = "admin-123"; // SAME as userId

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("You cannot deactivate your own account.", result.Message);
            
            // Verify FindByIdAsync was NOT called (short-circuit)
            _mockUserManager.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Test Case #2: User not found - Returns error

        /// <summary>
        /// Test Case #2: When user is not found, returns error
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenUserNotFound_ReturnsError()
        {
            // Arrange
            var userId = "non-existent";
            var adminId = "admin-456";
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
        }

        #endregion

        #region Test Case #3: User already deactivated - Returns error

        /// <summary>
        /// Test Case #3: When user is already deactivated (LockoutEnd in future), returns error
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenUserAlreadyDeactivated_ReturnsError()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(1), // Future = already locked
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User is already deactivated", result.Message);
            
            // Verify UpdateSecurityStampAsync was NOT called
            _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        #endregion

        #region Test Case #4: LockoutEnd is exactly now - Continues processing (Boundary)

        /// <summary>
        /// Test Case #4: When LockoutEnd is exactly at UtcNow, continues processing (boundary test)
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenLockoutEndIsExactlyNow_ContinuesProcessing()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = now, // EXACTLY at boundary - should NOT be considered "already deactivated"
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success); // Should continue and succeed
        }

        #endregion

        #region Test Case #5: LockoutEnd is in past - Continues processing (Boundary)

        /// <summary>
        /// Test Case #5: When LockoutEnd is in the past, continues processing (boundary test)
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenLockoutEndIsInPast_ContinuesProcessing()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1), // Past = not currently locked
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success); // Should continue and succeed
        }

        #endregion

        #region Test Case #6: LockoutEnd is in future - Returns error (Boundary)

        /// <summary>
        /// Test Case #6: When LockoutEnd is in the future, returns error (boundary test)
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenLockoutEndIsInFuture_ReturnsError()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1), // Future = already locked
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User is already deactivated", result.Message);
        }

        #endregion

        #region Test Case #7: LockoutEnd is null - Continues processing (Edge)

        /// <summary>
        /// Test Case #7: When LockoutEnd is null, continues processing (HasValue = false short-circuits)
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenLockoutEndIsNull_ContinuesProcessing()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = null, // NULL = never locked
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success); // Should continue and succeed
        }

        #endregion

        #region Test Case #8: User is protected (LockoutEnabled = false) - Returns error

        /// <summary>
        /// Test Case #8: When user is protected (LockoutEnabled = false), returns error
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenUserIsProtected_ReturnsError()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "superadmin",
                LockoutEnd = null,
                LockoutEnabled = false // PROTECTED!
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Cannot deactivate this user", result.Message);
            Assert.Contains("protected account", result.Message);
        }

        #endregion

        #region Test Case #9: UpdateSecurityStampAsync fails - Returns error

        /// <summary>
        /// Test Case #9: When UpdateSecurityStampAsync fails, returns error with details
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenUpdateSecurityStampFails_ReturnsError()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = null,
                LockoutEnabled = true
            };
            
            var identityErrors = new[] { new IdentityError { Description = "Database error" } };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to deactivate user", result.Message);
            Assert.Contains("Database error", result.Message);
        }

        #endregion

        #region Test Case #10: Happy path - Returns success with data

        /// <summary>
        /// Test Case #10: When all conditions are valid, returns success with data
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenAllValid_ReturnsSuccessWithData()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = null,
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Player" });

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        #endregion

        #region Test Case #11: Success - Sets LockoutEnd to MaxValue

        /// <summary>
        /// Test Case #11: When deactivation succeeds, LockoutEnd is set to DateTimeOffset.MaxValue
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenSuccess_SetsLockoutEndToMaxValue()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "testuser",
                LockoutEnd = null,
                LockoutEnabled = true
            };
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
                .Callback<ApplicationUser>(u => 
                {
                    // Verify LockoutEnd is set BEFORE UpdateSecurityStampAsync is called
                    Assert.Equal(DateTimeOffset.MaxValue, u.LockoutEnd);
                })
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        }

        #endregion

        #region Test Case #12: Exception thrown - Returns generic error

        /// <summary>
        /// Test Case #12: When exception is thrown, returns generic error message
        /// </summary>
        [Fact]
        public async Task DeactivateUserAsync_WhenExceptionThrown_ReturnsGenericError()
        {
            // Arrange
            var userId = "user-123";
            var adminId = "admin-456";
            
            _mockUserManager.Setup(x => x.FindByIdAsync(userId))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Error deactivating user", result.Message);
        }

        #endregion
    }
}

