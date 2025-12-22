using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.PlayerProfile
{
    /// <summary>
    /// Unit Tests for PlayerProfileService.UpdatePlayerFromUserAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 13 (based on UpdatePlayerFromUserAsync_TestCases.md)
    /// </summary>
    public class PlayerProfileServiceUpdatePlayerFromUserAsyncTests : IDisposable
    {
        // ============================================
        // SECTION 1: FIELDS
        // ============================================
        private readonly ApplicationDbContext _dbContext;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly PlayerProfileService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public PlayerProfileServiceUpdatePlayerFromUserAsyncTests()
        {
            // Use InMemory Database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);

            // Inject dependencies into the Service
            _sut = new PlayerProfileService(_dbContext);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        // ============================================
        // HELPER METHODS
        // ============================================
        private async Task<Player> CreatePlayerAsync(
            int id,
            string userId,
            string fullName,
            string slug,
            string? nickname = null,
            string? email = null,
            string? phone = null,
            string? country = null,
            string? city = null)
        {
            var player = new Player
            {
                Id = id,
                UserId = userId,
                FullName = fullName,
                Slug = slug,
                Nickname = nickname,
                Email = email,
                Phone = phone,
                Country = country,
                City = city,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Players.Add(player);
            await _dbContext.SaveChangesAsync();
            
            // Detach to simulate fresh query
            _dbContext.Entry(player).State = EntityState.Detached;
            
            return player;
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Player not found - Returns without action

        /// <summary>
        /// Test Case #1: When player is not found, returns without any action
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenPlayerNotFound_ReturnsWithoutAction()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user-999",
                FirstName = "John",
                LastName = "Doe"
            };
            // No players in DB

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert - No exception thrown, and no players added
            Assert.Empty(_dbContext.Players);
        }

        #endregion

        #region Test Case #2: FirstName and LastName exist - Uses FullName

        /// <summary>
        /// Test Case #2: When FirstName and LastName exist, uses combined FullName
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenFirstAndLastNameExist_UsesFullName()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("John Doe", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #3: Only FirstName exists - Uses FirstName

        /// <summary>
        /// Test Case #3: When only FirstName exists, uses FirstName as FullName
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenOnlyFirstNameExists_UsesFirstName()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "John",
                LastName = null
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("John", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #4: Only LastName exists - Uses LastName

        /// <summary>
        /// Test Case #4: When only LastName exists, uses LastName as FullName
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenOnlyLastNameExists_UsesLastName()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = null,
                LastName = "Doe"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("Doe", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #5: No name but UserName exists - Uses UserName

        /// <summary>
        /// Test Case #5: When no name but UserName exists, uses UserName as FullName
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenNoNameButUserNameExists_UsesUserName()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "",
                LastName = "",
                UserName = "johndoe"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("johndoe", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #6: No name and no UserName - Uses "Unknown Player"

        /// <summary>
        /// Test Case #6: When no name and no UserName, uses "Unknown Player" as FullName
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenNoNameAndNoUserName_UsesUnknownPlayer()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = null,
                LastName = null,
                UserName = null
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("Unknown Player", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #7: Slug not changed - Keeps old slug

        /// <summary>
        /// Test Case #7: When slug doesn't change, keeps the old slug
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenSlugNotChanged_KeepsOldSlug()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "John Doe", "john-doe");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "John",
                LastName = "Doe" // Same name -> same slug
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("john-doe", updatedPlayer.Slug); // Unchanged
        }

        #endregion

        #region Test Case #8: Slug changed and unique - Uses new slug

        /// <summary>
        /// Test Case #8: When slug changes and new slug is unique, uses new slug
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndUnique_UsesNewSlug()
        {
            // Arrange
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "New",
                LastName = "Name" // Different name -> different slug
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("new-name", updatedPlayer.Slug);
            Assert.Equal("New Name", updatedPlayer.FullName);
        }

        #endregion

        #region Test Case #9: Slug changed and collides 1 time - Appends "-1"

        /// <summary>
        /// Test Case #9: When slug changes and collides once, appends "-1" suffix
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndCollides1Time_AppendsNumber1()
        {
            // Arrange
            // Current player to update
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            // Another player with the target slug
            await CreatePlayerAsync(2, "user-456", "New Name", "new-name");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "New",
                LastName = "Name" // Will try "new-name" but it's taken
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("new-name-1", updatedPlayer.Slug); // Appended -1
        }

        #endregion

        #region Test Case #10: Slug changed and collides multiple times - Appends incrementing number

        /// <summary>
        /// Test Case #10: When slug changes and collides multiple times, appends incrementing number
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenSlugChangedAndCollidesMultipleTimes_AppendsIncrementingNumber()
        {
            // Arrange
            // Current player to update
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name");
            // Other players with colliding slugs
            await CreatePlayerAsync(2, "user-a", "New Name", "new-name");
            await CreatePlayerAsync(3, "user-b", "New Name", "new-name-1");
            await CreatePlayerAsync(4, "user-c", "New Name", "new-name-2");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "New",
                LastName = "Name"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("new-name-3", updatedPlayer.Slug); // Appended -3
        }

        #endregion

        #region Test Case #11: New slug matches own old slug - Does not trigger collision

        /// <summary>
        /// Test Case #11: When new slug matches player's own slug, does not trigger collision
        /// (p.Id != player.Id filter ensures player doesn't collide with itself)
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_WhenNewSlugMatchesOwnOldSlug_DoesNotTriggerCollision()
        {
            // Arrange
            // Player already has slug "john-doe"
            await CreatePlayerAsync(1, "user-123", "John Doe", "john-doe");
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "John",
                LastName = "Doe" // Same slug will be generated
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("john-doe", updatedPlayer.Slug); // No collision, keeps same slug
        }

        #endregion

        #region Test Case #12: Updates all fields correctly

        /// <summary>
        /// Test Case #12: When updating, all fields are mapped correctly from user to player
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_UpdatesAllFieldsCorrectly()
        {
            // Arrange
            await CreatePlayerAsync(
                id: 1,
                userId: "user-123",
                fullName: "Old Name",
                slug: "old-name",
                nickname: "OldNick",
                email: "old@email.com",
                phone: "111",
                country: "OC",
                city: "OldCity"
            );
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "New",
                LastName = "Name",
                Nickname = "NewNick",
                Email = "new@email.com",
                PhoneNumber = "999",
                Country = "NC",
                City = "NewCity"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("New Name", updatedPlayer.FullName);
            Assert.Equal("NewNick", updatedPlayer.Nickname);
            Assert.Equal("new@email.com", updatedPlayer.Email);
            Assert.Equal("999", updatedPlayer.Phone);
            Assert.Equal("NC", updatedPlayer.Country);
            Assert.Equal("NewCity", updatedPlayer.City);
        }

        #endregion

        #region Test Case #13: SaveChangesAsync is called

        /// <summary>
        /// Test Case #13: When player is found and updated, SaveChangesAsync is called
        /// </summary>
        [Fact]
        public async Task UpdatePlayerFromUserAsync_CallsSaveChangesAsync()
        {
            // Arrange
            var originalEmail = "old@email.com";
            await CreatePlayerAsync(1, "user-123", "Old Name", "old-name", email: originalEmail);
            
            var user = new ApplicationUser
            {
                Id = "user-123",
                FirstName = "New",
                LastName = "Name",
                Email = "new@email.com"
            };

            // Act
            await _sut.UpdatePlayerFromUserAsync(user);

            // Assert - Verify changes were persisted
            var updatedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == "user-123");
            Assert.NotNull(updatedPlayer);
            Assert.Equal("new@email.com", updatedPlayer.Email); // Change persisted
            Assert.NotEqual(originalEmail, updatedPlayer.Email);
        }

        #endregion
    }
}

