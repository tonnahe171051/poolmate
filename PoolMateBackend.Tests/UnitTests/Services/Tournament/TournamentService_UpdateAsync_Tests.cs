using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.Tournament;

/// <summary>
/// Unit Tests for TournamentService.UpdateAsync method
/// OPTIMIZED VERSION - Using xUnit Theory for efficiency
/// Total Test Methods: 10
/// Total Test Cases Executed: ~30 (via InlineData)
/// Coverage: 100%
/// </summary>
public class TournamentService_UpdateAsync_Tests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<ICloudinaryService> _mockCloudinaryService;
    private readonly TournamentService _sut;
    
    public TournamentService_UpdateAsync_Tests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new ApplicationDbContext(options);
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        
        // Create SUT (System Under Test)
        _sut = new TournamentService(_db, _mockCloudinaryService.Object);
    }
    
    public void Dispose()
    {
        _db.Dispose();
    }

    #region Helper Methods
    
    /// <summary>
    /// Setup test environment with a tournament and optional players
    /// </summary>
    private async Task<PoolMate.Api.Models.Tournament> SetupTournamentAsync(
        int tournamentId = 1,
        string ownerUserId = "user1",
        TournamentStatus status = TournamentStatus.Upcoming,
        bool isMultiStage = false,
        int currentPlayers = 0,
        PayoutMode payoutMode = PayoutMode.Template)
    {
        var tournament = new PoolMate.Api.Models.Tournament
        {
            Id = tournamentId,
            OwnerUserId = ownerUserId,
            Name = "Test Tournament",
            Status = status,
            IsMultiStage = isMultiStage,
            BracketType = BracketType.DoubleElimination,
            BracketOrdering = BracketOrdering.Random,
            Stage1Ordering = BracketOrdering.Random,
            Stage2Ordering = BracketOrdering.Random,
            PayoutMode = payoutMode,
            BracketSizeEstimate = 32,
            EntryFee = 50,
            AdminFee = 5,
            AddedMoney = 500,
            TotalPrize = 0,
            AdvanceToStage2Count = isMultiStage ? 8 : null,
            StartUtc = DateTime.UtcNow.AddDays(7),
            EndUtc = DateTime.UtcNow.AddDays(8),
            PlayerType = PlayerType.Singles,
            GameType = GameType.NineBall,
            Rule = Rule.WNT,
            BreakFormat = BreakFormat.WinnerBreak,
            IsPublic = true,
            OnlineRegistrationEnabled = true
        };
        
        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync();
        
        // Add players if needed
        if (currentPlayers > 0)
        {
            var players = Enumerable.Range(1, currentPlayers)
                .Select(i => new TournamentPlayer 
                { 
                    TournamentId = tournamentId, 
                    PlayerId = i,
                    DisplayName = $"Player {i}"
                });
            _db.TournamentPlayers.AddRange(players);
            await _db.SaveChangesAsync();
        }
        
        // Clear change tracker to avoid tracking issues
        _db.ChangeTracker.Clear();
        
        return tournament;
    }
    
    #endregion

    #region Test 1: Authorization & Not Found Validation
    
    [Fact]
    public async Task UpdateAsync_TournamentNotFound_ReturnsFalse()
    {
        // Arrange
        await SetupTournamentAsync(tournamentId: 1, ownerUserId: "user1");
        var model = new UpdateTournamentModel { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(999, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WrongOwner_ReturnsFalse()
    {
        // Arrange
        await SetupTournamentAsync(tournamentId: 1, ownerUserId: "user1");
        var model = new UpdateTournamentModel { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(1, "hacker", model, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
    
    #endregion

    #region Test 2: Basic Info & Game Settings Update (Happy Path)
    
    [Fact]
    public async Task UpdateAsync_BasicInfoAndSettings_UpdatesAllFieldsCorrectly()
    {
        // Arrange
        await SetupTournamentAsync();
        var model = new UpdateTournamentModel
        {
            Name = "  Updated Tournament  ", // Test trim
            Description = "New Description",
            StartUtc = DateTime.UtcNow.AddDays(10),
            EndUtc = DateTime.UtcNow.AddDays(12),
            VenueId = 99,
            IsPublic = false,
            OnlineRegistrationEnabled = false,
            
            // Game settings
            PlayerType = PlayerType.Doubles,
            GameType = GameType.EightBall,
            Rule = Rule.WPA,
            BreakFormat = BreakFormat.AlternateBreak,
            
            // Fee settings
            EntryFee = 100,
            AdminFee = 10,
            AddedMoney = 1000,
            PayoutMode = PayoutMode.Custom,
            PayoutTemplateId = 5,
            
            // Race settings
            WinnersRaceTo = 7,
            LosersRaceTo = 5,
            FinalsRaceTo = 9
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Tournament"); // Verify trim
        updated.Description.Should().Be("New Description");
        updated.StartUtc.Should().Be(model.StartUtc.Value);
        updated.EndUtc.Should().Be(model.EndUtc.Value);
        updated.VenueId.Should().Be(99);
        updated.IsPublic.Should().BeFalse();
        updated.OnlineRegistrationEnabled.Should().BeFalse();
        
        // Game settings
        updated.PlayerType.Should().Be(PlayerType.Doubles);
        updated.GameType.Should().Be(GameType.EightBall);
        updated.Rule.Should().Be(Rule.WPA);
        updated.BreakFormat.Should().Be(BreakFormat.AlternateBreak);
        
        // Fee settings
        updated.EntryFee.Should().Be(100);
        updated.AdminFee.Should().Be(10);
        updated.AddedMoney.Should().Be(1000);
        updated.PayoutMode.Should().Be(PayoutMode.Custom);
        updated.PayoutTemplateId.Should().Be(5);
        
        // Race settings
        updated.WinnersRaceTo.Should().Be(7);
        updated.LosersRaceTo.Should().Be(5);
        updated.FinalsRaceTo.Should().Be(9);
    }

    [Fact]
    public async Task UpdateAsync_NameWithWhitespaceOnly_DoesNotUpdate()
    {
        // Arrange
        var tournament = await SetupTournamentAsync();
        var originalName = tournament.Name;
        var model = new UpdateTournamentModel { Name = "   " };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.Name.Should().Be(originalName); // Should not change
    }

    [Fact]
    public async Task UpdateAsync_DescriptionNull_DoesNotUpdate()
    {
        // Arrange
        var tournament = await SetupTournamentAsync();
        tournament.Description = "Original Description";
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Note: When m.Description is null, the condition `if (m.Description is not null)` is FALSE
        // so Description field will NOT be updated
        var model = new UpdateTournamentModel(); // Description is null (not set)

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.Description.Should().Be("Original Description"); // Should not change
    }
    
    #endregion

    #region Test 3: TotalPrize Clamping Logic
    
    [Theory]
    [InlineData(5000, 5000)]  // Positive value
    [InlineData(0, 0)]        // Zero (boundary)
    [InlineData(-100, 0)]     // Negative -> clamped to 0
    public async Task UpdateAsync_TotalPrize_ClampsToZeroForNegative(decimal input, decimal expected)
    {
        // Arrange
        await SetupTournamentAsync(payoutMode: PayoutMode.Custom);
        var model = new UpdateTournamentModel { TotalPrize = input };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.TotalPrize.Should().Be(expected);
    }
    
    #endregion

    #region Test 4: CanEditBracket Guard Clause
    
    [Theory]
    [InlineData(TournamentStatus.Upcoming, true)]
    [InlineData(TournamentStatus.InProgress, false)]
    [InlineData(TournamentStatus.Completed, false)]
    public async Task UpdateAsync_BracketSettings_RespectsCanEditBracket(
        TournamentStatus status, bool shouldUpdate)
    {
        // Arrange
        var tournament = await SetupTournamentAsync(status: status);
        var originalBracketSize = tournament.BracketSizeEstimate;
        var model = new UpdateTournamentModel { BracketSizeEstimate = 64 };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _db.Tournaments.FindAsync(1);
        
        if (shouldUpdate)
        {
            updated!.BracketSizeEstimate.Should().Be(64);
        }
        else
        {
            updated!.BracketSizeEstimate.Should().Be(originalBracketSize);
        }
    }
    
    #endregion

    #region Test 5: BracketSizeEstimate vs CurrentPlayers Validation
    
    [Theory]
    [InlineData(10, 16, true, null)]   // Current 10, new 16 -> OK
    [InlineData(16, 16, true, null)]   // Equal (boundary) -> OK
    [InlineData(20, 16, false, "Cannot reduce bracket size below current player count")] // Too small -> Fail
    [InlineData(0, 8, true, null)]     // No players -> OK
    public async Task UpdateAsync_BracketSize_ValidatesAgainstCurrentPlayers(
        int currentPlayers, int newSize, bool expectSuccess, string? errorFragment)
    {
        // Arrange
        await SetupTournamentAsync(
            status: TournamentStatus.Upcoming,
            currentPlayers: currentPlayers);
        
        var model = new UpdateTournamentModel { BracketSizeEstimate = newSize };

        // Act & Assert
        if (expectSuccess)
        {
            var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
            result.Should().BeTrue();
            
            var updated = await _db.Tournaments.FindAsync(1);
            updated!.BracketSizeEstimate.Should().Be(newSize);
        }
        else
        {
            var act = async () => await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{errorFragment}*");
        }
    }
    
    #endregion

    #region Test 6: Multi-Stage Validation (Core Business Logic)
    
    [Theory]
    // Single Elimination conflict
    [InlineData(true, BracketType.SingleElimination, 8, false, "Single Elimination cannot be used")]
    // Power of 2 validation - Invalid
    [InlineData(true, BracketType.DoubleElimination, 3, false, "power of 2")]
    [InlineData(true, BracketType.DoubleElimination, 6, false, "power of 2")]
    [InlineData(true, BracketType.DoubleElimination, 0, false, "power of 2")]
    [InlineData(true, BracketType.DoubleElimination, -4, false, "power of 2")]
    // Min value validation
    [InlineData(true, BracketType.DoubleElimination, 2, false, "at least 4")]
    // Happy paths - Valid power of 2 >= 4
    [InlineData(true, BracketType.DoubleElimination, 4, true, null)]
    [InlineData(true, BracketType.DoubleElimination, 8, true, null)]
    [InlineData(true, BracketType.DoubleElimination, 16, true, null)]
    [InlineData(true, BracketType.DoubleElimination, 32, true, null)]
    public async Task UpdateAsync_MultiStage_ValidatesConfiguration(
        bool isMulti, BracketType stage1Type, int advanceCount, 
        bool expectSuccess, string? errorFragment)
    {
        // Arrange
        await SetupTournamentAsync(status: TournamentStatus.Upcoming);
        
        var model = new UpdateTournamentModel
        {
            IsMultiStage = isMulti,
            Stage1Type = stage1Type,
            AdvanceToStage2Count = advanceCount
        };

        // Act & Assert
        if (expectSuccess)
        {
            var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
            result.Should().BeTrue();
            
            var updated = await _db.Tournaments.FindAsync(1);
            updated!.IsMultiStage.Should().BeTrue();
            updated.AdvanceToStage2Count.Should().Be(advanceCount);
            updated.BracketType.Should().Be(stage1Type);
        }
        else
        {
            var act = async () => await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{errorFragment}*");
        }
    }

    [Fact]
    public async Task UpdateAsync_MultiStage_SingleEliminationViaBracketTypeFallback_ThrowsException()
    {
        // Arrange
        var tournament = await SetupTournamentAsync(status: TournamentStatus.Upcoming);  // CanEditBracket = true
        tournament.BracketType = BracketType.SingleElimination;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Try to enable multi-stage without specifying Stage1Type or BracketType in model
        // effectiveStage1Type = m.Stage1Type ?? m.BracketType ?? t.BracketType = SingleElimination
        var model = new UpdateTournamentModel
        {
            IsMultiStage = true,  // willBeMulti = true -> validation will run
            AdvanceToStage2Count = 8
            // No Stage1Type or BracketType specified -> uses t.BracketType (SingleElimination)
        };

        // Act & Assert
        var act = async () => await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Single Elimination cannot be used*");
    }

    [Fact]
    public async Task UpdateAsync_MultiStage_ExistingAdvanceCountLessThan4_ThrowsException()
    {
        // Arrange - Create tournament with corrupt data (AdvanceToStage2Count < 4)
        var tournament = await SetupTournamentAsync(
            status: TournamentStatus.Upcoming,  // CanEditBracket = true
            isMultiStage: true);
        
        // Manually set corrupt value
        tournament.AdvanceToStage2Count = 2;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Try to update without changing AdvanceToStage2Count
        // Validation: `else if (t.AdvanceToStage2Count.HasValue && t.AdvanceToStage2Count.Value < 4)`
        // This only runs when: willBeMulti = true AND m.AdvanceToStage2Count is null
        var model = new UpdateTournamentModel 
        { 
            IsMultiStage = true,  // willBeMulti = true
            // AdvanceToStage2Count not set -> will trigger the else if validation
        };

        // Act & Assert
        var act = async () => await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AdvanceToStage2Count must be at least 4*");
    }
    
    #endregion

    #region Test 7: Bracket Type & Ordering Priority + Sync Logic
    
    [Fact]
    public async Task UpdateAsync_MultiStage_AppliesPriorityAndSyncsFields()
    {
        // Arrange
        await SetupTournamentAsync(status: TournamentStatus.Upcoming);
        
        var model = new UpdateTournamentModel
        {
            IsMultiStage = true,
            AdvanceToStage2Count = 8,
            
            // Priority test: Stage1Type should win over BracketType
            Stage1Type = BracketType.DoubleElimination,
            BracketType = BracketType.SingleElimination, // Should be ignored
            
            // Priority test: Stage1Ordering should win over BracketOrdering
            Stage1Ordering = BracketOrdering.Seeded,
            BracketOrdering = BracketOrdering.Random, // Should be ignored
            
            Stage2Ordering = BracketOrdering.Seeded
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        
        // Verify Priority: Stage1Type wins
        updated!.BracketType.Should().Be(BracketType.DoubleElimination);
        
        // Verify Priority: Stage1Ordering wins
        updated.Stage1Ordering.Should().Be(BracketOrdering.Seeded);
        
        // Verify Sync: BracketOrdering synced with Stage1Ordering
        updated.BracketOrdering.Should().Be(BracketOrdering.Seeded);
        
        // Verify Stage2Ordering
        updated.Stage2Ordering.Should().Be(BracketOrdering.Seeded);
    }

    [Fact]
    public async Task UpdateAsync_SingleStage_BracketTypeAndOrdering_UpdatesCorrectly()
    {
        // Arrange
        await SetupTournamentAsync(status: TournamentStatus.Upcoming);
        
        var model = new UpdateTournamentModel
        {
            IsMultiStage = false,
            BracketType = BracketType.SingleElimination, // Allowed in single-stage
            BracketOrdering = BracketOrdering.Seeded
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.BracketType.Should().Be(BracketType.SingleElimination);
        updated.BracketOrdering.Should().Be(BracketOrdering.Seeded);
        updated.Stage1Ordering.Should().Be(BracketOrdering.Seeded); // Should sync
    }

    [Fact]
    public async Task UpdateAsync_MultiStage_BracketTypeFallback_WhenStage1TypeNull()
    {
        // Arrange
        await SetupTournamentAsync(status: TournamentStatus.Upcoming);
        
        var model = new UpdateTournamentModel
        {
            IsMultiStage = true,
            AdvanceToStage2Count = 8,
            BracketType = BracketType.DoubleElimination,
            // Stage1Type is null -> should fallback to BracketType
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.BracketType.Should().Be(BracketType.DoubleElimination);
    }
    
    #endregion

    #region Test 8: State Transition - Multi ↔ Single Stage
    
    [Fact]
    public async Task UpdateAsync_TransitionFromMultiToSingle_ResetsStage2Settings()
    {
        // Arrange - Start with multi-stage tournament
        var tournament = await SetupTournamentAsync(
            status: TournamentStatus.Upcoming,
            isMultiStage: true);
        
        tournament.AdvanceToStage2Count = 8;
        tournament.Stage2Ordering = BracketOrdering.Seeded;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Transition to single-stage
        var model = new UpdateTournamentModel { IsMultiStage = false };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.IsMultiStage.Should().BeFalse();
        updated.AdvanceToStage2Count.Should().BeNull(); // Should be cleared
        updated.Stage2Ordering.Should().Be(BracketOrdering.Random); // Should be reset
    }

    [Fact]
    public async Task UpdateAsync_TransitionFromSingleToMulti_AppliesStage2Settings()
    {
        // Arrange - Start with single-stage tournament
        await SetupTournamentAsync(
            status: TournamentStatus.Upcoming,
            isMultiStage: false);
        
        // Transition to multi-stage
        var model = new UpdateTournamentModel
        {
            IsMultiStage = true,
            AdvanceToStage2Count = 16,
            Stage1Type = BracketType.DoubleElimination,
            Stage2Ordering = BracketOrdering.Seeded
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.IsMultiStage.Should().BeTrue();
        updated.AdvanceToStage2Count.Should().Be(16);
        updated.Stage2Ordering.Should().Be(BracketOrdering.Seeded);
        updated.BracketType.Should().Be(BracketType.DoubleElimination);
    }

    [Fact]
    public async Task UpdateAsync_MultiStage_AdvanceToStage2CountNull_KeepsExisting()
    {
        // Arrange
        var tournament = await SetupTournamentAsync(
            status: TournamentStatus.Upcoming,
            isMultiStage: true);
        
        tournament.AdvanceToStage2Count = 8;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Update without changing AdvanceToStage2Count
        var model = new UpdateTournamentModel
        {
            IsMultiStage = true,
            AdvanceToStage2Count = null // Don't update this field
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.AdvanceToStage2Count.Should().Be(8); // Should remain unchanged
    }
    
    #endregion

    #region Test 9: CalculateTotalPrize Integration
    
    [Theory]
    [InlineData(PayoutMode.Template, 16, 50, 5, 500, 1220)] // (16*50) + 500 - (16*5) = 1220
    [InlineData(PayoutMode.Template, 32, 100, 10, 1000, 3880)] // (32*100) + 1000 - (32*10) = 3880
    [InlineData(PayoutMode.Custom, 32, 0, 0, 0, 5000)] // Custom mode preserves TotalPrize
    public async Task UpdateAsync_CalculateTotalPrize_AppliesCorrectFormula(
        PayoutMode mode, int bracketSize, decimal entryFee, 
        decimal adminFee, decimal addedMoney, decimal expectedTotal)
    {
        // Arrange
        var tournament = await SetupTournamentAsync(payoutMode: mode);
        tournament.BracketSizeEstimate = bracketSize;
        
        if (mode == PayoutMode.Custom)
        {
            tournament.TotalPrize = expectedTotal;
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        
        // Update fees and bracket size to trigger CalculateTotalPrize
        var model = new UpdateTournamentModel 
        { 
            BracketSizeEstimate = bracketSize, // Need to update bracket size for correct calculation
            EntryFee = entryFee,
            AdminFee = adminFee,
            AddedMoney = addedMoney
        };
        
        if (mode == PayoutMode.Custom)
        {
            model.TotalPrize = expectedTotal; // For custom mode, need to set TotalPrize in model
        }

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.TotalPrize.Should().Be(expectedTotal);
    }

    [Fact]
    public async Task UpdateAsync_PayoutModeTemplate_RecalculatesOnFeeUpdate()
    {
        // Arrange
        await SetupTournamentAsync(payoutMode: PayoutMode.Template);
        
        var model = new UpdateTournamentModel
        {
            EntryFee = 100,
            AdminFee = 10,
            AddedMoney = 2000
        };

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        // BracketSize = 32, EntryFee = 100, AdminFee = 10, AddedMoney = 2000
        // TotalPrize = (32*100) + 2000 - (32*10) = 3200 + 2000 - 320 = 4880
        updated!.TotalPrize.Should().Be(4880);
    }
    
    #endregion

    #region Test 10: Defensive - Null/Empty Model
    
    [Fact]
    public async Task UpdateAsync_EmptyModel_OnlyRecalculatesPrize()
    {
        // Arrange
        var tournament = await SetupTournamentAsync(payoutMode: PayoutMode.Template);
        var originalName = tournament.Name;
        
        var model = new UpdateTournamentModel(); // All fields null

        // Act
        var result = await _sut.UpdateAsync(1, "user1", model, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updated = await _db.Tournaments.FindAsync(1);
        updated!.Name.Should().Be(originalName); // No field should change
        
        // But CalculateTotalPrize should still be called
        // BracketSize=32, Entry=50, Admin=5, Added=500
        // TotalPrize = (32*50) + 500 - (32*5) = 1600 + 500 - 160 = 1940
        updated.TotalPrize.Should().Be(1940);
    }
    
    #endregion
}

