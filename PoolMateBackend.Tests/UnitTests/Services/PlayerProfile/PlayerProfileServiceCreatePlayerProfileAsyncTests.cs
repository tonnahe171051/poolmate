using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.PlayerProfile
{
    /// <summary>
    /// Unit Tests for PlayerProfileService.CreatePlayerProfileAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 13 (based on CreatePlayerProfileAsync_TestCases.md)
    /// </summary>
    public class PlayerProfileServiceCreatePlayerProfileAsyncTests : IDisposable
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
        public PlayerProfileServiceCreatePlayerProfileAsyncTests()
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
        private async Task<Player> CreateExistingPlayerAsync(
            string userId,
            string fullName = "Existing Player",
            string slug = "existing-player")
        {
            var player = new Player
            {
                UserId = userId,
                FullName = fullName,
                Slug = slug,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Players.Add(player);
            await _dbContext.SaveChangesAsync();
            return player;
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: UserId is null/empty/whitespace - Returns null

        /// <summary>
        /// Test Case #1: When userId is null, empty, or whitespace, returns null
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreatePlayerProfileAsync_WhenUserIdIsNullOrWhiteSpace_ReturnsNull(string? userId)
        {
            // Arrange
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId!, user);

            // Assert
            Assert.Null(result);
            
            // Verify DB was NOT modified
            Assert.Empty(_dbContext.Players);
        }

        #endregion

        #region Test Case #2: User already has profile - Throws InvalidOperationException

        /// <summary>
        /// Test Case #2: When user already has a player profile, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenUserAlreadyHasProfile_ThrowsInvalidOperationException()
        {
            // Arrange
            var userId = "user-123";
            await CreateExistingPlayerAsync(userId, "Existing Player", "existing-player");
            
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.CreatePlayerProfileAsync(userId, user));
            Assert.Equal("User already has a player profile.", ex.Message);
        }

        #endregion

        #region Test Case #3: FirstName and LastName exist - Uses FullName

        /// <summary>
        /// Test Case #3: When FirstName and LastName exist, uses combined FullName
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenFirstAndLastNameExist_UsesFullName()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John Doe", result.FullName);
        }

        #endregion

        #region Test Case #4: Only FirstName exists - Uses FirstName

        /// <summary>
        /// Test Case #4: When only FirstName exists, uses FirstName as FullName
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenOnlyFirstNameExists_UsesFirstName()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = null
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John", result.FullName);
        }

        #endregion

        #region Test Case #5: Only LastName exists - Uses LastName

        /// <summary>
        /// Test Case #5: When only LastName exists, uses LastName as FullName
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenOnlyLastNameExists_UsesLastName()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = null,
                LastName = "Doe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Doe", result.FullName);
        }

        #endregion

        #region Test Case #6: No name but UserName exists - Uses UserName

        /// <summary>
        /// Test Case #6: When no name but UserName exists, uses UserName as FullName
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenNoNameButUserNameExists_UsesUserName()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "",
                LastName = "",
                UserName = "johndoe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("johndoe", result.FullName);
        }

        #endregion

        #region Test Case #7: No name and no UserName - Uses "Unknown Player"

        /// <summary>
        /// Test Case #7: When no name and no UserName, uses "Unknown Player" as FullName
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenNoNameAndNoUserName_UsesUnknownPlayer()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = null,
                LastName = null,
                UserName = null
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Unknown Player", result.FullName);
        }

        #endregion

        #region Test Case #8: Slug is unique - Uses base slug

        /// <summary>
        /// Test Case #8: When slug is unique, uses base slug without suffix
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenSlugIsUnique_UsesBaseSlug()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };
            // No existing players with slug "john-doe"

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            
            // Verify entity in DB has base slug
            var savedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == userId);
            Assert.NotNull(savedPlayer);
            Assert.Equal("john-doe", savedPlayer.Slug);
        }

        #endregion

        #region Test Case #9: Slug collides 1 time - Appends "-1"

        /// <summary>
        /// Test Case #9: When slug collides once, appends "-1" suffix
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenSlugCollides1Time_AppendsNumber1()
        {
            // Arrange
            var userId = "user-new";
            await CreateExistingPlayerAsync("user-old", "John Doe", "john-doe");
            
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            
            // Verify entity in DB has slug with "-1" suffix
            var savedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == userId);
            Assert.NotNull(savedPlayer);
            Assert.Equal("john-doe-1", savedPlayer.Slug);
        }

        #endregion

        #region Test Case #10: Slug collides multiple times - Appends incrementing number

        /// <summary>
        /// Test Case #10: When slug collides multiple times, appends incrementing number
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenSlugCollidesMultipleTimes_AppendsIncrementingNumber()
        {
            // Arrange
            var userId = "user-new";
            
            // Create existing players with colliding slugs
            var player1 = new Player { UserId = "user-1", FullName = "John Doe", Slug = "john-doe", CreatedAt = DateTime.UtcNow };
            var player2 = new Player { UserId = "user-2", FullName = "John Doe", Slug = "john-doe-1", CreatedAt = DateTime.UtcNow };
            var player3 = new Player { UserId = "user-3", FullName = "John Doe", Slug = "john-doe-2", CreatedAt = DateTime.UtcNow };
            _dbContext.Players.AddRange(player1, player2, player3);
            await _dbContext.SaveChangesAsync();
            
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            
            // Verify entity in DB has slug with "-3" suffix
            var savedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == userId);
            Assert.NotNull(savedPlayer);
            Assert.Equal("john-doe-3", savedPlayer.Slug);
        }

        #endregion

        #region Test Case #11: All valid - Saves player to database

        /// <summary>
        /// Test Case #11: When all data is valid, saves player to database
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenAllValid_SavesPlayerToDatabase()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            
            // Verify player was saved to database
            var savedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == userId);
            Assert.NotNull(savedPlayer);
            Assert.Equal(1, await _dbContext.Players.CountAsync());
        }

        #endregion

        #region Test Case #12: All valid - Returns correct DTO

        /// <summary>
        /// Test Case #12: When all data is valid, returns correct DTO with all fields
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_WhenAllValid_ReturnsCorrectDto()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                Nickname = "JD",
                PhoneNumber = "1234567890",
                Country = "VN",
                City = "Ho Chi Minh"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John Doe", result.FullName);
            Assert.Equal("JD", result.Nickname);
            Assert.Equal("john@example.com", result.Email);
            Assert.Equal("1234567890", result.Phone);
            Assert.Equal("VN", result.Country);
            Assert.Equal("Ho Chi Minh", result.City);
            Assert.Equal("Player profile created automatically from account info", result.Message);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
            Assert.True(result.CreatedAt > DateTime.UtcNow.AddSeconds(-5)); // Created within last 5 seconds
        }

        #endregion

        #region Test Case #13: Verify Player entity is mapped correctly

        /// <summary>
        /// Test Case #13: Verify Player entity is mapped correctly from ApplicationUser
        /// </summary>
        [Fact]
        public async Task CreatePlayerProfileAsync_VerifyPlayerEntityMappedCorrectly()
        {
            // Arrange
            var userId = "user-123";
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                Nickname = "JD",
                PhoneNumber = "1234567890",
                Country = "VN",
                City = "Ho Chi Minh"
            };

            // Act
            var result = await _sut.CreatePlayerProfileAsync(userId, user);

            // Assert - Verify entity in database
            var savedPlayer = await _dbContext.Players.FirstOrDefaultAsync(p => p.UserId == userId);
            Assert.NotNull(savedPlayer);
            
            // Verify all mapped properties
            Assert.Equal(userId, savedPlayer.UserId);
            Assert.Equal("John Doe", savedPlayer.FullName);
            Assert.Equal("john@example.com", savedPlayer.Email);
            Assert.Equal("JD", savedPlayer.Nickname);
            Assert.Equal("1234567890", savedPlayer.Phone);
            Assert.Equal("VN", savedPlayer.Country);
            Assert.Equal("Ho Chi Minh", savedPlayer.City);
            Assert.Null(savedPlayer.SkillLevel); // Should be null by default
            Assert.StartsWith("john-doe", savedPlayer.Slug);
        }

        #endregion
    }
}

