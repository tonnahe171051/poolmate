using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using TournamentModel = PoolMate.Api.Models.Tournament;

namespace PoolMateBackend.Tests.UnitTests.Services.PlayerProfile
{
    /// <summary>
    /// Unit Tests for PlayerProfileService.GetPlayerStatsAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 18 (based on GetPlayerStatsAsync_TestCases.md)
    /// </summary>
    public class PlayerProfileServiceGetPlayerStatsAsyncTests : IDisposable
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
        public PlayerProfileServiceGetPlayerStatsAsyncTests()
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
            GameType gameType = GameType.NineBall,
            DateTime? startUtc = null)
        {
            var tournament = new TournamentModel
            {
                Id = id,
                Name = $"Tournament {id}",
                GameType = gameType,
                StartUtc = startUtc ?? DateTime.UtcNow,
                OwnerUserId = "owner-1"
            };
            _dbContext.Tournaments.Add(tournament);
            await _dbContext.SaveChangesAsync();
            return tournament;
        }

        private async Task<TournamentPlayer> CreateTournamentPlayerAsync(
            int id,
            int playerId,
            int tournamentId)
        {
            var tp = new TournamentPlayer
            {
                Id = id,
                PlayerId = playerId,
                TournamentId = tournamentId,
                DisplayName = $"Player {playerId}"
            };
            _dbContext.TournamentPlayers.Add(tp);
            await _dbContext.SaveChangesAsync();
            return tp;
        }

        private async Task<Match> CreateMatchAsync(
            int id,
            int tournamentId,
            int? player1TpId,
            int? player2TpId,
            int? winnerTpId,
            MatchStatus status = MatchStatus.Completed,
            DateTime? scheduledUtc = null)
        {
            // Ensure stage exists
            var stage = await _dbContext.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournamentId);
            if (stage == null)
            {
                stage = new TournamentStage
                {
                    TournamentId = tournamentId,
                    StageNo = 1,
                    Type = BracketType.SingleElimination,
                    Status = StageStatus.InProgress
                };
                _dbContext.TournamentStages.Add(stage);
                await _dbContext.SaveChangesAsync();
            }

            var match = new Match
            {
                Id = id,
                TournamentId = tournamentId,
                StageId = stage.Id,
                Player1TpId = player1TpId,
                Player2TpId = player2TpId,
                WinnerTpId = winnerTpId,
                Status = status,
                ScheduledUtc = scheduledUtc ?? DateTime.UtcNow,
                RoundNo = 1,
                PositionInRound = 1,
                Bracket = BracketSide.Winners,
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } // Required for InMemory DB
            };
            _dbContext.Matches.Add(match);
            await _dbContext.SaveChangesAsync();
            return match;
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: No TournamentPlayers - Returns empty stats

        /// <summary>
        /// Test Case #1: When player has no TournamentPlayers, returns empty PlayerStatsDto
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasNoTournamentPlayers_ReturnsEmptyStats()
        {
            // Arrange
            var playerId = 999; // Non-existent player in TournamentPlayers

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalMatches);
            Assert.Equal(0, result.TotalWins);
            Assert.Equal(0, result.TotalLosses);
            Assert.Equal(0, result.WinRate);
            Assert.Equal(0, result.TotalTournaments);
            Assert.Empty(result.RecentForm);
            Assert.Empty(result.StatsByGameType);
        }

        #endregion

        #region Test Case #2: Has tournaments but no completed matches

        /// <summary>
        /// Test Case #2: When player has tournaments but no completed matches, returns zero match stats
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasTournamentsButNoCompletedMatches_ReturnsEmptyMatchStats()
        {
            // Arrange
            var playerId = 1;
            await CreateTournamentAsync(1);
            await CreateTournamentAsync(2);
            await CreateTournamentPlayerAsync(100, playerId, 1);
            await CreateTournamentPlayerAsync(101, playerId, 2);
            
            // Create matches but NOT completed
            await CreateMatchAsync(1, 1, 100, 200, null, MatchStatus.NotStarted);
            await CreateMatchAsync(2, 2, 101, 201, null, MatchStatus.InProgress);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalTournaments);
            Assert.Equal(0, result.TotalMatches);
            Assert.Equal(0, result.TotalWins);
            Assert.Equal(0, result.TotalLosses);
            Assert.Equal(0, result.WinRate);
            Assert.Empty(result.RecentForm);
        }

        #endregion

        #region Test Case #3: Player wins match - Counts as win

        /// <summary>
        /// Test Case #3: When player wins a match, counts as win
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerWinsMatch_CountsAsWin()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            var opponentTpId = 200;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(opponentTpId, 2, 1); // Opponent
            await CreateMatchAsync(1, 1, playerTpId, opponentTpId, playerTpId); // Player wins!

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(1, result.TotalMatches);
            Assert.Equal(1, result.TotalWins);
            Assert.Equal(0, result.TotalLosses);
            Assert.Equal(100.0, result.WinRate);
            Assert.Single(result.RecentForm);
            Assert.Equal("W", result.RecentForm[0]);
        }

        #endregion

        #region Test Case #4: Player loses match - Counts as loss

        /// <summary>
        /// Test Case #4: When player loses a match (opponent wins), counts as loss
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerLosesMatch_CountsAsLoss()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            var opponentTpId = 200;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(opponentTpId, 2, 1);
            await CreateMatchAsync(1, 1, playerTpId, opponentTpId, opponentTpId); // Opponent wins!

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(1, result.TotalMatches);
            Assert.Equal(0, result.TotalWins);
            Assert.Equal(1, result.TotalLosses);
            Assert.Equal(0, result.WinRate);
            Assert.Single(result.RecentForm);
            Assert.Equal("L", result.RecentForm[0]);
        }

        #endregion

        #region Test Case #5: WinnerTpId is null - Counts as loss

        /// <summary>
        /// Test Case #5: When WinnerTpId is null in completed match, counts as loss
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenWinnerTpIdIsNull_CountsAsLoss()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            await CreateMatchAsync(1, 1, playerTpId, 200, null); // No winner recorded!

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(1, result.TotalMatches);
            Assert.Equal(0, result.TotalWins);
            Assert.Equal(1, result.TotalLosses); // Counts as loss
            Assert.Single(result.RecentForm);
            Assert.Equal("L", result.RecentForm[0]);
        }

        #endregion

        #region Test Case #6: Exactly 5 matches - RecentForm has 5 items

        /// <summary>
        /// Test Case #6: When player has exactly 5 matches, RecentForm has 5 items (boundary)
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasExactly5Matches_RecentFormHas5Items()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            var baseDate = DateTime.UtcNow;
            
            await CreateTournamentAsync(1, GameType.NineBall, baseDate);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // Create exactly 5 matches with alternating W/L
            for (int i = 1; i <= 5; i++)
            {
                var winnerTpId = i % 2 == 1 ? playerTpId : 200; // W, L, W, L, W
                await CreateMatchAsync(i, 1, playerTpId, 200, winnerTpId, 
                    MatchStatus.Completed, baseDate.AddMinutes(-i));
            }

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(5, result.TotalMatches);
            Assert.Equal(5, result.RecentForm.Count); // Exactly 5
        }

        #endregion

        #region Test Case #7: More than 5 matches - RecentForm capped at 5

        /// <summary>
        /// Test Case #7: When player has more than 5 matches, RecentForm is capped at 5 (boundary)
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasMoreThan5Matches_RecentFormCappedAt5()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            var baseDate = DateTime.UtcNow;
            
            await CreateTournamentAsync(1, GameType.NineBall, baseDate);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // Create 7 matches
            for (int i = 1; i <= 7; i++)
            {
                var winnerTpId = i % 2 == 0 ? playerTpId : 200;
                await CreateMatchAsync(i, 1, playerTpId, 200, winnerTpId,
                    MatchStatus.Completed, baseDate.AddMinutes(-i));
            }

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(7, result.TotalMatches);
            Assert.Equal(5, result.RecentForm.Count); // Capped at 5!
        }

        #endregion

        #region Test Case #8: Less than 5 matches - RecentForm has all matches

        /// <summary>
        /// Test Case #8: When player has less than 5 matches, RecentForm has all matches (boundary)
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasLessThan5Matches_RecentFormHasAllMatches()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // Create only 3 matches
            for (int i = 1; i <= 3; i++)
            {
                await CreateMatchAsync(i, 1, playerTpId, 200, playerTpId);
            }

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(3, result.TotalMatches);
            Assert.Equal(3, result.RecentForm.Count); // All 3 matches
        }

        #endregion

        #region Test Case #9: Multiple GameTypes - Groups stats by GameType

        /// <summary>
        /// Test Case #9: When player has matches in multiple GameTypes, groups stats correctly
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerHasMultipleGameTypes_GroupsStatsByGameType()
        {
            // Arrange
            var playerId = 1;
            var playerTpId1 = 100;
            var playerTpId2 = 101;
            var opponentTpId1 = 200;
            var opponentTpId2 = 201;
            
            // NineBall tournament
            await CreateTournamentAsync(1, GameType.NineBall);
            await CreateTournamentPlayerAsync(playerTpId1, playerId, 1);
            await CreateTournamentPlayerAsync(opponentTpId1, 2, 1);
            
            // EightBall tournament
            await CreateTournamentAsync(2, GameType.EightBall);
            await CreateTournamentPlayerAsync(playerTpId2, playerId, 2);
            await CreateTournamentPlayerAsync(opponentTpId2, 2, 2);
            
            // NineBall: 2 wins, 1 loss
            await CreateMatchAsync(1, 1, playerTpId1, opponentTpId1, playerTpId1);
            await CreateMatchAsync(2, 1, playerTpId1, opponentTpId1, playerTpId1);
            await CreateMatchAsync(3, 1, playerTpId1, opponentTpId1, opponentTpId1);
            
            // EightBall: 1 win, 1 loss
            await CreateMatchAsync(4, 2, playerTpId2, opponentTpId2, playerTpId2);
            await CreateMatchAsync(5, 2, playerTpId2, opponentTpId2, opponentTpId2);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(5, result.TotalMatches);
            Assert.Equal(2, result.StatsByGameType.Count);
            
            var nineBall = result.StatsByGameType.First(x => x.GameType == "NineBall");
            Assert.Equal(2, nineBall.Wins);
            Assert.Equal(1, nineBall.Losses);
            Assert.Equal(66.7, nineBall.WinRate); // Math.Round(2/3 * 100, 1)
            
            var eightBall = result.StatsByGameType.First(x => x.GameType == "EightBall");
            Assert.Equal(1, eightBall.Wins);
            Assert.Equal(1, eightBall.Losses);
            Assert.Equal(50.0, eightBall.WinRate);
        }

        #endregion

        #region Test Case #10: Calculates overall WinRate correctly

        /// <summary>
        /// Test Case #10: Calculates overall WinRate correctly
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_CalculatesOverallWinRateCorrectly()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // 3 wins, 2 losses (5 total)
            await CreateMatchAsync(1, 1, playerTpId, 200, playerTpId); // W
            await CreateMatchAsync(2, 1, playerTpId, 200, playerTpId); // W
            await CreateMatchAsync(3, 1, playerTpId, 200, playerTpId); // W
            await CreateMatchAsync(4, 1, playerTpId, 200, 200);        // L
            await CreateMatchAsync(5, 1, playerTpId, 200, 200);        // L

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(5, result.TotalMatches);
            Assert.Equal(3, result.TotalWins);
            Assert.Equal(2, result.TotalLosses);
            Assert.Equal(60.0, result.WinRate); // Math.Round(3/5 * 100, 1) = 60.0
        }

        #endregion

        #region Test Case #11: All wins - WinRate is 100

        /// <summary>
        /// Test Case #11: When all matches are wins, WinRate is 100
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenAllWins_WinRateIs100()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // 5 wins, 0 losses
            for (int i = 1; i <= 5; i++)
            {
                await CreateMatchAsync(i, 1, playerTpId, 200, playerTpId); // All wins
            }

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(5, result.TotalWins);
            Assert.Equal(0, result.TotalLosses);
            Assert.Equal(100.0, result.WinRate);
        }

        #endregion

        #region Test Case #12: All losses - WinRate is 0

        /// <summary>
        /// Test Case #12: When all matches are losses, WinRate is 0
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenAllLosses_WinRateIs0()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // 0 wins, 5 losses
            for (int i = 1; i <= 5; i++)
            {
                await CreateMatchAsync(i, 1, playerTpId, 200, 200); // All losses
            }

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(0, result.TotalWins);
            Assert.Equal(5, result.TotalLosses);
            Assert.Equal(0, result.WinRate);
        }

        #endregion

        #region Test Case #13: Calculates GameType WinRate correctly

        /// <summary>
        /// Test Case #13: Calculates WinRate per GameType correctly
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_CalculatesGameTypeWinRateCorrectly()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1, GameType.NineBall);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // NineBall: 2 wins, 2 losses (4 total)
            await CreateMatchAsync(1, 1, playerTpId, 200, playerTpId); // W
            await CreateMatchAsync(2, 1, playerTpId, 200, playerTpId); // W
            await CreateMatchAsync(3, 1, playerTpId, 200, 200);        // L
            await CreateMatchAsync(4, 1, playerTpId, 200, 200);        // L

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            var nineBall = result.StatsByGameType.First(x => x.GameType == "NineBall");
            Assert.Equal(2, nineBall.Wins);
            Assert.Equal(2, nineBall.Losses);
            Assert.Equal(50.0, nineBall.WinRate); // 2/4 * 100 = 50.0
        }

        #endregion

        #region Test Case #14: Player is Player1 - Match is included

        /// <summary>
        /// Test Case #14: When player is Player1 in match, match is included in stats
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerIsPlayer1_MatchIsIncluded()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // Player is Player1
            await CreateMatchAsync(1, 1, playerTpId, 200, playerTpId);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(1, result.TotalMatches);
            Assert.Equal(1, result.TotalWins);
        }

        #endregion

        #region Test Case #15: Player is Player2 - Match is included

        /// <summary>
        /// Test Case #15: When player is Player2 in match, match is included in stats
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_WhenPlayerIsPlayer2_MatchIsIncluded()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            var opponentTpId = 200;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(opponentTpId, 2, 1);
            
            // Player is Player2 (opponent is Player1)
            await CreateMatchAsync(1, 1, opponentTpId, playerTpId, playerTpId);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(1, result.TotalMatches);
            Assert.Equal(1, result.TotalWins);
        }

        #endregion

        #region Test Case #16: TotalTournaments equals unique TpId count

        /// <summary>
        /// Test Case #16: TotalTournaments equals number of unique TournamentPlayer entries
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_TotalTournamentsEqualsUniqueTpIdCount()
        {
            // Arrange
            var playerId = 1;
            
            // Player participates in 3 different tournaments
            await CreateTournamentAsync(1);
            await CreateTournamentAsync(2);
            await CreateTournamentAsync(3);
            await CreateTournamentPlayerAsync(100, playerId, 1);
            await CreateTournamentPlayerAsync(101, playerId, 2);
            await CreateTournamentPlayerAsync(102, playerId, 3);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(3, result.TotalTournaments);
        }

        #endregion

        #region Test Case #17: Matches sorted by Tournament date then scheduled

        /// <summary>
        /// Test Case #17: Matches are sorted by Tournament.StartUtc (desc) then ScheduledUtc (desc)
        /// RecentForm takes from newest matches first
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_MatchesSortedByTournamentDateThenScheduled()
        {
            // Arrange
            var playerId = 1;
            var playerTpId1 = 100;
            var playerTpId2 = 101;
            
            // Tournament A: January 2024 (older)
            var tournamentA = await CreateTournamentAsync(1, GameType.NineBall, new DateTime(2024, 1, 1));
            await CreateTournamentPlayerAsync(playerTpId1, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // Tournament B: February 2024 (newer)
            var tournamentB = await CreateTournamentAsync(2, GameType.NineBall, new DateTime(2024, 2, 1));
            await CreateTournamentPlayerAsync(playerTpId2, playerId, 2);
            await CreateTournamentPlayerAsync(201, 2, 2);
            
            // Match from older tournament (Loss)
            await CreateMatchAsync(1, 1, playerTpId1, 200, 200, MatchStatus.Completed, new DateTime(2024, 1, 15));
            
            // Match from newer tournament (Win)
            await CreateMatchAsync(2, 2, playerTpId2, 201, playerTpId2, MatchStatus.Completed, new DateTime(2024, 2, 15));

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(2, result.TotalMatches);
            Assert.Equal(2, result.RecentForm.Count);
            // Newest match (from Tournament B - Feb) should be first
            Assert.Equal("W", result.RecentForm[0]); // Win from Feb tournament
            Assert.Equal("L", result.RecentForm[1]); // Loss from Jan tournament
        }

        #endregion

        #region Test Case #18: Only completed matches are counted

        /// <summary>
        /// Test Case #18: Only matches with Status = Completed are counted
        /// </summary>
        [Fact]
        public async Task GetPlayerStatsAsync_OnlyCompletedMatchesAreCounted()
        {
            // Arrange
            var playerId = 1;
            var playerTpId = 100;
            
            await CreateTournamentAsync(1);
            await CreateTournamentPlayerAsync(playerTpId, playerId, 1);
            await CreateTournamentPlayerAsync(200, 2, 1);
            
            // 3 Completed
            await CreateMatchAsync(1, 1, playerTpId, 200, playerTpId, MatchStatus.Completed);
            await CreateMatchAsync(2, 1, playerTpId, 200, playerTpId, MatchStatus.Completed);
            await CreateMatchAsync(3, 1, playerTpId, 200, 200, MatchStatus.Completed);
            
            // 2 InProgress
            await CreateMatchAsync(4, 1, playerTpId, 200, null, MatchStatus.InProgress);
            await CreateMatchAsync(5, 1, playerTpId, 200, null, MatchStatus.InProgress);
            
            // 1 NotStarted
            await CreateMatchAsync(6, 1, playerTpId, 200, null, MatchStatus.NotStarted);

            // Act
            var result = await _sut.GetPlayerStatsAsync(playerId);

            // Assert
            Assert.Equal(3, result.TotalMatches); // Only Completed
            Assert.Equal(2, result.TotalWins);
            Assert.Equal(1, result.TotalLosses);
        }

        #endregion
    }
}

