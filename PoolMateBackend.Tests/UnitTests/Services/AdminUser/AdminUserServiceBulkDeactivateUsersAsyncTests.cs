using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.AdminUser
{
    /// <summary>
    /// Unit Tests for AdminUserService.BulkDeactivateUsersAsync
    /// Method: Solitary Unit Testing with Mocks
    /// Total Test Cases: 17 (based on BulkDeactivateUsersAsync_TestCases.md)
    /// </summary>
    public class AdminUserServiceBulkDeactivateUsersAsyncTests : IDisposable
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
        public AdminUserServiceBulkDeactivateUsersAsyncTests()
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

        #region Test Case #1: All users valid - Deactivates all

        /// <summary>
        /// Test Case #1: When all users are valid, deactivates all successfully
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenAllUsersValid_DeactivatesAll()
        {
            // Arrange
            var adminId = "admin-999";
            var users = new List<ApplicationUser>
            {
                new() { Id = "user-1", UserName = "user1", LockoutEnabled = true, LockoutEnd = null },
                new() { Id = "user-2", UserName = "user2", LockoutEnabled = true, LockoutEnd = null },
                new() { Id = "user-3", UserName = "user3", LockoutEnabled = true, LockoutEnd = null }
            };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1", "user-2", "user-3" },
                Reason = "Test cleanup"
            };

            foreach (var user in users)
            {
                _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
                _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            }

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(3, data.TotalRequested);
            Assert.Equal(3, data.SuccessCount);
            Assert.Equal(0, data.FailedCount);
            Assert.Equal(0, data.SkippedCount);
            Assert.All(data.Results, r => Assert.Equal("Success", r.Status));
        }

        #endregion

        #region Test Case #2: Admin in list - Skips self and continues

        /// <summary>
        /// Test Case #2: When admin is in the user list, skips self and continues with others
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenAdminInList_SkipsSelfAndContinues()
        {
            // Arrange
            var adminId = "admin-1";
            var validUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "admin-1", "user-2" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(validUser);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal(1, data.SuccessCount);

            var adminResult = data.Results.First(r => r.UserId == "admin-1");
            Assert.Equal("Skipped", adminResult.Status);
            Assert.Equal("Cannot deactivate your own account", adminResult.ErrorMessage);
        }

        #endregion

        #region Test Case #3: User not found - Marks as failed

        /// <summary>
        /// Test Case #3: When user is not found, marks as failed
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenUserNotFound_MarksAsFailed()
        {
            // Arrange
            var adminId = "admin-999";
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "non-existent" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("non-existent"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(0, data.SuccessCount);

            var failedResult = data.Results.First();
            Assert.Equal("Failed", failedResult.Status);
            Assert.Equal("User not found", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #4: User already deactivated - Skips user

        /// <summary>
        /// Test Case #4: When user is already deactivated (LockoutEnd in future), skips user
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenUserAlreadyDeactivated_SkipsUser()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddDays(1) // Already locked
            };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal(0, data.SuccessCount);

            var skippedResult = data.Results.First();
            Assert.Equal("Skipped", skippedResult.Status);
            Assert.Equal("User is already deactivated", skippedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #5: LockoutEnd exactly now - Continues deactivation (Boundary)

        /// <summary>
        /// Test Case #5: When LockoutEnd is exactly at UtcNow, continues deactivation (boundary)
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenLockoutEndExactlyNow_ContinuesDeactivation()
        {
            // Arrange
            var adminId = "admin-999";
            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = now // EXACTLY at boundary
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SuccessCount);
            Assert.Equal(0, data.SkippedCount);
        }

        #endregion

        #region Test Case #6: LockoutEnd in past - Continues deactivation (Boundary)

        /// <summary>
        /// Test Case #6: When LockoutEnd is in the past, continues deactivation (boundary)
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenLockoutEndInPast_ContinuesDeactivation()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1) // Past
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SuccessCount);
            Assert.Equal(0, data.SkippedCount);
        }

        #endregion

        #region Test Case #7: LockoutEnd in future - Skips user (Boundary)

        /// <summary>
        /// Test Case #7: When LockoutEnd is in the future, skips user (boundary)
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenLockoutEndInFuture_SkipsUser()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1) // Future
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal("Skipped", data.Results.First().Status);
        }

        #endregion

        #region Test Case #8: Protected account and no Force - Skips user

        /// <summary>
        /// Test Case #8: When user is protected (LockoutEnabled = false) and no Force, skips user
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenProtectedAccountAndNoForce_SkipsUser()
        {
            // Arrange
            var adminId = "admin-999";
            var protectedUser = new ApplicationUser
            {
                Id = "user-1",
                UserName = "superadmin",
                LockoutEnabled = false, // PROTECTED
                LockoutEnd = null
            };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Force = false // No force
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(protectedUser);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Contains("Protected account", data.Results.First().ErrorMessage);
        }

        #endregion

        #region Test Case #9: Protected account and Force - Deactivates user

        /// <summary>
        /// Test Case #9: When user is protected but Force = true, deactivates user
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenProtectedAccountAndForce_DeactivatesUser()
        {
            // Arrange
            var adminId = "admin-999";
            var protectedUser = new ApplicationUser
            {
                Id = "user-1",
                UserName = "superadmin",
                LockoutEnabled = false, // PROTECTED
                LockoutEnd = null
            };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Force = true // FORCE override
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(protectedUser);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(protectedUser)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SuccessCount);
            Assert.Equal(0, data.SkippedCount);
        }

        #endregion

        #region Test Case #10: UpdateSecurityStamp fails - Marks as failed

        /// <summary>
        /// Test Case #10: When UpdateSecurityStampAsync fails, marks user as failed
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenUpdateSecurityStampFails_MarksAsFailed()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = null
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            var identityErrors = new[] { new IdentityError { Description = "Database error" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(0, data.SuccessCount);

            var failedResult = data.Results.First();
            Assert.Equal("Failed", failedResult.Status);
            Assert.Contains("Database error", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #11: Inner exception thrown - Marks as failed and continues

        /// <summary>
        /// Test Case #11: When inner exception is thrown for one user, marks as failed and continues with others
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenInnerExceptionThrown_MarksAsFailedAndContinues()
        {
            // Arrange
            var adminId = "admin-999";
            var validUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1", "user-2" }
            };

            // user-1 throws exception
            _mockUserManager.Setup(x => x.FindByIdAsync("user-1"))
                .ThrowsAsync(new Exception("Database error"));

            // user-2 is valid
            _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(validUser);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success); // Overall operation succeeds
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(1, data.SuccessCount);

            var failedResult = data.Results.First(r => r.UserId == "user-1");
            Assert.Equal("Failed", failedResult.Status);
            Assert.Equal("Internal error", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #12: Outer exception thrown - Returns error

        /// <summary>
        /// Test Case #12: When outer exception is thrown, returns error response
        /// Note: This is hard to simulate as the outer try-catch wraps everything
        /// We simulate by making the request.UserIds throw when accessed
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenOuterExceptionThrown_ReturnsError()
        {
            // Arrange
            var adminId = "admin-999";
            // Create a request that will throw in the foreach
            var mockUserIds = new Mock<List<string>>();

            // Since we can't easily mock List<string>.GetEnumerator to throw,
            // we'll verify the error path indirectly by checking error handling exists
            // In practice, this would require a more complex setup

            var request = new BulkDeactivateUsersDto
            {
                UserIds = null! // This will cause NullReferenceException in foreach
            };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Error processing bulk deactivate operation", result.Message);
        }

        #endregion

        #region Test Case #13: Mixed results - Returns correct counts

        /// <summary>
        /// Test Case #13: When mixed results (valid, not found, already deactivated, protected), returns correct counts
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts()
        {
            // Arrange
            var adminId = "admin-999";

            var validUser = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnabled = true, LockoutEnd = null };
            var alreadyDeactivated = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnabled = true, LockoutEnd = DateTimeOffset.MaxValue };
            var protectedUser = new ApplicationUser { Id = "user-3", UserName = "superadmin", LockoutEnabled = false, LockoutEnd = null };

            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1", "user-not-found", "user-2", "user-3" },
                Force = false
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(validUser);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-not-found")).ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(alreadyDeactivated);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-3")).ReturnsAsync(protectedUser);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(validUser)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(4, data.TotalRequested);
            Assert.Equal(1, data.SuccessCount);   // user-1
            Assert.Equal(1, data.FailedCount);    // user-not-found
            Assert.Equal(2, data.SkippedCount);   // user-2 (already deactivated), user-3 (protected)
        }

        #endregion

        #region Test Case #14: Returns correct bulk result structure

        /// <summary>
        /// Test Case #14: Verifies the response contains correct bulk result structure
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_ReturnsCorrectBulkResultStructure()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnabled = true, LockoutEnd = null };
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Reason = "Cleanup operation"
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            var beforeTest = DateTime.UtcNow;

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);

            Assert.Equal(1, data.TotalRequested);
            Assert.Equal("Cleanup operation", data.Reason);
            Assert.True(data.ProcessedAt >= beforeTest);
            Assert.True(data.ProcessedAt <= DateTime.UtcNow.AddSeconds(5));
            Assert.Single(data.Results);
        }

        #endregion

        #region Test Case #15: Empty user IDs list - Returns empty result

        /// <summary>
        /// Test Case #15: When user IDs list is empty, returns empty result
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenEmptyUserIdsList_ReturnsEmptyResult()
        {
            // Arrange
            var adminId = "admin-999";
            var request = new BulkDeactivateUsersDto
            {
                UserIds = new List<string>() // Empty list
            };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(0, data.TotalRequested);
            Assert.Equal(0, data.SuccessCount);
            Assert.Equal(0, data.FailedCount);
            Assert.Equal(0, data.SkippedCount);
            Assert.Empty(data.Results);
        }

        #endregion

        #region Test Case #16: LockoutEnd is null - Continues deactivation

        /// <summary>
        /// Test Case #16: When LockoutEnd is null, continues deactivation (HasValue = false short-circuits)
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_WhenLockoutEndIsNull_ContinuesDeactivation()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = null // NULL
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SuccessCount);
        }

        #endregion

        #region Test Case #17: Success - Sets LockoutEnd to MaxValue

        /// <summary>
        /// Test Case #17: When deactivation succeeds, LockoutEnd is set to DateTimeOffset.MaxValue
        /// </summary>
        [Fact]
        public async Task BulkDeactivateUsersAsync_SetsLockoutEndToMaxValueOnSuccess()
        {
            // Arrange
            var adminId = "admin-999";
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnabled = true,
                LockoutEnd = null
            };
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
                .Callback<ApplicationUser>(u =>
                {
                    // Verify LockoutEnd is set BEFORE UpdateSecurityStampAsync is called
                    Assert.Equal(DateTimeOffset.MaxValue, u.LockoutEnd);
                })
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        }

        #endregion
    }
}

