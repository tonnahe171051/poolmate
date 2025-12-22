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
    /// Unit Tests for AdminUserService.DeactivateUserAsync
    /// </summary>
    public class AdminUserServiceDeactivateUserAsyncTests : IDisposable
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<ILogger<AdminUserService>> _mockLogger;
        private readonly Mock<IBannedUserCacheService> _mockBannedUserCache;
        private readonly Mock<IHubContext<AppHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockHubClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly ApplicationDbContext _dbContext;
        private readonly AdminUserService _sut;

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

            // Mock Cache
            _mockBannedUserCache = new Mock<IBannedUserCacheService>();

            // Mock SignalR
            _mockHubContext = new Mock<IHubContext<AppHub>>();
            _mockHubClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            
            _mockHubContext.Setup(x => x.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(x => x.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);

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
        public async Task DeactivateUserAsync_ShouldReturnError_WhenAdminDeactivatesSelf()
        {
            // Arrange
            var adminId = "admin-1";
            var userId = "admin-1";

            // Act
            var result = await _sut.DeactivateUserAsync(userId, adminId, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("You cannot deactivate your own account.", result.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldReturnError_WhenUserNotFound()
        {
            // Arrange
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _sut.DeactivateUserAsync("user-1", "admin-1", CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldReturnError_WhenUserAlreadyLocked()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1" };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

            // Act
            var result = await _sut.DeactivateUserAsync(user.Id, "admin-1", CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User is already deactivated", result.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldReturnError_WhenTargetIsAdmin()
        {
            // Arrange
            var user = new ApplicationUser { Id = "admin-target" };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { "Admin" });

            // Act
            var result = await _sut.DeactivateUserAsync(user.Id, "admin-1", CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Cannot deactivate this user", result.Message);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldSuccess_WhenValidUser()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1", UserName = "testuser", LockoutEnabled = false };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { "User" });
            
            _mockUserManager.Setup(x => x.SetLockoutEnabledAsync(user, true))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _mockBannedUserCache.Setup(x => x.BanUserAsync(user.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.DeactivateUserAsync(user.Id, "admin-1", CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            
            // Verify DB calls
            _mockUserManager.Verify(x => x.SetLockoutEnabledAsync(user, true), Times.Once);
            _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), Times.Once);
            _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);

            // Verify Cache
            _mockBannedUserCache.Verify(x => x.BanUserAsync(user.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify SignalR
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                AppHubEvents.ReceiveLogoutCommand,
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
