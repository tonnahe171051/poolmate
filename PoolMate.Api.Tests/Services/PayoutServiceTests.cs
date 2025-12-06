using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Data;
using PoolMate.Api.Services;
using PoolMate.Api.Models;
using PoolMate.Api.Tests.Fixtures;
using PoolMate.Api.Dtos.Payout;
using Microsoft.EntityFrameworkCore;

namespace PoolMate.Api.Tests.Services
{
    /// <summary>
    /// Unit tests cho PayoutService - logic tính toán payout ph?c t?p
    /// Focus: Calculate payouts, Distribute prizes, Handle edge cases
    /// </summary>
    public class PayoutServiceTests : IDisposable
    {
        private readonly TestDatabaseFixture _fixture;
        private readonly Mock<ILogger<PayoutService>> _mockLogger;
        private readonly PayoutService _payoutService;

        public PayoutServiceTests()
        {
            _fixture = new TestDatabaseFixture();
            _mockLogger = new Mock<ILogger<PayoutService>>();

            _payoutService = new PayoutService(
                _fixture.Context,
                _mockLogger.Object
            );
        }

        #region Payout Calculation Tests

        [Fact]
        public async Task CalculatePayouts_WithValidTemplate_ShouldDistributeCorrectly()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var template = MockDataFactory.CreateMockPayoutTemplate("Standard", 8);
            
            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 1000,
                TemplateId = template.Id
            };

            // Act
            var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Rankings.Count > 0);
            Assert.True(result.TotalDistributed <= request.TotalPayout);
        }

        [Fact]
        public async Task CalculatePayouts_With5Percent_ShouldCalculateCorrectly()
        {
            // Arrange
            var template = MockDataFactory.CreateMockPayoutTemplate("Test", 4);
            template.RankPercentages = new Dictionary<int, decimal>
            {
                { 1, 50 },
                { 2, 30 },
                { 3, 15 },
                { 4, 5 }
            };

            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var tournament = MockDataFactory.CreateMockTournament();
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 1000, // $1000 total
                TemplateId = template.Id
            };

            // Act
            var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var rankings = result.Rankings.OrderBy(r => r.Rank).ToList();
            
            Assert.Equal(500, rankings[0].Amount); // 1st: 50%
            Assert.Equal(300, rankings[1].Amount); // 2nd: 30%
            Assert.Equal(150, rankings[2].Amount); // 3rd: 15%
            Assert.Equal(50, rankings[3].Amount);  // 4th: 5%
        }

        #endregion

        #region Error Cases

        [Fact]
        public async Task CalculatePayouts_WithInvalidTemplate_ShouldThrowException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            _fixture.Context.Tournaments.Add(tournament);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 1000,
                TemplateId = 999 // Non-existent template
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _payoutService.SimulatePayoutAsync(request, CancellationToken.None)
            );
        }

        [Fact]
        public async Task CalculatePayouts_WithNegativePayout_ShouldThrowException()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var template = MockDataFactory.CreateMockPayoutTemplate();

            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = -1000, // Invalid negative payout
                TemplateId = template.Id
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _payoutService.SimulatePayoutAsync(request, CancellationToken.None)
            );
        }

        [Fact]
        public async Task CalculatePayouts_WithZeroPayout_ShouldReturnZeroDistribution()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var template = MockDataFactory.CreateMockPayoutTemplate();

            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 0,
                TemplateId = template.Id
            };

            // Act
            var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.All(result.Rankings, r => Assert.Equal(0, r.Amount));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task CalculatePayouts_WithLargeAmount_ShouldMaintainPrecision()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var template = MockDataFactory.CreateMockPayoutTemplate("Large", 4);

            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 1000000, // $1 million
                TemplateId = template.Id
            };

            // Act
            var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalDistributed > 0);
            Assert.True(Math.Abs(result.TotalDistributed - 1000000) < 1); // Allow $1 rounding
        }

        [Fact]
        public async Task CalculatePayouts_WithFractionalAmounts_ShouldRoundCorrectly()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            var template = MockDataFactory.CreateMockPayoutTemplate("Fractional", 3);
            template.RankPercentages = new Dictionary<int, decimal>
            {
                { 1, 33.33m },
                { 2, 33.33m },
                { 3, 33.34m }
            };

            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.Add(template);
            await _fixture.Context.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TournamentId = tournament.Id,
                TotalPayout = 1000,
                TemplateId = template.Id
            };

            // Act
            var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var total = result.Rankings.Sum(r => r.Amount);
            Assert.Equal(1000, total); // Should sum exactly
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public async Task CalculatePayouts_WithMultipleDistributionScenarios_ShouldHandleCorrectly()
        {
            // Arrange
            var tournament = MockDataFactory.CreateMockTournament();
            
            // Create multiple templates
            var templates = new List<PayoutTemplate>
            {
                MockDataFactory.CreateMockPayoutTemplate("Winner Takes All", 2),
                MockDataFactory.CreateMockPayoutTemplate("Balanced", 4),
                MockDataFactory.CreateMockPayoutTemplate("Top Heavy", 8)
            };

            _fixture.Context.Tournaments.Add(tournament);
            _fixture.Context.PayoutTemplates.AddRange(templates);
            await _fixture.Context.SaveChangesAsync();

            // Act - Test each template
            foreach (var template in templates)
            {
                var request = new PayoutSimulationRequestDto
                {
                    TournamentId = tournament.Id,
                    TotalPayout = 1000,
                    TemplateId = template.Id
                };

                var result = await _payoutService.SimulatePayoutAsync(request, CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Rankings.Count > 0);
            }
        }

        #endregion

        public void Dispose()
        {
            _fixture?.Dispose();
        }
    }
}
