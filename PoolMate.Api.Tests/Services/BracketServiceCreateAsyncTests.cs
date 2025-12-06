using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Data;
using PoolMate.Api.Services;
using PoolMate.Api.Models;
using PoolMate.Api.Hubs;
using PoolMate.Api.Tests.Fixtures;
using PoolMate.Api.Dtos.Tournament;
using Microsoft.EntityFrameworkCore;

namespace PoolMate.Api.Tests.Services
{
    /// <summary>
    /// Unit tests cho BracketService - hàm CreateAsync (logic ph?c t?p)
    /// </summary>
    public class BracketServiceCreateAsyncTests : IDisposable
    {
        private readonly TestDatabaseFixture _fixture;
        private readonly Mock<IMatchLockService> _mockLockService;
        private readonly Mock<IHubContext<TournamentHub>> _mockHubContext;
        private readonly Mock<ILogger<BracketService>> _mockLogger;
        private readonly BracketService _bracketService;

        public BracketServiceCreateAsyncTests()
        {
            _fixture = new TestDatabaseFixture();
            _mockLockService = new Mock<IMatchLockService>();
            _mockHubContext = new Mock<IHubContext<TournamentHub>>();
            _mockLogger = new Mock<ILogger<BracketService>>();

            _bracketService = new BracketService(
                _fixture.Context,
                _mockLockService.Object,
                _mockHubContext.Object,
                _mockLogger.Object
            );
        }

        #region D??ng tính - Success Cases

        [Fact]
        public async Task CreateAsync_WithValidPlayers_ShouldCreateSingleEliminationBracket()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            tournament.BracketType = BracketType.SingleElimination;
            tournament.Format = 1;
            
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var player1 = MockDataFactory.CreateMockPlayer("Player One");
            var player2 = MockDataFactory.CreateMockPlayer("Player Two");
            
