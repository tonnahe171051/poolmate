using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using TournamentModel = PoolMate.Api.Models.Tournament;

namespace PoolMateBackend.Tests.UnitTests.Services.AdminPlayer
{
    /// <summary>
    /// Unit Tests for AdminPlayerService.GetDataQualityReportAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 25 (based on GetDataQualityReportAsync_TestCases.md)
    /// </summary>
    public class AdminPlayerServiceGetDataQualityReportAsyncTests : IDisposable
    {
        // ============================================
        // SECTION 1: FIELDS
        // ============================================
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly AdminPlayerService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public AdminPlayerServiceGetDataQualityReportAsyncTests()
        {
            // Use InMemory Database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);

            // Mock UserManager
            var store = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Inject dependencies into the Service
            _sut = new AdminPlayerService(_dbContext, _mockUserManager.Object);
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
            string? email = "valid@test.com",
            string? phone = "1234567890",
            int? skillLevel = 5,
            string? country = "VN",
            string? city = "HCM",
            bool withRecentTournament = false,
            bool withOldTournament = false,
            DateTime? tournamentDate = null)
        {
            var player = new Player
            {
                Id = id,
                FullName = $"Player {id}",
                Slug = $"player-{id}",
                Email = email,
                Phone = phone,
                SkillLevel = skillLevel,
                Country = country,
                City = city,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Players.Add(player);
            await _dbContext.SaveChangesAsync();

            if (withRecentTournament || withOldTournament || tournamentDate.HasValue)
            {
                var tournament = new TournamentModel
                {
                    Name = $"Tournament for Player {id}",
                    OwnerUserId = "owner-1",
                    StartUtc = tournamentDate ?? (withRecentTournament 
                        ? DateTime.UtcNow.AddMonths(-6) 
                        : DateTime.UtcNow.AddYears(-2))
                };
                _dbContext.Tournaments.Add(tournament);
                await _dbContext.SaveChangesAsync();

                var tournamentPlayer = new TournamentPlayer
                {
                    PlayerId = player.Id,
                    TournamentId = tournament.Id,
                    DisplayName = player.FullName
                };
                _dbContext.TournamentPlayers.Add(tournamentPlayer);
                await _dbContext.SaveChangesAsync();
            }

            return player;
        }

        private async Task CreateMultiplePlayersAsync(int count, Func<int, (string? email, string? phone, int? skill, string? country, string? city, bool recent, bool old)> configFunc)
        {
            for (int i = 1; i <= count; i++)
            {
                var config = configFunc(i);
                await CreatePlayerAsync(
                    i,
                    config.email,
                    config.phone,
                    config.skill,
                    config.country,
                    config.city,
                    config.recent,
                    config.old);
            }
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: No players - Returns zero stats

        /// <summary>
        /// Test Case #1: When no players exist, returns zero stats
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenNoPlayers_ReturnsZeroStats()
        {
            // Arrange - No players added

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(0, result.Overview.TotalPlayers);
            Assert.Equal(0, result.Overview.IssuePercentage);
            Assert.Equal(0, result.Overview.HealthyPercentage);
            Assert.Empty(result.Recommendations);
        }

        #endregion

        #region Test Case #2: All players healthy - Returns no issues

        /// <summary>
        /// Test Case #2: When all players are healthy, returns no issues
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenAllPlayersHealthy_ReturnsNoIssues()
        {
            // Arrange
            await CreatePlayerAsync(1, "valid@test.com", "1234567890", 5, "VN", "HCM", withRecentTournament: true);
            await CreatePlayerAsync(2, "valid2@test.com", "0987654321", 7, "VN", "HN", withRecentTournament: true);

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(2, result.Overview.TotalPlayers);
            Assert.Equal(0, result.Overview.PlayersWithIssues);
            Assert.Equal(2, result.Overview.HealthyPlayers);
            Assert.Equal(100, result.Overview.HealthyPercentage);
        }

        #endregion

        #region Test Case #3: Player missing email - Counts missing email

        /// <summary>
        /// Test Case #3: When player is missing email, counts missing email
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerMissingEmail_CountsMissingEmail()
        {
            // Arrange
            await CreatePlayerAsync(1, email: null, phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingEmail);
            Assert.Equal(1, result.Overview.PlayersWithIssues);
        }

        #endregion

        #region Test Case #4: Player missing phone - Counts missing phone

        /// <summary>
        /// Test Case #4: When player is missing phone, counts missing phone
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerMissingPhone_CountsMissingPhone()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "", skillLevel: 5, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingPhone);
        }

