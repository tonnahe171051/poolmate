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
    /// Unit Tests for AdminUserService.BulkReactivateUsersAsync
    /// Method: Solitary Unit Testing with Mocks
    /// Total Test Cases: 16 (based on BulkReactivateUsersAsync_TestCases.md)
    /// </summary>
    public class AdminUserServiceBulkReactivateUsersAsyncTests : IDisposable
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
        public AdminUserServiceBulkReactivateUsersAsyncTests()
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

        #region Test Case #1: All users deactivated - Reactivates all

        /// <summary>
        /// Test Case #1: When all users are deactivated, reactivates all successfully
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenAllUsersDeactivated_ReactivatesAll()
        {
            // Arrange
            var users = new List<ApplicationUser>
            {
                new() { Id = "user-1", UserName = "user1", LockoutEnd = DateTimeOffset.MaxValue },
                new() { Id = "user-2", UserName = "user2", LockoutEnd = DateTimeOffset.MaxValue },
                new() { Id = "user-3", UserName = "user3", LockoutEnd = DateTimeOffset.MaxValue }
            };
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1", "user-2", "user-3" },
                Reason = "Batch reactivation"
            };

            foreach (var user in users)
            {
                _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
                _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
            }

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(3, data.TotalRequested);
            Assert.Equal(3, data.SuccessCount);
            Assert.Equal(0, data.FailedCount);
            Assert.Equal(0, data.SkippedCount);
            Assert.All(data.Results, r => Assert.Equal("Success", r.Status));

            // Verify LockoutEnd was set to null
            foreach (var user in users)
            {
                Assert.Null(user.LockoutEnd);
            }
        }

        #endregion

        #region Test Case #2: User not found - Marks as failed

        /// <summary>
        /// Test Case #2: When user is not found, marks as failed
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenUserNotFound_MarksAsFailed()
        {
            // Arrange
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "non-existent" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("non-existent"))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(0, data.SuccessCount);

            var failedResult = data.Results.First();
            Assert.Equal("Failed", failedResult.Status);
            Assert.Equal("User not found", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #3: User not deactivated - Skips user

        /// <summary>
        /// Test Case #3: When user is not currently deactivated (LockoutEnd is null), skips user
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenUserNotDeactivated_SkipsUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = null // NOT deactivated
            };
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal(0, data.SuccessCount);
            Assert.Equal("Skipped", data.Results.First().Status);
            Assert.Equal("User is not currently deactivated", data.Results.First().ErrorMessage);

            // Verify UpdateSecurityStampAsync was NOT called
            _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        #endregion

        #region Test Case #4: LockoutEnd is null - Skips user (Boundary)

        /// <summary>
        /// Test Case #4: When LockoutEnd is null (HasValue = false), skips user (short-circuit)
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenLockoutEndIsNull_SkipsUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = null // NULL = short-circuit at !HasValue
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal(0, data.SuccessCount);
        }

        #endregion

        #region Test Case #5: LockoutEnd exactly now - Skips user (Boundary)

        /// <summary>
        /// Test Case #5: When LockoutEnd is exactly at UtcNow, skips user (boundary <= passes)
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenLockoutEndExactlyNow_SkipsUser()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = now // EXACTLY at boundary
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount); // Should skip because <= passes at exact boundary
            Assert.Equal(0, data.SuccessCount);
        }

        #endregion

        #region Test Case #6: LockoutEnd in past - Skips user (Boundary)

        /// <summary>
        /// Test Case #6: When LockoutEnd is in the past, skips user (lockout expired)
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenLockoutEndInPast_SkipsUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1) // Past = expired lockout
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SkippedCount);
            Assert.Equal(0, data.SuccessCount);
        }

        #endregion

        #region Test Case #7: LockoutEnd in future - Reactivates user (Boundary)

        /// <summary>
        /// Test Case #7: When LockoutEnd is in the future, reactivates user (currently locked)
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenLockoutEndInFuture_ReactivatesUser()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1) // Future = currently locked
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.SuccessCount);
            Assert.Equal(0, data.SkippedCount);
            Assert.Null(user.LockoutEnd); // Verify it was set to null
        }

        #endregion

        #region Test Case #8: UpdateSecurityStamp fails - Marks as failed

        /// <summary>
        /// Test Case #8: When UpdateSecurityStampAsync fails, marks user as failed
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenUpdateSecurityStampFails_MarksAsFailed()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.MaxValue
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            var identityErrors = new[] { new IdentityError { Description = "Database error" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(0, data.SuccessCount);

            var failedResult = data.Results.First();
            Assert.Equal("Failed", failedResult.Status);
            Assert.Contains("Database error", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #9: Inner exception thrown - Marks as failed and continues

        /// <summary>
        /// Test Case #9: When inner exception is thrown for one user, marks as failed and continues with others
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenInnerExceptionThrown_MarksAsFailedAndContinues()
        {
            // Arrange
            var validUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnd = DateTimeOffset.MaxValue };
            var request = new BulkReactivateUsersDto
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
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success); // Overall operation succeeds
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(1, data.FailedCount);
            Assert.Equal(1, data.SuccessCount);

            var failedResult = data.Results.First(r => r.UserId == "user-1");
            Assert.Equal("Failed", failedResult.Status);
            Assert.Contains("Database error", failedResult.ErrorMessage);
        }

        #endregion

        #region Test Case #10: Outer exception thrown - Returns error

        /// <summary>
        /// Test Case #10: When outer exception is thrown, returns error response
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenOuterExceptionThrown_ReturnsError()
        {
            // Arrange
            var request = new BulkReactivateUsersDto
            {
                UserIds = null! // This will cause NullReferenceException in foreach
            };

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Error processing bulk reactivate operation", result.Message);
        }

        #endregion

        #region Test Case #11: Mixed results - Returns correct counts

        /// <summary>
        /// Test Case #11: When mixed results, returns correct counts
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenMixedResults_ReturnsCorrectCounts()
        {
            // Arrange
            var deactivatedUser = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnd = DateTimeOffset.MaxValue };
            var notDeactivatedUser = new ApplicationUser { Id = "user-2", UserName = "user2", LockoutEnd = null };
            var failUpdateUser = new ApplicationUser { Id = "user-3", UserName = "user3", LockoutEnd = DateTimeOffset.MaxValue };

            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1", "user-not-found", "user-2", "user-3" }
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(deactivatedUser);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-not-found")).ReturnsAsync((ApplicationUser?)null);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-2")).ReturnsAsync(notDeactivatedUser);
            _mockUserManager.Setup(x => x.FindByIdAsync("user-3")).ReturnsAsync(failUpdateUser);

            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(deactivatedUser)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(failUpdateUser))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(4, data.TotalRequested);
            Assert.Equal(1, data.SuccessCount);   // user-1
            Assert.Equal(2, data.FailedCount);    // user-not-found, user-3 (update failed)
            Assert.Equal(1, data.SkippedCount);   // user-2 (not deactivated)
        }

        #endregion

        #region Test Case #12: Returns correct bulk result structure

        /// <summary>
        /// Test Case #12: Verifies the response contains correct bulk result structure
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_ReturnsCorrectBulkResultStructure()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1", UserName = "user1", LockoutEnd = DateTimeOffset.MaxValue };
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Reason = "Batch reactivation"
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            var beforeTest = DateTime.UtcNow;

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);

            Assert.Equal(1, data.TotalRequested);
            Assert.Equal("Batch reactivation", data.Reason);
            Assert.True(data.ProcessedAt >= beforeTest);
            Assert.True(data.ProcessedAt <= DateTime.UtcNow.AddSeconds(5));
            Assert.Single(data.Results);
        }

        #endregion

        #region Test Case #13: Empty user IDs list - Returns empty result

        /// <summary>
        /// Test Case #13: When user IDs list is empty, returns empty result
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenEmptyUserIdsList_ReturnsEmptyResult()
        {
            // Arrange
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string>() // Empty list
            };

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(0, data.TotalRequested);
            Assert.Equal(0, data.SuccessCount);
            Assert.Equal(0, data.FailedCount);
            Assert.Equal(0, data.SkippedCount);
            Assert.Empty(data.Results);
        }

        #endregion

        #region Test Case #14: Success - Sets LockoutEnd to null

        /// <summary>
        /// Test Case #14: When reactivation succeeds, LockoutEnd is set to null
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_SetsLockoutEndToNullOnSuccess()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.MaxValue // Currently locked
            };
            var request = new BulkReactivateUsersDto { UserIds = new List<string> { "user-1" } };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
                .Callback<ApplicationUser>(u =>
                {
                    // Verify LockoutEnd is set to null BEFORE UpdateSecurityStampAsync is called
                    Assert.Null(u.LockoutEnd);
                })
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Null(user.LockoutEnd);
        }

        #endregion

        #region Test Case #15: Reason provided - Logs reason

        /// <summary>
        /// Test Case #15: When reason is provided, it is included in the result
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenReasonProvided_IncludesReasonInResult()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.MaxValue
            };
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Reason = "Test reason"
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal("Test reason", data.Reason);
        }

        #endregion

        #region Test Case #16: Reason null - Uses default message in log

        /// <summary>
        /// Test Case #16: When reason is null, result contains null reason
        /// </summary>
        [Fact]
        public async Task BulkReactivateUsersAsync_WhenReasonNull_ResultHasNullReason()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user1",
                LockoutEnd = DateTimeOffset.MaxValue
            };
            var request = new BulkReactivateUsersDto
            {
                UserIds = new List<string> { "user-1" },
                Reason = null // NULL reason
            };

            _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.BulkReactivateUsersAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkReactivateResultDto;
            Assert.NotNull(data);
            Assert.Null(data.Reason);
        }

        #endregion
    }
}

