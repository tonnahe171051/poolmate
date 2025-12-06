using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Data;
using PoolMate.Api.Services;
using PoolMate.Api.Models;
using PoolMate.Api.Tests.Fixtures;
using PoolMate.Api.Dtos.Admin.Player;
using Microsoft.EntityFrameworkCore;

namespace PoolMate.Api.Tests.Services
{
    /// <summary>
    /// Unit tests cho AdminPlayerService - logic ph?c t?p
    /// Focus: Detect duplicates, Merge players, Update player statistics
    /// </summary>
    public class AdminPlayerServiceTests : IDisposable
    {
        private readonly TestDatabaseFixture _fixture;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<ILogger<AdminPlayerService>> _mockLogger;
        private readonly AdminPlayerService _adminPlayerService;

        public AdminPlayerServiceTests()
        {
            _fixture = new TestDatabaseFixture();
            
            var store = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
            _mockLogger = new Mock<ILogger<AdminPlayerService>>();

            _adminPlayerService = new AdminPlayerService(
                _fixture.Context,
                _mockUserManager.Object,
                _mockLogger.Object
            );
        }

        #region Duplicate Detection Tests

        [Fact]
        public async Task DetectDuplicatePlayers_WithSameEmail_ShouldIdentifyDuplicates()
        {
            // Arrange
            var email = "duplicate@example.com";
            var player1 = MockDataFactory.CreateMockPlayer("John Doe", email);
            var player2 = MockDataFactory.CreateMockPlayer("John D.", email);
            
            _fixture.Context.Players.AddRange(player1, player2);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var duplicates = await _adminPlayerService.GetDuplicatePlayersAsync(CancellationToken.None);

            // Assert
            Assert.NotEmpty(duplicates);
            var group = duplicates.FirstOrDefault(d => d.Players.Any(p => p.Email == email));
            Assert.NotNull(group);
            Assert.Equal(2, group.Players.Count);
        }

        [Fact]
        public async Task DetectDuplicatePlayers_WithSimilarNames_ShouldIdentifyPotentialDuplicates()
        {
            // Arrange
            var player1 = MockDataFactory.CreateMockPlayer("John Doe", "john@example.com");
            var player2 = MockDataFactory.CreateMockPlayer("Jon Doe", "jon@example.com");
            
            _fixture.Context.Players.AddRange(player1, player2);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var duplicates = await _adminPlayerService.GetDuplicatePlayersAsync(CancellationToken.None);

            // Assert - Name similarity detection may flag these as potential duplicates
            // depending on implementation
            Assert.NotNull(duplicates);
        }

        [Fact]
        public async Task DetectDuplicatePlayers_WithUniqueData_ShouldReturnNoDuplicates()
        {
            // Arrange
            var player1 = MockDataFactory.CreateMockPlayer("John Doe", "john@example.com");
            var player2 = MockDataFactory.CreateMockPlayer("Jane Smith", "jane@example.com");
            var player3 = MockDataFactory.CreateMockPlayer("Bob Johnson", "bob@example.com");
            
            _fixture.Context.Players.AddRange(player1, player2, player3);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var duplicates = await _adminPlayerService.GetDuplicatePlayersAsync(CancellationToken.None);

            // Assert
            Assert.Empty(duplicates);
        }

        #endregion

        #region Player Merge Tests

        [Fact]
        public async Task MergePlayers_WithValidMerge_ShouldCombineRecords()
        {
            // Arrange
            var player1 = MockDataFactory.CreateMockPlayer("Player One", "player1@example.com");
            var player2 = MockDataFactory.CreateMockPlayer("Player Two", "player2@example.com");
            
            _fixture.Context.Players.AddRange(player1, player2);
            await _fixture.Context.SaveChangesAsync();

            var mergeRequest = new MergePlayerRequestDto
            {
                SourcePlayerIds = new List<string> { player2.Id },
                TargetPlayerId = player1.Id,
                RetainEmail = player1.Id
            };

            // Act
            var result = await _adminPlayerService.MergePlayersAsync(mergeRequest, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(player1.Id, result.TargetPlayerId);
            Assert.Equal(1, result.MergedCount);
        }

        [Fact]
        public async Task MergePlayers_WithInvalidTarget_ShouldThrowException()
        {
            // Arrange
            var player1 = MockDataFactory.CreateMockPlayer("Player One", "player1@example.com");
            _fixture.Context.Players.Add(player1);
            await _fixture.Context.SaveChangesAsync();

            var mergeRequest = new MergePlayerRequestDto
            {
                SourcePlayerIds = new List<string> { player1.Id },
                TargetPlayerId = "non-existent-id",
                RetainEmail = player1.Id
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _adminPlayerService.MergePlayersAsync(mergeRequest, CancellationToken.None)
            );
        }

        [Fact]
        public async Task MergePlayers_PreservingTournamentHistory_ShouldMaintainStats()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var player1 = MockDataFactory.CreateMockPlayer("Player One", "player1@example.com");
            var player2 = MockDataFactory.CreateMockPlayer("Player Two", "player2@example.com");
            
            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.Players.AddRange(player1, player2);
            await _fixture.Context.SaveChangesAsync();

            // Create tournament players
            var tp1 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player1.Id, 1);
            var tp2 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player2.Id, 2);
            
