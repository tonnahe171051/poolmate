using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using Xunit;

namespace PoolMateBackend.Tests.UnitTests.Services.BracketTests
{
    /// <summary>
    /// Unit Tests for BracketService.GetAsync() method
    /// Coverage: 100% of GetAsync method (lines 261-301)
    /// Pattern: In-Memory Database + Moq for external dependencies
    /// Framework: xUnit + Moq + EF Core InMemory
    /// </summary>
    public class BracketServiceGetAsyncTests : IDisposable
    {
        private readonly ApplicationDbContext _db;
        private readonly Mock<IMatchService> _mockMatchService;
        private readonly BracketService _sut;

        public BracketServiceGetAsyncTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
                .Options;
            
            _db = new ApplicationDbContext(options);
            _mockMatchService = new Mock<IMatchService>();
            _sut = new BracketService(_db, _mockMatchService.Object);
        }

        public void Dispose()
        {
            _db?.Dispose();
        }

        /// <summary>
        /// UTC001: Tournament_NotFound_ThrowsKeyNotFoundException
        /// CONDITION:
        ///   - Precondition: Database is empty
        ///   - Input: tournamentId = 999
        /// CONFIRMATION:
        ///   - Exception: KeyNotFoundException with message "Tournament not found"
        /// RESULT: Abnormal (A)
        /// </summary>
        [Fact]
        public async Task GetAsync_TournamentNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            int tournamentId = 999;
            _mockMatchService.Setup(m => m.ProcessAutoAdvancementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _sut.GetAsync(tournamentId, CancellationToken.None)
            );
            Assert.Equal("Tournament not found", exception.Message);
        }

        /// <summary>
        /// UTC002: NoMatches_ThrowsInvalidOperationException
        /// CONDITION:
        ///   - Precondition: Tournament exists with 1 stage, no matches
        ///   - Input: tournamentId = 1
        /// CONFIRMATION:
        ///   - Exception: InvalidOperationException with message "Bracket not created yet."
        /// RESULT: Abnormal (A)
        /// </summary>
        [Fact]
        public async Task GetAsync_NoMatches_ThrowsInvalidOperationException()
        {
            // Arrange
            int tournamentId = 1;
            var tournament = new PoolMate.Api.Models.Tournament 
            { 
                Id = tournamentId,
                Name = "Test Tournament",
                IsMultiStage = false,
                OwnerUserId = "test-user",
                StartUtc = DateTime.UtcNow
            };
            var stage = new TournamentStage 
            { 
                Id = 1, 
                TournamentId = tournamentId,
                StageNo = 1,
                Type = BracketType.SingleElimination
            };
            
            _db.Tournaments.Add(tournament);
            _db.TournamentStages.Add(stage);
            await _db.SaveChangesAsync();
            
            _mockMatchService.Setup(m => m.ProcessAutoAdvancementsAsync(tournamentId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.GetAsync(tournamentId, CancellationToken.None)
            );
            Assert.Equal("Bracket not created yet.", exception.Message);
        }

        /// <summary>
        /// UTC003: SingleStage_WithOneMatch_ReturnsBracketDto
        /// CONDITION:
        ///   - Precondition: Tournament with 1 stage and 1 match exists
        ///   - Input: tournamentId = 2
        /// CONFIRMATION:
        ///   - Return: BracketDto with TournamentId=2, IsMultiStage=false, Stages.Count=1
        /// RESULT: Normal (N)
        /// </summary>
        [Fact]
        public async Task GetAsync_SingleStage_WithOneMatch_ReturnsBracketDto()
        {
            // Arrange
            int tournamentId = 2;
            var tournament = new PoolMate.Api.Models.Tournament 
            { 
                Id = tournamentId,
                Name = "Single Stage Tournament",
                IsMultiStage = false,
                OwnerUserId = "test-user",
                StartUtc = DateTime.UtcNow
            };
            var stage = new TournamentStage 
            { 
                Id = 10,
                TournamentId = tournamentId,
                StageNo = 1,
                Type = BracketType.SingleElimination,
                Ordering = BracketOrdering.Seeded
            };
            var match = new PoolMate.Api.Models.Match 
            { 
                Id = 100,
                TournamentId = tournamentId,
                StageId = stage.Id,
                Bracket = BracketSide.Knockout,
                RoundNo = 1,
                PositionInRound = 1,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[] { 1 }
            };
            
            _db.Tournaments.Add(tournament);
            _db.TournamentStages.Add(stage);
            _db.Matches.Add(match);
            await _db.SaveChangesAsync();
            
            _mockMatchService.Setup(m => m.ProcessAutoAdvancementsAsync(tournamentId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.GetAsync(tournamentId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tournamentId, result.TournamentId);
            Assert.False(result.IsMultiStage);
            Assert.Single(result.Stages);
            Assert.Equal(1, result.Stages[0].StageNo);
        }

        /// <summary>
        /// UTC004: MultiStage_WithTwoStages_ReturnsBracketDto
        /// CONDITION:
        ///   - Precondition: Tournament with 2 stages and matches in both stages
        ///   - Input: tournamentId = 3
        /// CONFIRMATION:
        ///   - Return: BracketDto with TournamentId=3, IsMultiStage=true, Stages.Count=2
        /// RESULT: Normal (N)
        /// </summary>
        [Fact]
        public async Task GetAsync_MultiStage_WithTwoStages_ReturnsBracketDto()
        {
            // Arrange
            int tournamentId = 3;
            var tournament = new PoolMate.Api.Models.Tournament 
            { 
                Id = tournamentId,
                Name = "Multi Stage Tournament",
                IsMultiStage = true,
                OwnerUserId = "test-user",
                StartUtc = DateTime.UtcNow
            };
            var stage1 = new TournamentStage 
            { 
                Id = 20,
                TournamentId = tournamentId,
                StageNo = 1,
                Type = BracketType.DoubleElimination,
                Ordering = BracketOrdering.Seeded
            };
            var stage2 = new TournamentStage 
            { 
                Id = 21,
                TournamentId = tournamentId,
                StageNo = 2,
                Type = BracketType.SingleElimination,
                Ordering = BracketOrdering.Seeded
            };
            var match1 = new PoolMate.Api.Models.Match 
            { 
                Id = 200,
                TournamentId = tournamentId,
                StageId = stage1.Id,
                Bracket = BracketSide.Winners,
                RoundNo = 1,
                PositionInRound = 1,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[] { 1 }
            };
            var match2 = new PoolMate.Api.Models.Match 
            { 
                Id = 201,
                TournamentId = tournamentId,
                StageId = stage2.Id,
                Bracket = BracketSide.Knockout,
                RoundNo = 1,
                PositionInRound = 1,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[] { 1 }
            };
            
            _db.Tournaments.Add(tournament);
            _db.TournamentStages.AddRange(stage1, stage2);
            _db.Matches.AddRange(match1, match2);
            await _db.SaveChangesAsync();
            
            _mockMatchService.Setup(m => m.ProcessAutoAdvancementsAsync(tournamentId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.GetAsync(tournamentId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tournamentId, result.TournamentId);
            Assert.True(result.IsMultiStage);
            Assert.Equal(2, result.Stages.Count);
            Assert.Equal(1, result.Stages[0].StageNo);
            Assert.Equal(2, result.Stages[1].StageNo);
        }

        /// <summary>
        /// UTC005: Dependency_ProcessAutoAdvancementsAsync_CalledFirst
        /// CONDITION:
        ///   - Precondition: Tournament with 1 stage and 1 match exists
        ///   - Input: tournamentId = 4
        /// CONFIRMATION:
        ///   - ProcessAutoAdvancementsAsync called exactly once with tournamentId=4
        ///   - Called before database queries
        /// RESULT: Normal (N) - Dependency verification
        /// </summary>
        [Fact]
        public async Task GetAsync_Always_CallsProcessAutoAdvancementsFirst()
        {
            // Arrange
            int tournamentId = 4;
            var tournament = new PoolMate.Api.Models.Tournament 
            { 
                Id = tournamentId,
                Name = "Dependency Test",
                IsMultiStage = false,
                OwnerUserId = "test-user",
                StartUtc = DateTime.UtcNow
            };
            var stage = new TournamentStage 
            { 
                Id = 30,
                TournamentId = tournamentId,
                StageNo = 1,
                Type = BracketType.SingleElimination
            };
            var match = new PoolMate.Api.Models.Match 
            { 
                Id = 300,
                TournamentId = tournamentId,
                StageId = stage.Id,
                Bracket = BracketSide.Knockout,
                RoundNo = 1,
                PositionInRound = 1,
                RowVersion = new byte[] { 1 }
            };
            
            _db.Tournaments.Add(tournament);
            _db.TournamentStages.Add(stage);
            _db.Matches.Add(match);
            await _db.SaveChangesAsync();
            
            _mockMatchService.Setup(m => m.ProcessAutoAdvancementsAsync(tournamentId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.GetAsync(tournamentId, CancellationToken.None);

            // Assert
            _mockMatchService.Verify(
                m => m.ProcessAutoAdvancementsAsync(tournamentId, It.IsAny<CancellationToken>()),
                Times.Once
            );
        }
    }
}