            var tp1 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player1.Id, 1);
            var tp2 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player2.Id, 2);
            
            _fixture.Context.TournamentPlayers.AddRange(tp1, tp2);
            await _fixture.Context.SaveChangesAsync();

            var request = new CreateBracketRequest { Type = BracketCreationType.Automatic };

            // Act
            await _bracketService.CreateAsync(tournament.Id, request, CancellationToken.None);

            // Assert
            var matches = await _fixture.Context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();

            Assert.NotEmpty(matches);
            Assert.All(matches, m => Assert.NotNull(m.StageId));
        }

        [Fact]
        public async Task CreateAsync_WithMultiplePlayers_ShouldCreateBracketWithCorrectSize()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            tournament.BracketType = BracketType.SingleElimination;
            
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            // Create 8 players
            var players = Enumerable.Range(1, 8)
                .Select(i => MockDataFactory.CreateMockPlayer($"Player {i}"))
                .ToList();
            
            _fixture.Context.Players.AddRange(players);
            await _fixture.Context.SaveChangesAsync();

            var tournamentPlayers = players
                .Select((p, i) => MockDataFactory.CreateMockTournamentPlayer(tournament.Id, p.Id, i + 1))
                .ToList();
            
            _fixture.Context.TournamentPlayers.AddRange(tournamentPlayers);
            await _fixture.Context.SaveChangesAsync();

            // Act
            await _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var firstRoundMatches = await _fixture.Context.Matches
                .Where(m => m.TournamentId == tournament.Id && m.RoundNo == 1)
                .ToListAsync();

            Assert.Equal(4, firstRoundMatches.Count); // 8 players = 4 matches in round 1
        }

        #endregion

        #region Âm tính - Error Cases

        [Fact]
        public async Task CreateAsync_WithLessThanTwoPlayers_ShouldThrowValidationException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var player = MockDataFactory.CreateMockPlayer("Solo Player");
            var tp = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player.Id, 1);
            
            _fixture.Context.TournamentPlayers.Add(tp);
            await _fixture.Context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<PoolMate.Api.Common.ValidationException>(
                () => _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None)
            );
            
            Assert.Contains("At least two players", exception.Message);
        }

        [Fact]
        public async Task CreateAsync_WhenBracketAlreadyExists_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var stage = new TournamentStage
            {
                TournamentId = tournament.Id,
                StageNo = 1,
                Type = BracketType.SingleElimination,
                Status = StageStatus.NotStarted
            };
            _fixture.Context.TournamentStages.Add(stage);
            await _fixture.Context.SaveChangesAsync();

            var match = new Match
            {
                TournamentId = tournament.Id,
                StageId = stage.Id,
                Bracket = BracketSide.Knockout,
                RoundNo = 1,
                PositionInRound = 1,
                Status = MatchStatus.NotStarted
            };
            _fixture.Context.Matches.Add(match);
            await _fixture.Context.SaveChangesAsync();

            var player1 = MockDataFactory.CreateMockPlayer("Player One");
            var player2 = MockDataFactory.CreateMockPlayer("Player Two");
            
            var tp1 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player1.Id, 1);
            var tp2 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player2.Id, 2);
            
            _fixture.Context.TournamentPlayers.AddRange(tp1, tp2);
            await _fixture.Context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None)
            );
            
            Assert.Contains("Bracket already created", exception.Message);
        }

        [Fact]
        public async Task CreateAsync_MultiStageWithInsufficientPlayers_ShouldThrowValidationException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            tournament.IsMultiStage = true;
            tournament.AdvanceToStage2Count = 6; // Requires 6 players minimum
            tournament.BracketType = BracketType.DoubleElimination;
            
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            // Only 3 players
            var players = Enumerable.Range(1, 3)
                .Select(i => MockDataFactory.CreateMockPlayer($"Player {i}"))
                .ToList();
            
            _fixture.Context.Players.AddRange(players);
            await _fixture.Context.SaveChangesAsync();

            var tournamentPlayers = players
                .Select((p, i) => MockDataFactory.CreateMockTournamentPlayer(tournament.Id, p.Id, i + 1))
                .ToList();
            
            _fixture.Context.TournamentPlayers.AddRange(tournamentPlayers);
            await _fixture.Context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<PoolMate.Api.Common.ValidationException>(
                () => _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None)
            );
            
            Assert.Contains("Multi-stage bracket requires at least", exception.Message);
        }

        [Fact]
        public async Task CreateAsync_MultiStageWithSingleElimination_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            tournament.IsMultiStage = true;
            tournament.BracketType = BracketType.SingleElimination; // Invalid for multi-stage
            tournament.AdvanceToStage2Count = 2;
            
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var player1 = MockDataFactory.CreateMockPlayer("Player One");
            var player2 = MockDataFactory.CreateMockPlayer("Player Two");
            
            var tp1 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player1.Id, 1);
            var tp2 = MockDataFactory.CreateMockTournamentPlayer(tournament.Id, player2.Id, 2);
            
            _fixture.Context.TournamentPlayers.AddRange(tp1, tp2);
            await _fixture.Context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None)
            );
            
            Assert.Contains("multi-stage cannot use Single Elimination", exception.Message);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task CreateAsync_WithBracketSizeEstimate_ShouldUseEstimateIfValid()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            tournament.BracketType = BracketType.SingleElimination;
            tournament.BracketSizeEstimate = 16; // Estimate for 8 players
            
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var players = Enumerable.Range(1, 8)
                .Select(i => MockDataFactory.CreateMockPlayer($"Player {i}"))
                .ToList();
            
            _fixture.Context.Players.AddRange(players);
            await _fixture.Context.SaveChangesAsync();

            var tournamentPlayers = players
                .Select((p, i) => MockDataFactory.CreateMockTournamentPlayer(tournament.Id, p.Id, i + 1))
                .ToList();
            
            _fixture.Context.TournamentPlayers.AddRange(tournamentPlayers);
            await _fixture.Context.SaveChangesAsync();

            // Act
            await _bracketService.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var allMatches = await _fixture.Context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();

            // With size 16, we should have more slots than minimal 8
            Assert.NotEmpty(allMatches);
        }

        #endregion

        public void Dispose()
        {
            _fixture?.Dispose();
        }
    }
}