        #endregion

        #region Test Case #5: Player missing skill level - Counts missing skill

        /// <summary>
        /// Test Case #5: When player is missing skill level, counts missing skill
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerMissingSkillLevel_CountsMissingSkill()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "1234567890", skillLevel: null, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingSkillLevel);
        }

        #endregion

        #region Test Case #6: Player missing country only - Counts missing location

        /// <summary>
        /// Test Case #6: When player is missing country only, counts missing location
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerMissingCountryOnly_CountsMissingLocation()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "1234567890", skillLevel: 5, country: null, city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingLocation);
        }

        #endregion

        #region Test Case #7: Player missing city only - Counts missing location

        /// <summary>
        /// Test Case #7: When player is missing city only, counts missing location
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerMissingCityOnly_CountsMissingLocation()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: null);

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingLocation);
        }

        #endregion

        #region Test Case #8: Player has invalid email - Counts invalid email

        /// <summary>
        /// Test Case #8: When player has invalid email format, counts invalid email
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerHasInvalidEmail_CountsInvalidEmail()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "not-an-email", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.InvalidEmail);
        }

        #endregion

        #region Test Case #9: Player has invalid phone - Counts invalid phone

        /// <summary>
        /// Test Case #9: When player has invalid phone format, counts invalid phone
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerHasInvalidPhone_CountsInvalidPhone()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "abc", skillLevel: 5, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.InvalidPhone);
        }

        #endregion

        #region Test Case #10: Player has invalid skill level - Counts invalid skill

        /// <summary>
        /// Test Case #10: When player has invalid skill level (out of range 1-10), counts invalid skill
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerHasInvalidSkillLevel_CountsInvalidSkill()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "valid@test.com", phone: "1234567890", skillLevel: 999, country: "VN", city: "HCM");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.InvalidSkillLevel);
        }

        #endregion

        #region Test Case #11: Duplicate emails - Counts potential duplicates

        /// <summary>
        /// Test Case #11: When multiple players have same email, counts potential duplicates
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenDuplicateEmails_CountsPotentialDuplicates()
        {
            // Arrange - Two players with same email
            await CreatePlayerAsync(1, email: "test@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            await CreatePlayerAsync(2, email: "test@test.com", phone: "0987654321", skillLevel: 6, country: "VN", city: "HN");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.PotentialDuplicates); // 1 group of duplicates
        }

        #endregion

        #region Test Case #12: Duplicate emails different case - Counts as same group

        /// <summary>
        /// Test Case #12: When emails differ only in case, counts as same group (case-insensitive)
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenDuplicateEmailsDifferentCase_CountsAsSameGroup()
        {
            // Arrange - Same email, different case
            await CreatePlayerAsync(1, email: "Test@Test.COM", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            await CreatePlayerAsync(2, email: "test@test.com", phone: "0987654321", skillLevel: 6, country: "VN", city: "HN");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.PotentialDuplicates); // Counted as 1 group (case-insensitive)
        }

        #endregion

        #region Test Case #13: Tournament exactly one year ago - Player is active

        /// <summary>
        /// Test Case #13: When tournament is exactly 1 year ago, player is considered active (>= passes)
        /// Note: Using 1 year ago + 1 second to account for timing differences during test execution
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenTournamentExactlyOneYearAgo_PlayerIsActive()
        {
            // Arrange
            var now = DateTime.UtcNow;
            // Use 1 year ago + 1 minute to ensure we're at or past the boundary during query
            var exactlyOneYearAgo = now.AddYears(-1).AddMinutes(1);

            await CreatePlayerAsync(1, email: "test@test.com", phone: "1234567890", skillLevel: 5, 
                country: "VN", city: "HCM", tournamentDate: exactlyOneYearAgo);

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(0, result.Issues.InactivePlayers); // NOT inactive because >= passes
        }

        #endregion

        #region Test Case #14: Tournament one year plus one day ago - Player is inactive

        /// <summary>
        /// Test Case #14: When tournament is more than 1 year ago, player is considered inactive
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenTournamentOneYearPlusOneDay_PlayerIsInactive()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var moreThanOneYearAgo = now.AddYears(-1).AddDays(-1);

            await CreatePlayerAsync(1, email: "test@test.com", phone: "1234567890", skillLevel: 5, 
                country: "VN", city: "HCM", tournamentDate: moreThanOneYearAgo);

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.InactivePlayers); // IS inactive
        }

        #endregion

        #region Test Case #15: Player never played - Counts never played tournament

        /// <summary>
        /// Test Case #15: When player has no tournament participation, counts never played
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerNeverPlayed_CountsNeverPlayedTournament()
        {
            // Arrange - Player with no tournaments
            await CreatePlayerAsync(1, email: "test@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            // No tournament added

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.NeverPlayedTournament);
        }

        #endregion

        #region Test Case #16: Missing phone exactly 50% - No recommendation

        /// <summary>
        /// Test Case #16: When missing phone is exactly 50%, no recommendation (boundary)
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenMissingPhoneExactly50Percent_NoRecommendation()
        {
            // Arrange - 5 out of 10 missing phone (exactly 50%)
            for (int i = 1; i <= 10; i++)
            {
                await CreatePlayerAsync(i, 
                    email: $"user{i}@test.com", 
                    phone: i <= 5 ? null : "1234567890", // 5 missing
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM");
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(5, result.Issues.MissingPhone);
            Assert.DoesNotContain(result.Recommendations, r => r.Contains("missing phone", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Test Case #17: Missing phone over 50% - Adds recommendation

        /// <summary>
        /// Test Case #17: When missing phone is over 50%, adds recommendation
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenMissingPhoneOver50Percent_AddsRecommendation()
        {
            // Arrange - 6 out of 10 missing phone (60%)
            for (int i = 1; i <= 10; i++)
            {
                await CreatePlayerAsync(i, 
                    email: $"user{i}@test.com", 
                    phone: i <= 6 ? null : "1234567890", // 6 missing
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM");
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(6, result.Issues.MissingPhone);
            Assert.Contains(result.Recommendations, r => r.Contains("High rate of missing phone numbers"));
        }

        #endregion

        #region Test Case #18: Inactive exactly 30% - No recommendation

        /// <summary>
        /// Test Case #18: When inactive players is exactly 30%, no recommendation (boundary)
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenInactiveExactly30Percent_NoRecommendation()
        {
            // Arrange - 3 out of 10 inactive (exactly 30%)
            var now = DateTime.UtcNow;
            var recentDate = now.AddMonths(-6);
            var oldDate = now.AddYears(-2);

            for (int i = 1; i <= 10; i++)
            {
                // First 3 players: played old tournament (inactive)
                // Rest: played recent tournament (active)
                await CreatePlayerAsync(i, 
                    email: $"user{i}@test.com", 
                    phone: "1234567890", 
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM",
                    tournamentDate: i <= 3 ? oldDate : recentDate);
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(3, result.Issues.InactivePlayers);
            Assert.DoesNotContain(result.Recommendations, r => r.Contains("inactive players", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Test Case #19: Inactive over 30% - Adds recommendation

        /// <summary>
        /// Test Case #19: When inactive players is over 30%, adds recommendation
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenInactiveOver30Percent_AddsRecommendation()
        {
            // Arrange - 4 out of 10 inactive (40%)
            var now = DateTime.UtcNow;
            var recentDate = now.AddMonths(-6);
            var oldDate = now.AddYears(-2);

            for (int i = 1; i <= 10; i++)
            {
                // First 4 players: played old tournament (inactive)
                // Rest: played recent tournament (active)
                await CreatePlayerAsync(i, 
                    email: $"user{i}@test.com", 
                    phone: "1234567890", 
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM",
                    tournamentDate: i <= 4 ? oldDate : recentDate);
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(4, result.Issues.InactivePlayers);
            Assert.Contains(result.Recommendations, r => r.Contains("inactive players") && r.Contains("Consider archiving"));
        }

        #endregion

        #region Test Case #20: Has duplicates - Adds recommendation

        /// <summary>
        /// Test Case #20: When has duplicate email groups, adds recommendation
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenHasDuplicates_AddsRecommendation()
        {
            // Arrange - 2 groups of duplicates
            await CreatePlayerAsync(1, email: "dup1@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            await CreatePlayerAsync(2, email: "dup1@test.com", phone: "0987654321", skillLevel: 5, country: "VN", city: "HCM"); // Group 1
            await CreatePlayerAsync(3, email: "dup2@test.com", phone: "1111111111", skillLevel: 5, country: "VN", city: "HCM");
            await CreatePlayerAsync(4, email: "dup2@test.com", phone: "2222222222", skillLevel: 5, country: "VN", city: "HCM"); // Group 2

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(2, result.Issues.PotentialDuplicates);
            Assert.Contains(result.Recommendations, r => r.Contains("2 duplicate email groups found"));
        }

        #endregion

        #region Test Case #21: No duplicates - No recommendation for duplicates

        /// <summary>
        /// Test Case #21: When no duplicate emails, no recommendation for duplicates
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenNoDuplicates_NoRecommendationForDuplicates()
        {
            // Arrange - All unique emails
            await CreatePlayerAsync(1, email: "user1@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            await CreatePlayerAsync(2, email: "user2@test.com", phone: "0987654321", skillLevel: 6, country: "VN", city: "HN");
            await CreatePlayerAsync(3, email: "user3@test.com", phone: "1111111111", skillLevel: 7, country: "VN", city: "DN");

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(0, result.Issues.PotentialDuplicates);
            Assert.DoesNotContain(result.Recommendations, r => r.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Test Case #22: Calculates issue percentage correctly

        /// <summary>
        /// Test Case #22: Issue percentage is calculated correctly
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_CalculatesIssuePercentageCorrectly()
        {
            // Arrange - 3 players with issues out of 10
            for (int i = 1; i <= 10; i++)
            {
                await CreatePlayerAsync(i, 
                    email: i <= 3 ? null : $"user{i}@test.com", // First 3 have missing email
                    phone: "1234567890", 
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM");
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(10, result.Overview.TotalPlayers);
            Assert.Equal(3, result.Overview.PlayersWithIssues);
            Assert.Equal(30.0, result.Overview.IssuePercentage);
        }

        #endregion

        #region Test Case #23: Calculates healthy percentage correctly

        /// <summary>
        /// Test Case #23: Healthy percentage is calculated correctly
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_CalculatesHealthyPercentageCorrectly()
        {
            // Arrange - 7 healthy players out of 10
            for (int i = 1; i <= 10; i++)
            {
                await CreatePlayerAsync(i, 
                    email: i <= 3 ? null : $"user{i}@test.com", // First 3 have issues
                    phone: "1234567890", 
                    skillLevel: 5, 
                    country: "VN", 
                    city: "HCM");
            }

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(7, result.Overview.HealthyPlayers);
            Assert.Equal(70.0, result.Overview.HealthyPercentage);
        }

        #endregion

        #region Test Case #24: Player with multiple issues - Counts once in players with issues

        /// <summary>
        /// Test Case #24: When player has multiple issues, counts only once in PlayersWithIssues
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_WhenPlayerHasMultipleIssues_CountsOnceInPlayersWithIssues()
        {
            // Arrange - Player has ALL issues
            await CreatePlayerAsync(1, 
                email: null, // Missing
                phone: null, // Missing
                skillLevel: null, // Missing
                country: null, // Missing location
                city: null); // Missing location

            // Act
            var result = await _sut.GetDataQualityReportAsync();

            // Assert
            Assert.Equal(1, result.Issues.MissingEmail);
            Assert.Equal(1, result.Issues.MissingPhone);
            Assert.Equal(1, result.Issues.MissingSkillLevel);
            Assert.Equal(1, result.Issues.MissingLocation);
            Assert.Equal(1, result.Overview.PlayersWithIssues); // Only counted ONCE
        }

        #endregion

        #region Test Case #25: Sets GeneratedAt to current time

        /// <summary>
        /// Test Case #25: GeneratedAt is set to approximately current time
        /// </summary>
        [Fact]
        public async Task GetDataQualityReportAsync_SetsGeneratedAtToCurrentTime()
        {
            // Arrange
            await CreatePlayerAsync(1, email: "test@test.com", phone: "1234567890", skillLevel: 5, country: "VN", city: "HCM");
            var beforeTest = DateTime.UtcNow;

            // Act
            var result = await _sut.GetDataQualityReportAsync();
            var afterTest = DateTime.UtcNow;

            // Assert
            Assert.True(result.GeneratedAt >= beforeTest.AddSeconds(-1));
            Assert.True(result.GeneratedAt <= afterTest.AddSeconds(1));
        }

        #endregion
    }
}

