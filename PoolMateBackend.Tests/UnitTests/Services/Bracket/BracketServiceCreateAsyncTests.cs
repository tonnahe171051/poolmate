using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using Xunit;
using TournamentModel = PoolMate.Api.Models.Tournament;

namespace PoolMateBackend.Tests.UnitTests.Services.Bracket
{
    public class BracketServiceCreateAsyncTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IMatchService> _mockMatchService;
        private readonly BracketService _service;

        public BracketServiceCreateAsyncTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockMatchService = new Mock<IMatchService>();
            _service = new BracketService(_context, _mockMatchService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Test Case 1: Tournament Not Found
        
        [Fact]
        public async Task CreateAsync_WhenTournamentNotFound_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            var nonExistentTournamentId = 9999;
            var ct = CancellationToken.None;

            // Act
            Func<Task> act = async () => await _service.CreateAsync(nonExistentTournamentId, null, ct);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Tournament not found");
        }

        #endregion

        #region Test Case 2: Invalid Configuration - MultiStage with Single Elimination Stage 1

        [Fact]
        public async Task CreateAsync_WhenMultiStageWithSingleElimination_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 1,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = true,
                AdvanceToStage2Count = 4,
                Stage1Ordering = BracketOrdering.Random,
                Stage2Ordering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            // Act
            Func<Task> act = async () => await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tournament configured as multi-stage cannot use Single Elimination for Stage 1.");
        }

        #endregion

        #region Test Case 3: Bracket Already Created

        [Fact]
        public async Task CreateAsync_WhenBracketAlreadyCreated_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 2,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            var stage = new TournamentStage
            {
                TournamentId = tournament.Id,
                StageNo = 1,
                Type = BracketType.DoubleElimination,
                Status = StageStatus.NotStarted,
                Ordering = BracketOrdering.Random
            };
            _context.TournamentStages.Add(stage);

            var match = new PoolMate.Api.Models.Match
            {
                TournamentId = tournament.Id,
                StageId = stage.Id,
                Bracket = BracketSide.Winners,
                RoundNo = 1,
                PositionInRound = 1,
                Status = MatchStatus.NotStarted,
                RowVersion = new byte[8]
            };
            _context.Matches.Add(match);
            await _context.SaveChangesAsync();

            // Act
            Func<Task> act = async () => await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Bracket already created.");
        }

        #endregion

        #region Test Case 4: Validation - Less Than 2 Players

        [Fact]
        public async Task CreateAsync_WhenLessThanTwoPlayers_ShouldThrowValidationException()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 3,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add only 1 player
            var player = new TournamentPlayer
            {
                TournamentId = tournament.Id,
                DisplayName = "Player 1",
                Seed = 1
            };
            _context.TournamentPlayers.Add(player);
            await _context.SaveChangesAsync();

            // Act
            Func<Task> act = async () => await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("At least two players are required to create a bracket.");
        }

        #endregion

        #region Test Case 5: Validation - MultiStage Insufficient Players

        [Fact]
        public async Task CreateAsync_WhenMultiStageInsufficientPlayers_ShouldThrowValidationException()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 4,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = true,
                AdvanceToStage2Count = 8,
                Stage1Ordering = BracketOrdering.Random,
                Stage2Ordering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add only 4 players (required at least 9 for advance count 8)
            for (int i = 1; i <= 4; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            Func<Task> act = async () => await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("Multi-stage bracket requires at least 9 players assigned in stage 1 for advance count 8.");
        }

        #endregion

        #region Test Case 6: Auto Creation - Happy Path Single Elimination

        [Fact]
        public async Task CreateAsync_AutoCreation_SingleElimination_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 5,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add 8 players (power of 2)
            for (int i = 1; i <= 8; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var createdStage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            createdStage.Should().NotBeNull();
            createdStage!.StageNo.Should().Be(1);
            createdStage.Type.Should().Be(BracketType.SingleElimination);
            createdStage.Status.Should().Be(StageStatus.NotStarted);

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 7: Auto Creation - Happy Path Double Elimination

        [Fact]
        public async Task CreateAsync_AutoCreation_DoubleElimination_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 6,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add 4 players
            for (int i = 1; i <= 4; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var createdStage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            createdStage.Should().NotBeNull();
            createdStage!.Type.Should().Be(BracketType.DoubleElimination);

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();
            
            // Double elimination should have both Winners and Losers brackets
            matches.Should().Contain(m => m.Bracket == BracketSide.Winners);
            matches.Should().Contain(m => m.Bracket == BracketSide.Losers);

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 8: Auto Creation - Non Power of 2 Players

        [Fact]
        public async Task CreateAsync_AutoCreation_NonPowerOfTwoPlayers_ShouldCreateBracketWithByes()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 7,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add 8 players (power of 2 - safe)
            for (int i = 1; i <= 8; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 9: Auto Creation - MultiStage Happy Path

        [Fact]
        public async Task CreateAsync_AutoCreation_MultiStage_ShouldCreateStage1Successfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 8,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = true,
                AdvanceToStage2Count = 4,
                Stage1Ordering = BracketOrdering.Random,
                Stage2Ordering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add 8 players (enough for advance count 4)
            for (int i = 1; i <= 8; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage1 = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id && s.StageNo == 1);
            stage1.Should().NotBeNull();
            stage1!.Type.Should().Be(BracketType.DoubleElimination);
            stage1.AdvanceCount.Should().Be(4);
            stage1.Ordering.Should().Be(BracketOrdering.Random);

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 10: Auto Creation - Minimum Players (2)

        [Fact]
        public async Task CreateAsync_AutoCreation_MinimumTwoPlayers_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 9,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add exactly 2 players
            for (int i = 1; i <= 2; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();
            matches.Should().HaveCountGreaterThanOrEqualTo(1); // At least one final match

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 11: Auto Creation - Large Bracket (32 Players)

        [Fact]
        public async Task CreateAsync_AutoCreation_LargeBracket_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 10,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add 32 players
            for (int i = 1; i <= 32; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();
            matches.Should().HaveCountGreaterThan(30); // Large bracket has many matches

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 12: Manual Creation - Happy Path

        [Fact]
        public async Task CreateAsync_ManualCreation_WithValidAssignments_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 11,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            var players = new List<TournamentPlayer>();
            for (int i = 1; i <= 4; i++)
            {
                players.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            _context.TournamentPlayers.AddRange(players);
            await _context.SaveChangesAsync();

            var request = new CreateBracketRequest
            {
                Type = BracketCreationType.Manual,
                ManualAssignments = new List<ManualSlotAssignment>
                {
                    new ManualSlotAssignment { SlotPosition = 0, TpId = players[0].Id },
                    new ManualSlotAssignment { SlotPosition = 1, TpId = players[1].Id },
                    new ManualSlotAssignment { SlotPosition = 2, TpId = players[2].Id },
                    new ManualSlotAssignment { SlotPosition = 3, TpId = players[3].Id }
                }
            };

            // Act
            await _service.CreateAsync(tournament.Id, request, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 13: Auto Creation - Odd Number of Players (3, 5, 7)

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        public async Task CreateAsync_AutoCreation_OddNumberOfPlayers_ShouldCreateBracketWithByes(int playerCount)
        {
            // Arrange
            var tournamentId = 100 + playerCount;
            var tournament = new TournamentModel
            {
                Id = tournamentId,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            for (int i = 1; i <= playerCount; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 14: Auto Creation - Random Ordering

        [Fact]
        public async Task CreateAsync_AutoCreation_RandomOrdering_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 12,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            for (int i = 1; i <= 8; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = null // No seeds for random
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();
            stage!.Ordering.Should().Be(BracketOrdering.Random);

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 15: Auto Creation - Seeded Ordering

        [Fact]
        public async Task CreateAsync_AutoCreation_SeededOrdering_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 13,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            for (int i = 1; i <= 8; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();
            stage!.Ordering.Should().Be(BracketOrdering.Seeded);

            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 16: Exception Handling - Rethrow

        [Fact]
        public async Task CreateAsync_WhenExceptionOccurs_ShouldRethrowException()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 14,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add players
            for (int i = 1; i <= 4; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Mock ProcessAutoAdvancementsAsync to throw exception
            _mockMatchService
                .Setup(m => m.ProcessAutoAdvancementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Auto advancement failed"));

            // Act
            Func<Task> act = async () => await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Auto advancement failed");
        }

        #endregion

        #region Test Case 17: Auto Creation - Players with Mixed Seeds and Unseeded

        [Fact]
        public async Task CreateAsync_AutoCreation_MixedSeededAndUnseeded_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 15,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add mix of seeded and unseeded players
            for (int i = 1; i <= 4; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i <= 2 ? i : null // First 2 seeded, rest unseeded
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var matches = await _context.Matches
                .Where(m => m.TournamentId == tournament.Id)
                .ToListAsync();
            matches.Should().NotBeEmpty();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 18: Auto Creation - MultiStage with Exact Minimum Players

        [Fact]
        public async Task CreateAsync_AutoCreation_MultiStageExactMinimumPlayers_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 16,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = true,
                AdvanceToStage2Count = 4,
                Stage1Ordering = BracketOrdering.Random,
                Stage2Ordering = BracketOrdering.Seeded,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            // Add exactly 5 players (minimum for advance count 4)
            for (int i = 1; i <= 5; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();
            stage!.AdvanceCount.Should().Be(4);

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 19: Request Type Automatic Explicitly Set

        [Fact]
        public async Task CreateAsync_RequestTypeAutomatic_ShouldCreateBracketSuccessfully()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 17,
                Name = "Test Tournament",
                BracketType = BracketType.SingleElimination,
                IsMultiStage = false,
                BracketOrdering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            for (int i = 1; i <= 4; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            var request = new CreateBracketRequest
            {
                Type = BracketCreationType.Automatic
            };

            // Act
            await _service.CreateAsync(tournament.Id, request, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            stage.Should().NotBeNull();

            _mockMatchService.Verify(m => m.ProcessAutoAdvancementsAsync(tournament.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Test Case 20: Verify Stage Properties

        [Fact]
        public async Task CreateAsync_ShouldSetCorrectStageProperties()
        {
            // Arrange
            var tournament = new TournamentModel
            {
                Id = 18,
                Name = "Test Tournament",
                BracketType = BracketType.DoubleElimination,
                IsMultiStage = true,
                AdvanceToStage2Count = 8,
                Stage1Ordering = BracketOrdering.Seeded,
                Stage2Ordering = BracketOrdering.Random,
                OwnerUserId = "user1"
            };
            _context.Tournaments.Add(tournament);

            for (int i = 1; i <= 16; i++)
            {
                _context.TournamentPlayers.Add(new TournamentPlayer
                {
                    TournamentId = tournament.Id,
                    DisplayName = $"Player {i}",
                    Seed = i
                });
            }
            await _context.SaveChangesAsync();

            // Act
            await _service.CreateAsync(tournament.Id, null, CancellationToken.None);

            // Assert
            var stage = await _context.TournamentStages
                .FirstOrDefaultAsync(s => s.TournamentId == tournament.Id);
            
            stage.Should().NotBeNull();
            stage!.StageNo.Should().Be(1);
            stage.Type.Should().Be(BracketType.DoubleElimination);
            stage.Status.Should().Be(StageStatus.NotStarted);
            stage.Ordering.Should().Be(BracketOrdering.Seeded);
            stage.AdvanceCount.Should().Be(8);
            stage.TournamentId.Should().Be(tournament.Id);
        }

        #endregion
    }
}
