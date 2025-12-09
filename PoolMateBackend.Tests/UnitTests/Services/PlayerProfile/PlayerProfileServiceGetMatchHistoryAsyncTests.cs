using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using TournamentModel = PoolMate.Api.Models.Tournament;

namespace PoolMateBackend.Tests.UnitTests.Services.PlayerProfile
{
    /// <summary>
    /// Unit Tests for PlayerProfileService.GetMatchHistoryAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 18 (based on GetMatchHistoryAsync_TestCases.md)
    /// </summary>
    public class PlayerProfileServiceGetMatchHistoryAsyncTests : IDisposable
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
        public PlayerProfileServiceGetMatchHistoryAsyncTests()
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
        private async Task<TournamentModel> CreateTournamentAsync(
            int id,
            string name = "Test Tournament",
            GameType gameType = GameType.NineBall,
            DateTime? startUtc = null)
        {
            var tournament = new TournamentModel
            {
                Id = id,
                Name = name,
                GameType = gameType,
                StartUtc = startUtc ?? DateTime.UtcNow,
                OwnerUserId = "owner-1"
            };
            _dbContext.Tournaments.Add(tournament);
            await _dbContext.SaveChangesAsync();
            return tournament;
        }

        private async Task<TournamentStage> CreateStageAsync(int tournamentId)
        {
            var stage = new TournamentStage
            {
                TournamentId = tournamentId,
                StageNo = 1,
                Type = BracketType.SingleElimination,
                Status = StageStatus.InProgress
            };
            _dbContext.TournamentStages.Add(stage);
            await _dbContext.SaveChangesAsync();
            return stage;
        }

        private async Task<TournamentPlayer> CreateTournamentPlayerAsync(
            int id,
            int? playerId,
            int tournamentId,
            string displayName = "Player")
        {
            var tp = new TournamentPlayer
            {
                Id = id,
                PlayerId = playerId,
                TournamentId = tournamentId,
                DisplayName = displayName
            };
            _dbContext.TournamentPlayers.Add(tp);
            await _dbContext.SaveChangesAsync();
            return tp;
        }

        private async Task<Match> CreateMatchAsync(
            int id,
            int tournamentId,
            int stageId,
            int? player1TpId,
            int? player2TpId,
            int? winnerTpId,
            MatchStatus status = MatchStatus.Completed,
            int? scoreP1 = null,
            int? scoreP2 = null,
            int? raceTo = null,
            int roundNo = 1,
            DateTime? scheduledUtc = null)
        {
            var match = new Match
            {
                Id = id,
                TournamentId = tournamentId,
                StageId = stageId,
                Player1TpId = player1TpId,
                Player2TpId = player2TpId,
                WinnerTpId = winnerTpId,
                Status = status,
                ScoreP1 = scoreP1,
                ScoreP2 = scoreP2,
                RaceTo = raceTo,
                RoundNo = roundNo,
                PositionInRound = 1,
                Bracket = BracketSide.Winners,
                ScheduledUtc = scheduledUtc ?? DateTime.UtcNow,
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }
            };
            _dbContext.Matches.Add(match);
            await _dbContext.SaveChangesAsync();
            return match;
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: No matches for player - Returns empty paging list

