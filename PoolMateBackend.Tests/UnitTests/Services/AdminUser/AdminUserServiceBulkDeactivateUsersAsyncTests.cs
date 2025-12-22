using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Hubs;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using Xunit;

namespace PoolMateBackend.Tests.UnitTests.Services.AdminUser
{
    /// <summary>
    /// Unit Tests for AdminUserService.BulkDeactivateUsersAsync
    /// </summary>
    public class AdminUserServiceBulkDeactivateUsersAsyncTests : IDisposable
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
        public async Task BulkDeactivateUsersAsync_ShouldSkip_WhenAdminDeactivatesSelf()
        {
            // Arrange
            var adminId = "admin-1";
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { adminId } };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, adminId, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.NotNull(data);
            Assert.Equal(0, data.SuccessCount);
            Assert.Equal(1, data.SkippedCount);
        }

        [Fact]
        public async Task BulkDeactivateUsersAsync_ShouldSkip_WhenUserNotFound()
        {
            // Arrange
            _mockUserManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { "user-1" } };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, "admin-1", CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.Equal(0, data.SuccessCount);
            Assert.Equal(1, data.FailedCount); // Or Skipped depending on implementation, usually Failed if not found
        }

        [Fact]
        public async Task BulkDeactivateUsersAsync_ShouldSkip_WhenUserAlreadyLocked()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1" };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { user.Id } };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, "admin-1", CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.Equal(1, data.SkippedCount);
        }
        

        [Fact]
        public async Task BulkDeactivateUsersAsync_ShouldSuccess_WhenValidUser()
        {
            // Arrange
            var user = new ApplicationUser { Id = "user-1", LockoutEnabled = false };
            _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { "User" });
            
            _mockUserManager.Setup(x => x.SetLockoutEnabledAsync(user, true))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            var request = new BulkDeactivateUsersDto { UserIds = new List<string> { user.Id } };

            // Act
            var result = await _sut.BulkDeactivateUsersAsync(request, "admin-1", CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            var data = result.Data as BulkDeactivateResultDto;
            Assert.Equal(1, data.SuccessCount);
            
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