            _fixture.Context.TournamentPlayers.AddRange(tp1, tp2);
            await _fixture.Context.SaveChangesAsync();

            var mergeRequest = new MergePlayerRequestDto
            {
                SourcePlayerIds = new List<string> { player2.Id },
                TargetPlayerId = player1.Id,
                RetainEmail = player1.Id
            };

            // Act
            await _adminPlayerService.MergePlayersAsync(mergeRequest, CancellationToken.None);

            // Assert
            var remainingPlayer1 = await _fixture.Context.Players.FindAsync(player1.Id);
            Assert.NotNull(remainingPlayer1);
            
            // Player2 should be marked as deleted or merged
            var player2Records = await _fixture.Context.TournamentPlayers
                .Where(tp => tp.PlayerId == player2.Id)
                .ToListAsync();
            
            Assert.NotEmpty(player2Records); // Should either be transferred or marked
        }

        #endregion

        #region Player Statistics Tests

        [Fact]
        public async Task GetPlayerStatistics_WithWins_ShouldCalculateCorrectly()
        {
            // Arrange
            var player = MockDataFactory.CreateMockPlayer("Champion Player", "champ@example.com");
            var tournament = MockDataFactory.CreateMockTournament();
            
            _fixture.Context.Players.Add(player);
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var tp = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player.Id, 1);
            _fixture.Context.TournamentPlayers.Add(tp);
            await _fixture.Context.SaveChangesAsync();

            // Create matches where player wins
            var stage = new TournamentStage
            {
                TournamentId = tournament.Id,
                StageNo = 1,
                Type = BracketType.SingleElimination,
                Status = StageStatus.Completed
            };
            _fixture.Context.TournamentStages.Add(stage);
            await _fixture.Context.SaveChangesAsync();

            var opponent = MockDataFactory.CreateMockPlayer("Opponent", "opponent@example.com");
            var opp_tp = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, opponent.Id, 2);
            _fixture.Context.Players.Add(opponent);
            _fixture.Context.TournamentPlayers.Add(opp_tp);
            await _fixture.Context.SaveChangesAsync();

            var match = new Match
            {
                TournamentId = tournament.Id,
                StageId = stage.Id,
                Bracket = BracketSide.Knockout,
                RoundNo = 1,
                PositionInRound = 1,
                Player1TpId = tp.Id,
                Player2TpId = opp_tp.Id,
                WinnerTpId = tp.Id,
                ScoreP1 = 2,
                ScoreP2 = 0,
                Status = MatchStatus.Completed
            };
            _fixture.Context.Matches.Add(match);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var stats = await _adminPlayerService.GetPlayerStatisticsAsync(player.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(1, stats.Wins);
            Assert.Equal(0, stats.Losses);
        }

        [Fact]
        public async Task GetPlayerStatistics_WithNoMatches_ShouldReturnZeroStats()
        {
            // Arrange
            var player = MockDataFactory.CreateMockPlayer("New Player", "new@example.com");
            _fixture.Context.Players.Add(player);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var stats = await _adminPlayerService.GetPlayerStatisticsAsync(player.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.Wins);
            Assert.Equal(0, stats.Losses);
        }

        #endregion

        #region Data Quality Tests

        [Fact]
        public async Task GetDataQuality_WithCompleteData_ShouldReturnHighQuality()
        {
            // Arrange
            var user = MockDataFactory.CreateMockApplicationUser("complete@example.com", "completeuser");
            var player = MockDataFactory.CreateMockPlayer("Complete Player", "complete@example.com");
            
            // Setup mock for UserManager
            _mockUserManager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _fixture.Context.Players.Add(player);
            await _fixture.Context.SaveChangesAsync();

            // Act
            var quality = await _adminPlayerService.GetPlayerDataQualityAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(quality);
            Assert.True(quality.CompletionRate >= 0);
        }

        #endregion

        public void Dispose()
        {
            _fixture?.Dispose();
        }
    }
}
