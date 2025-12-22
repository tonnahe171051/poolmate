using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Hubs;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using Xunit;

namespace PoolMateBackend.Tests.UnitTests.Services.AdminUser
{
    /// <summary>
    /// Unit Tests for AdminUserService.ReactivateUserAsync
    /// </summary>
    public class AdminUserServiceReactivateUserAsyncTests : IDisposable
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<ILogger<AdminUserService>> _mockLogger;
        private readonly Mock<IBannedUserCacheService> _mockBannedUserCache;
        private readonly Mock<IHubContext<AppHub>> _mockHubContext;
        private readonly ApplicationDbContext _dbContext;
        private readonly AdminUserService _sut;

        public AdminUserServiceReactivateUserAsyncTests()
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

            // Mock Cache
            _mockBannedUserCache = new Mock<IBannedUserCacheService>();

            // Mock SignalR
            _mockHubContext = new Mock<IHubContext<AppHub>>();

            // DbContext
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);

            _sut = new AdminUserService(
                _mockUserManager.Object,
                _mockRoleManager.Object,
                _dbContext,
                _mockLogger.Object,
                _mockBannedUserCache.Object,
                _mockHubContext.Object);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        [Fact]
        public async Task ReactivateUserAsync_ShouldReturnError_WhenUserNotFound()
        {
            // Arrange
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _sut.ReactivateUserAsync("user-1", CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
        }

        [Fact]
        public async Task ReactivateUserAsync_ShouldReturnError_WhenUserNotLocked()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1" };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

            // Act
            var result = await _sut.ReactivateUserAsync(user.Id, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User is not currently deactivated", result.Message);
        }

        [Fact]
        public async Task ReactivateUserAsync_ShouldSuccess_WhenValidUser()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1", UserName = "testuser" };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { "User" });
            
            _mockUserManager.Setup(x => x.SetLockoutEndDateAsync(user, null))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.ResetAccessFailedCountAsync(user))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _mockBannedUserCache.Setup(x => x.UnbanUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.ReactivateUserAsync(user.Id, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            
            // Verify DB calls
            _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(user, null), Times.Once);
            _mockUserManager.Verify(x => x.ResetAccessFailedCountAsync(user), Times.Once);
            _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);

            // Verify Cache
            _mockBannedUserCache.Verify(x => x.UnbanUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