        /// <summary>
        /// Test Case #1: When player has no matches, returns empty PagingList
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayerHasNoMatches_ReturnsEmptyPagingList()
        {
            // Arrange
            var playerId = 999;
            // No matches added to DB

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalRecords);
            Assert.Equal(1, result.PageIndex);
            Assert.Equal(20, result.PageSize);
        }

        #endregion

        #region Test Case #2: Player is Player1 - Returns correct perspective

        /// <summary>
        /// Test Case #2: When player is Player1, returns correct score perspective
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayerIsPlayer1_ReturnsCorrectPerspective()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100,
                scoreP1: 5,
                scoreP2: 3
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            var item = result.Items[0];
            Assert.Equal("5 - 3", item.Score); // Player1's perspective
            Assert.Equal("Player Two", item.OpponentName);
            Assert.Equal(2, item.OpponentId);
            Assert.Equal("Win", item.Result);
        }

        #endregion

        #region Test Case #3: Player is Player2 - Returns swapped score

        /// <summary>
        /// Test Case #3: When player is Player2, returns swapped score
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayerIsPlayer2_ReturnsSwappedScore()
        {
            // Arrange
            var playerId = 2;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, 1, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, playerId, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100, // Player1 wins
                scoreP1: 5,
                scoreP2: 3
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            var item = result.Items[0];
            Assert.Equal("3 - 5", item.Score); // Swapped! Player2's perspective
            Assert.Equal("Player One", item.OpponentName);
            Assert.Equal(1, item.OpponentId);
            Assert.Equal("Loss", item.Result); // Player2 lost
        }

        #endregion

        #region Test Case #4: Player1 has Bye opponent

        /// <summary>
        /// Test Case #4: When Player2Tp is null (bye), returns "Bye" as opponent
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayer1HasByeOpponent_ReturnsOpponentNameBye()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            // No Player2 - this is a BYE

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: null, // BYE
                winnerTpId: 100
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            var item = result.Items[0];
            Assert.Equal("Bye", item.OpponentName);
            Assert.Null(item.OpponentId);
        }

        #endregion

        #region Test Case #5: Player2 has Bye opponent

        /// <summary>
        /// Test Case #5: When Player1Tp is null (bye for player2), returns "Bye" as opponent
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayer2HasByeOpponent_ReturnsOpponentNameBye()
        {
            // Arrange
            var playerId = 2;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            // No Player1 - Player2 gets a BYE
            var player2Tp = await CreateTournamentPlayerAsync(200, playerId, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: null, // BYE
                player2TpId: 200,
                winnerTpId: 200
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            var item = result.Items[0];
            Assert.Equal("Bye", item.OpponentName);
            Assert.Null(item.OpponentId);
        }

        #endregion

        #region Test Case #6: Player wins - Result is Win

        /// <summary>
        /// Test Case #6: When player wins match, Result is "Win"
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayerWins_ResultIsWin()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100, // Player1 wins
                scoreP1: 5,
                scoreP2: 2
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("Win", result.Items[0].Result);
        }

        #endregion

        #region Test Case #7: Player loses - Result is Loss

        /// <summary>
        /// Test Case #7: When player loses match, Result is "Loss"
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPlayerLoses_ResultIsLoss()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 200, // Player2 wins (Player1 loses)
                scoreP1: 2,
                scoreP2: 5
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("Loss", result.Items[0].Result);
        }

        #endregion

        #region Test Case #8: Scores are null - Defaults to zero

        /// <summary>
        /// Test Case #8: When scores are null, defaults to "0 - 0"
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenScoresAreNull_DefaultsToZero()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100,
                scoreP1: null, // NULL
                scoreP2: null  // NULL
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("0 - 0", result.Items[0].Score);
        }

        #endregion

        #region Test Case #9: ScoreP1 is null only - Defaults P1 to zero

        /// <summary>
        /// Test Case #9: When only ScoreP1 is null, defaults P1 to zero
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenScoreP1IsNullOnly_DefaultsP1ToZero()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 200,
                scoreP1: null, // NULL
                scoreP2: 3
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("0 - 3", result.Items[0].Score); // P1 defaulted to 0
        }

        #endregion

        #region Test Case #10: RaceTo is null - Defaults to zero

        /// <summary>
        /// Test Case #10: When RaceTo is null, defaults to 0
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenRaceToIsNull_DefaultsToZero()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100,
                raceTo: null // NULL
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal(0, result.Items[0].RaceTo);
        }

        #endregion

        #region Test Case #11: Only Completed matches are included

        /// <summary>
        /// Test Case #11: Only matches with Status = Completed are included
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_OnlyIncludesCompletedMatches()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            // 3 Completed
            await CreateMatchAsync(1, 1, stage.Id, 100, 200, 100, MatchStatus.Completed);
            await CreateMatchAsync(2, 1, stage.Id, 100, 200, 100, MatchStatus.Completed);
            await CreateMatchAsync(3, 1, stage.Id, 100, 200, 200, MatchStatus.Completed);

            // 2 InProgress - NOT included
            await CreateMatchAsync(4, 1, stage.Id, 100, 200, null, MatchStatus.InProgress);
            await CreateMatchAsync(5, 1, stage.Id, 100, 200, null, MatchStatus.InProgress);

            // 1 NotStarted - NOT included
            await CreateMatchAsync(6, 1, stage.Id, 100, 200, null, MatchStatus.NotStarted);

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Equal(3, result.TotalRecords); // Only Completed
            Assert.Equal(3, result.Items.Count);
        }

        #endregion

        #region Test Case #12: Sorts by Tournament date desc then RoundNo desc

        /// <summary>
        /// Test Case #12: Matches are sorted by Tournament.StartUtc desc, then RoundNo desc
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_SortsByTournamentDateDescThenRoundNoDesc()
        {
            // Arrange
            var playerId = 1;
            var jan2024 = new DateTime(2024, 1, 15);
            var feb2024 = new DateTime(2024, 2, 15);

            var tournamentJan = await CreateTournamentAsync(1, "Jan Tournament", GameType.NineBall, jan2024);
            var tournamentFeb = await CreateTournamentAsync(2, "Feb Tournament", GameType.NineBall, feb2024);

            var stageJan = await CreateStageAsync(1);
            var stageFeb = await CreateStageAsync(2);

            // Create TournamentPlayers for both tournaments
            await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            await CreateTournamentPlayerAsync(101, playerId, 2, "Player One");
            await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");
            await CreateTournamentPlayerAsync(201, 2, 2, "Player Two");

            // Jan, Round 2
            await CreateMatchAsync(1, 1, stageJan.Id, 100, 200, 100, roundNo: 2);
            // Jan, Round 1
            await CreateMatchAsync(2, 1, stageJan.Id, 100, 200, 100, roundNo: 1);
            // Feb, Round 1
            await CreateMatchAsync(3, 2, stageFeb.Id, 101, 201, 101, roundNo: 1);

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert - Expected order: Feb-R1, Jan-R2, Jan-R1
            Assert.Equal(3, result.Items.Count);
            Assert.Equal("Feb Tournament", result.Items[0].TournamentName);
            Assert.Equal("Round 1", result.Items[0].RoundName);
            Assert.Equal("Jan Tournament", result.Items[1].TournamentName);
            Assert.Equal("Round 2", result.Items[1].RoundName);
            Assert.Equal("Jan Tournament", result.Items[2].TournamentName);
            Assert.Equal("Round 1", result.Items[2].RoundName);
        }

        #endregion

        #region Test Case #13: Page 1 returns first page items

        /// <summary>
        /// Test Case #13: When pageIndex = 1, returns first page items
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPageIndex1_ReturnsFirstPageItems()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            // Create 12 matches
            for (int i = 1; i <= 12; i++)
            {
                await CreateMatchAsync(i, 1, stage.Id, 100, 200, 100);
            }

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId, pageIndex: 1, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalRecords);
            Assert.Equal(5, result.Items.Count); // Page 1 has 5 items
            Assert.Equal(1, result.PageIndex);
            Assert.Equal(5, result.PageSize);
        }

        #endregion

        #region Test Case #14: Page 2 returns second page items

        /// <summary>
        /// Test Case #14: When pageIndex = 2, returns second page items
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPageIndex2_ReturnsSecondPageItems()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            // Create 12 matches
            for (int i = 1; i <= 12; i++)
            {
                await CreateMatchAsync(i, 1, stage.Id, 100, 200, 100);
            }

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId, pageIndex: 2, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalRecords);
            Assert.Equal(5, result.Items.Count); // Page 2 has 5 items
            Assert.Equal(2, result.PageIndex);
        }

        #endregion

        #region Test Case #15: Page exceeds total pages - Returns empty items

        /// <summary>
        /// Test Case #15: When pageIndex exceeds total pages, returns empty items
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPageIndexExceedsTotalPages_ReturnsEmptyItems()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            // Create 12 matches
            for (int i = 1; i <= 12; i++)
            {
                await CreateMatchAsync(i, 1, stage.Id, 100, 200, 100);
            }

            // Act - Page 10 with pageSize 5 = items 46-50, but only 12 exist
            var result = await _sut.GetMatchHistoryAsync(playerId, pageIndex: 10, pageSize: 5);

            // Assert
            Assert.Equal(12, result.TotalRecords);
            Assert.Empty(result.Items); // No items on page 10
            Assert.Equal(10, result.PageIndex);
        }

        #endregion

        #region Test Case #16: PageSize exceeds total count - Returns all items

        /// <summary>
        /// Test Case #16: When pageSize exceeds total count, returns all items
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_WhenPageSizeExceedsTotalCount_ReturnsAllItems()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            // Create 12 matches
            for (int i = 1; i <= 12; i++)
            {
                await CreateMatchAsync(i, 1, stage.Id, 100, 200, 100);
            }

            // Act - PageSize 100, but only 12 matches
            var result = await _sut.GetMatchHistoryAsync(playerId, pageIndex: 1, pageSize: 100);

            // Assert
            Assert.Equal(12, result.TotalRecords);
            Assert.Equal(12, result.Items.Count); // All 12 items
        }

        #endregion

        #region Test Case #17: Maps all fields correctly

        /// <summary>
        /// Test Case #17: All fields are mapped correctly in MatchHistoryDto
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_MapsAllFieldsCorrectly()
        {
            // Arrange
            var playerId = 1;
            var tournamentDate = new DateTime(2024, 6, 15);
            var matchDate = new DateTime(2024, 6, 15, 14, 30, 0);

            var tournament = await CreateTournamentAsync(1, "Championship 2024", GameType.EightBall, tournamentDate);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "John Doe");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Jane Smith");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100,
                scoreP1: 7,
                scoreP2: 4,
                raceTo: 7,
                roundNo: 3,
                scheduledUtc: matchDate
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            var item = result.Items[0];

            Assert.Equal(1, item.MatchId);
            Assert.Equal(1, item.TournamentId);
            Assert.Equal("Championship 2024", item.TournamentName);
            Assert.Equal(tournamentDate, item.TournamentDate);
            Assert.Equal("EightBall", item.GameType);
            Assert.Equal("SingleElimination", item.StageType);
            Assert.Equal("Winners", item.BracketSide);
            Assert.Equal("Round 3", item.RoundName);
            Assert.Equal("Jane Smith", item.OpponentName);
            Assert.Equal(2, item.OpponentId);
            Assert.Equal("7 - 4", item.Score);
            Assert.Equal(7, item.RaceTo);
            Assert.Equal("Win", item.Result);
            Assert.Equal(matchDate, item.MatchDate);
        }

        #endregion

        #region Test Case #18: RoundName formatted correctly

        /// <summary>
        /// Test Case #18: RoundName is formatted as "Round X"
        /// </summary>
        [Fact]
        public async Task GetMatchHistoryAsync_RoundNameFormattedCorrectly()
        {
            // Arrange
            var playerId = 1;
            var tournament = await CreateTournamentAsync(1);
            var stage = await CreateStageAsync(1);

            var player1Tp = await CreateTournamentPlayerAsync(100, playerId, 1, "Player One");
            var player2Tp = await CreateTournamentPlayerAsync(200, 2, 1, "Player Two");

            await CreateMatchAsync(
                id: 1,
                tournamentId: 1,
                stageId: stage.Id,
                player1TpId: 100,
                player2TpId: 200,
                winnerTpId: 100,
                roundNo: 5 // Round 5
            );

            // Act
            var result = await _sut.GetMatchHistoryAsync(playerId);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("Round 5", result.Items[0].RoundName);
        }

        #endregion
    }
}

