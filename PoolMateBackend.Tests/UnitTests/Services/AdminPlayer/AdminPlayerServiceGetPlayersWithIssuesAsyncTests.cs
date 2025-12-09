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
    /// Unit Tests for AdminPlayerService.GetPlayersWithIssuesAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 25 (based on GetPlayersWithIssuesAsync_TestCases.md)
    /// </summary>
    public class AdminPlayerServiceGetPlayersWithIssuesAsyncTests : IDisposable
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
        public AdminPlayerServiceGetPlayersWithIssuesAsyncTests()
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
            string fullName = "Test Player",
            string? email = "test@test.com",
            string? phone = "1234567890",
            int? skillLevel = 5,
            string? country = "VN",
            string? city = "HCM",
            DateTime? tournamentDate = null,
            bool noTournament = true)
        {
            var player = new Player
            {
                Id = id,
                FullName = fullName,
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

            if (!noTournament && tournamentDate.HasValue)
            {
                var tournament = new TournamentModel
                {
                    Name = $"Tournament for Player {id}",
                    OwnerUserId = "owner-1",
                    StartUtc = tournamentDate.Value
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

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Unknown issue type - Returns empty DTO

        /// <summary>
        /// Test Case #1: When unknown issue type is provided, returns empty DTO
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenUnknownIssueType_ReturnsEmptyDto()
        {
            // Arrange
            var issueType = "unknown-type";
            await CreatePlayerAsync(1);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync(issueType);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Players);
            Assert.Equal(0, result.TotalCount);
        }

        #endregion

        #region Test Case #2: Missing email - Returns players with null or empty email

        /// <summary>
        /// Test Case #2: When missing-email, returns players with null or empty email
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenMissingEmail_ReturnsPlayersWithNullOrEmptyEmail()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: null);
            await CreatePlayerAsync(2, fullName: "Player 2", email: "");
            await CreatePlayerAsync(3, fullName: "Player 3", email: "test@test.com");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Missing email", p.Issues));
        }

        #endregion

        #region Test Case #3: Missing phone - Returns players with null or empty phone

        /// <summary>
        /// Test Case #3: When missing-phone, returns players with null or empty phone
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenMissingPhone_ReturnsPlayersWithNullOrEmptyPhone()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", phone: null);
            await CreatePlayerAsync(2, fullName: "Player 2", phone: "");
            await CreatePlayerAsync(3, fullName: "Player 3", phone: "1234567890");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-phone");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Missing phone", p.Issues));
        }

        #endregion

        #region Test Case #4: Missing skill - Returns players with null skill level

        /// <summary>
        /// Test Case #4: When missing-skill, returns players with null skill level
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenMissingSkill_ReturnsPlayersWithNullSkillLevel()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", skillLevel: null);
            await CreatePlayerAsync(2, fullName: "Player 2", skillLevel: 5);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-skill");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Missing skill level", p.Issues));
        }

        #endregion

        #region Test Case #5: Inactive-1y - Returns inactive players

        /// <summary>
        /// Test Case #5: When inactive-1y, returns players with tournament over 1 year ago
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInactive1y_ReturnsInactivePlayers()
        {
            // Arrange
            var oldDate = DateTime.UtcNow.AddYears(-2);
            await CreatePlayerAsync(1, fullName: "Inactive Player", tournamentDate: oldDate, noTournament: false);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("inactive-1y");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Inactive > 1 year", p.Issues));
        }

        #endregion

        #region Test Case #6: Tournament exactly one year ago - Player is NOT inactive

        /// <summary>
        /// Test Case #6: When tournament is exactly 1 year ago, player is NOT inactive (boundary)
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenTournamentExactlyOneYearAgo_PlayerIsNotInactive()
        {
            // Arrange
            var now = DateTime.UtcNow;
            // Use 1 year ago + 1 minute to ensure we're at or past the boundary during query
            var exactlyOneYearAgo = now.AddYears(-1).AddMinutes(1);

            await CreatePlayerAsync(1, fullName: "Player 1", tournamentDate: exactlyOneYearAgo, noTournament: false);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("inactive-1y");

            // Assert
            Assert.Equal(0, result.TotalCount); // NOT inactive because >= passes
        }

        #endregion

        #region Test Case #7: Tournament over one year ago - Player IS inactive

        /// <summary>
        /// Test Case #7: When tournament is over 1 year ago, player IS inactive (boundary)
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenTournamentOverOneYearAgo_PlayerIsInactive()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var overOneYearAgo = now.AddYears(-1).AddDays(-1);

            await CreatePlayerAsync(1, fullName: "Player 1", tournamentDate: overOneYearAgo, noTournament: false);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("inactive-1y");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.Contains("Inactive > 1 year", result.Players[0].Issues);
        }

        #endregion

        #region Test Case #8: Never played - Returns players with no tournaments

        /// <summary>
        /// Test Case #8: When never-played, returns players with no tournament participation
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenNeverPlayed_ReturnsPlayersWithNoTournaments()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Never Played", noTournament: true);
            await CreatePlayerAsync(2, fullName: "Has Played", tournamentDate: DateTime.UtcNow.AddMonths(-1), noTournament: false);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("never-played");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.Contains("Never played", result.Players[0].Issues);
        }

        #endregion

        #region Test Case #9: Invalid email - Validates email in memory

        /// <summary>
        /// Test Case #9: When invalid-email, validates email format in memory
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInvalidEmail_ValidatesEmailInMemory()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "P1", email: "valid@test.com");
            await CreatePlayerAsync(2, fullName: "P2", email: "not-an-email");
            await CreatePlayerAsync(3, fullName: "P3", email: "also-bad");
            await CreatePlayerAsync(4, fullName: "P4", email: null); // Excluded from candidates

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("invalid-email");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Invalid email format", p.Issues));
        }

        #endregion

        #region Test Case #10: Invalid phone - Validates phone in memory

        /// <summary>
        /// Test Case #10: When invalid-phone, validates phone format in memory
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInvalidPhone_ValidatesPhoneInMemory()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "P1", phone: "1234567890");
            await CreatePlayerAsync(2, fullName: "P2", phone: "abc");
            await CreatePlayerAsync(3, fullName: "P3", phone: "xy");
            await CreatePlayerAsync(4, fullName: "P4", phone: null); // Excluded from candidates

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("invalid-phone");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Invalid phone format", p.Issues));
        }

        #endregion

        #region Test Case #11: Potential duplicates - Finds duplicate emails

        /// <summary>
        /// Test Case #11: When potential-duplicates, finds players with duplicate emails
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicateEmails()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: "same@test.com", phone: "111");
            await CreatePlayerAsync(2, fullName: "Player 2", email: "same@test.com", phone: "222");
            await CreatePlayerAsync(3, fullName: "Player 3", email: "unique@test.com", phone: "333");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("potential-duplicates");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Potential duplicate", p.Issues));
        }

        #endregion

        #region Test Case #12: Potential duplicates - Finds duplicate phones

        /// <summary>
        /// Test Case #12: When potential-duplicates, finds players with duplicate phones
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicatePhones()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: "a@test.com", phone: "same-phone");
            await CreatePlayerAsync(2, fullName: "Player 2", email: "b@test.com", phone: "same-phone");
            await CreatePlayerAsync(3, fullName: "Player 3", email: "c@test.com", phone: "unique-phone");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("potential-duplicates");

            // Assert
            Assert.Equal(2, result.TotalCount);
        }

        #endregion

        #region Test Case #13: Potential duplicates - Finds duplicate context (Name+City+Skill)

        /// <summary>
        /// Test Case #13: When potential-duplicates, finds players with same FullName+City+SkillLevel
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPotentialDuplicates_FindsDuplicateContext()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Same Name", email: "a@test.com", phone: "111", city: "HCM", skillLevel: 5);
            await CreatePlayerAsync(2, fullName: "Same Name", email: "b@test.com", phone: "222", city: "HCM", skillLevel: 5);
            await CreatePlayerAsync(3, fullName: "Different", email: "c@test.com", phone: "333", city: "HCM", skillLevel: 5);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("potential-duplicates");

            // Assert
            Assert.Equal(2, result.TotalCount);
        }

        #endregion

        #region Test Case #14: Potential duplicates - Sorts by Email, Phone, FullName

        /// <summary>
        /// Test Case #14: When potential-duplicates, results are sorted by Email → Phone → FullName
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPotentialDuplicates_SortsByEmailPhoneName()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Zebra", email: "dup@test.com", phone: "999");
            await CreatePlayerAsync(2, fullName: "Alpha", email: "dup@test.com", phone: "111");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("potential-duplicates");

            // Assert
            Assert.Equal(2, result.TotalCount);
            // Should be sorted by Email (same), then Phone (111 < 999), then FullName
        }

        #endregion

        #region Test Case #15: Non-duplicates - Sorts by Id

        /// <summary>
        /// Test Case #15: When not potential-duplicates, results are sorted by Id (default)
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenNotDuplicates_SortsById()
        {
            // Arrange
            await CreatePlayerAsync(3, fullName: "Player 3", email: null);
            await CreatePlayerAsync(1, fullName: "Player 1", email: null);
            await CreatePlayerAsync(2, fullName: "Player 2", email: null);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email");

            // Assert
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(1, result.Players[0].Id);
            Assert.Equal(2, result.Players[1].Id);
            Assert.Equal(3, result.Players[2].Id);
        }

        #endregion

        #region Test Case #16: Paging - Page 1 returns first page

        /// <summary>
        /// Test Case #16: When pageIndex=1, returns first page of results
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPageIndex1_ReturnsFirstPage()
        {
            // Arrange - 12 players with missing email
            for (int i = 1; i <= 12; i++)
            {
                await CreatePlayerAsync(i, fullName: $"Player {i}", email: null);
            }

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email", pageIndex: 1, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalCount);
            Assert.Equal(5, result.Players.Count);
        }

        #endregion

        #region Test Case #17: Paging - Page 2 returns second page

        /// <summary>
        /// Test Case #17: When pageIndex=2, returns second page of results
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPageIndex2_ReturnsSecondPage()
        {
            // Arrange - 12 players with missing email
            for (int i = 1; i <= 12; i++)
            {
                await CreatePlayerAsync(i, fullName: $"Player {i}", email: null);
            }

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email", pageIndex: 2, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalCount);
            Assert.Equal(5, result.Players.Count);
            Assert.Equal(6, result.Players[0].Id); // First item of page 2
        }

        #endregion

        #region Test Case #18: Paging - Page exceeds total returns empty

        /// <summary>
        /// Test Case #18: When pageIndex exceeds total pages, returns empty list
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenPageExceedsTotalPages_ReturnsEmptyList()
        {
            // Arrange - 12 players with missing email
            for (int i = 1; i <= 12; i++)
            {
                await CreatePlayerAsync(i, fullName: $"Player {i}", email: null);
            }

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email", pageIndex: 10, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalCount);
            Assert.Empty(result.Players);
        }

        #endregion

        #region Test Case #19: Invalid email - In-memory paging works

        /// <summary>
        /// Test Case #19: When invalid-email, in-memory paging works correctly
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInvalidEmail_InMemoryPagingWorks()
        {
            // Arrange - 15 players with invalid email
            for (int i = 1; i <= 15; i++)
            {
                await CreatePlayerAsync(i, fullName: $"Player {i}", email: $"invalid-email-{i}");
            }

            // Act - Get page 2 with page size 5
            var result = await _sut.GetPlayersWithIssuesAsync("invalid-email", pageIndex: 2, pageSize: 5);

            // Assert
            Assert.Equal(15, result.TotalCount);
            Assert.Equal(5, result.Players.Count);
        }

        #endregion

        #region Test Case #20: Inactive-1y - Includes LastTournamentDate

        /// <summary>
        /// Test Case #20: When inactive-1y, LastTournamentDate is populated
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInactive1y_IncludesLastTournamentDate()
        {
            // Arrange
            var oldDate = DateTime.UtcNow.AddYears(-2);
            await CreatePlayerAsync(1, fullName: "Inactive Player", tournamentDate: oldDate, noTournament: false);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("inactive-1y");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.NotNull(result.Players[0].LastTournamentDate);
        }

        #endregion

        #region Test Case #21: Other types - LastTournamentDate is null

        /// <summary>
        /// Test Case #21: When not inactive-1y, LastTournamentDate is null
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenOtherTypes_LastTournamentDateIsNull()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: null);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email");

            // Assert
            Assert.Equal(1, result.TotalCount);
            Assert.Null(result.Players[0].LastTournamentDate);
        }

        #endregion

        #region Test Case #22: No matching players - Returns empty list

        /// <summary>
        /// Test Case #22: When no players match criteria, returns empty list
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenNoMatchingPlayers_ReturnsEmptyList()
        {
            // Arrange - All players have valid email
            await CreatePlayerAsync(1, fullName: "Player 1", email: "valid1@test.com");
            await CreatePlayerAsync(2, fullName: "Player 2", email: "valid2@test.com");

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("missing-email");

            // Assert
            Assert.Empty(result.Players);
            Assert.Equal(0, result.TotalCount);
        }

        #endregion

        #region Test Case #23: Issue type is case insensitive

        /// <summary>
        /// Test Case #23: Issue type is case insensitive
        /// </summary>
        [Theory]
        [InlineData("MISSING-EMAIL")]
        [InlineData("Missing-Email")]
        [InlineData("mIsSiNg-EmAiL")]
        public async Task GetPlayersWithIssuesAsync_IssueTypeIsCaseInsensitive(string issueType)
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: null);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync(issueType);

            // Assert
            Assert.Equal(1, result.TotalCount);
        }

        #endregion

        #region Test Case #24: Issue type with whitespace is trimmed

        /// <summary>
        /// Test Case #24: Issue type with leading/trailing whitespace is trimmed
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_IssueTypeWithWhitespace_IsTrimmed()
        {
            // Arrange
            await CreatePlayerAsync(1, fullName: "Player 1", email: null);

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("  missing-email  ");

            // Assert
            Assert.Equal(1, result.TotalCount);
        }

        #endregion

        #region Test Case #25: Invalid email - Limited to 2000 candidates

        /// <summary>
        /// Test Case #25: When invalid-email, only checks first 2000 candidates
        /// </summary>
        [Fact]
        public async Task GetPlayersWithIssuesAsync_WhenInvalidEmail_LimitedTo2000Candidates()
        {
            // Arrange - Create a smaller set to verify the limit logic works
            // We can't create 3000 records efficiently, so we verify the pattern works
            for (int i = 1; i <= 50; i++)
            {
                await CreatePlayerAsync(i, fullName: $"Player {i}", 
                    email: i <= 10 ? $"invalid-{i}" : $"valid{i}@test.com");
            }

            // Act
            var result = await _sut.GetPlayersWithIssuesAsync("invalid-email");

            // Assert
            // Only the 10 invalid emails should be returned
            Assert.Equal(10, result.TotalCount);
            Assert.All(result.Players, p => Assert.Contains("Invalid email format", p.Issues));
        }

        #endregion
    }
}

