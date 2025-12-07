using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Payout;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMateBackend.Tests.UnitTests.Services.Payout
{
    /// <summary>
    /// Unit Tests for PayoutService.SimulatePayoutAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 18 (based on SimulatePayoutAsync_TestCases.md)
    /// </summary>
    public class PayoutServiceSimulatePayoutAsyncTests : IDisposable
    {
        // ============================================
        // SECTION 1: FIELDS
        // ============================================
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<ILogger<PayoutService>> _mockLogger;

        // ============================================
        // SECTION 2: SYSTEM UNDER TEST (SUT) DECLARATION
        // ============================================
        private readonly PayoutService _sut;

        // ============================================
        // SECTION 3: CONSTRUCTOR - INITIALIZATION
        // ============================================
        public PayoutServiceSimulatePayoutAsyncTests()
        {
            // Use InMemory Database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);

            // Initialize Mock Logger
            _mockLogger = new Mock<ILogger<PayoutService>>();

            // Inject dependencies into the Service
            _sut = new PayoutService(_dbContext, _mockLogger.Object);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Template not found

        /// <summary>
        /// Test Case #1: When TemplateId provided but template not found, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenTemplateIdProvidedButNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                TemplateId = 999,
                TotalPrizePool = 1000
            };
            // No template added to DB

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SimulatePayoutAsync(request));
            Assert.Equal("Payout template with ID 999 not found.", ex.Message);
        }

        #endregion

        #region Test Case #2: Template found, uses template distribution

        /// <summary>
        /// Test Case #2: When TemplateId provided and found, uses template distribution
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenTemplateIdProvidedAndFound_UsesTemplateDistribution()
        {
            // Arrange
            var template = new PayoutTemplate
            {
                Id = 1,
                Name = "Test Template",
                PercentJson = "[{\"Rank\":1,\"Percent\":60},{\"Rank\":2,\"Percent\":40}]",
                OwnerUserId = "user-1",
                MinPlayers = 2,
                MaxPlayers = 10,
                Places = 2
            };
            _dbContext.PayoutTemplates.Add(template);
            await _dbContext.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TemplateId = 1,
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(1000, result.TotalPrize);
            Assert.Equal(2, result.Breakdown.Count);
            Assert.Equal(1, result.Breakdown[0].Rank);
            Assert.Equal(60, result.Breakdown[0].Percent);
            Assert.Equal(600, result.Breakdown[0].Amount); // 60% of 1000
            Assert.Equal(2, result.Breakdown[1].Rank);
            Assert.Equal(40, result.Breakdown[1].Percent);
            Assert.Equal(400, result.Breakdown[1].Amount); // 40% of 1000
        }

        #endregion

        #region Test Case #3: Template PercentJson deserializes to null

        /// <summary>
        /// Test Case #3: When template PercentJson deserializes to null, uses empty distribution
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenTemplatePercentJsonDeserializesToNull_UsesEmptyDistribution()
        {
            // Arrange
            var template = new PayoutTemplate
            {
                Id = 1,
                Name = "Empty Template",
                PercentJson = "null", // Deserializes to null
                OwnerUserId = "user-1",
                MinPlayers = 2,
                MaxPlayers = 10,
                Places = 0
            };
            _dbContext.PayoutTemplates.Add(template);
            await _dbContext.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TemplateId = 1,
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Returns empty breakdown due to early return (distribution is empty)
            Assert.Equal(1000, result.TotalPrize);
            Assert.Empty(result.Breakdown);
        }

        #endregion

        #region Test Case #4: CustomDistribution valid, uses custom distribution

        /// <summary>
        /// Test Case #4: When CustomDistribution is valid, uses custom distribution
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionValid_UsesCustomDistribution()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(1000, result.TotalPrize);
            Assert.Equal(2, result.Breakdown.Count);
            Assert.Equal(1, result.Breakdown[0].Rank);
            Assert.Equal(600, result.Breakdown[0].Amount);
            Assert.Equal(2, result.Breakdown[1].Rank);
            Assert.Equal(400, result.Breakdown[1].Amount);
        }

        #endregion

        #region Test Case #5: CustomDistribution total is 99.98 - BELOW boundary

        /// <summary>
        /// Test Case #5: When CustomDistribution total is 99.98 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs99_98_ThrowsInvalidOperationException()
        {
            // Arrange - Sum = 99.98 (error = 0.02 > 0.01)
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.98 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 1000
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SimulatePayoutAsync(request));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);
        }

        #endregion

        #region Test Case #6: CustomDistribution total is 99.99 - AT boundary (valid)

        /// <summary>
        /// Test Case #6: When CustomDistribution total is 99.99 (error = 0.01), returns result (valid)
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs99_99_ReturnsResult()
        {
            // Arrange - Sum = 99.99 (error = 0.01, exactly at boundary, should be valid)
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.99 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Should NOT throw, returns valid result
            Assert.NotNull(result);
            Assert.Equal(1000, result.TotalPrize);
            Assert.Equal(2, result.Breakdown.Count);
        }

        #endregion

        #region Test Case #7: CustomDistribution total is 100.01 - AT boundary (valid)

        /// <summary>
        /// Test Case #7: When CustomDistribution total is 100.01 (error = 0.01), returns result (valid)
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs100_01_ReturnsResult()
        {
            // Arrange - Sum = 100.01 (error = 0.01, exactly at boundary, should be valid)
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.01 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Should NOT throw, returns valid result
            Assert.NotNull(result);
            Assert.Equal(1000, result.TotalPrize);
            Assert.Equal(2, result.Breakdown.Count);
        }

        #endregion

        #region Test Case #8: CustomDistribution total is 100.02 - ABOVE boundary

        /// <summary>
        /// Test Case #8: When CustomDistribution total is 100.02 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionTotalIs100_02_ThrowsInvalidOperationException()
        {
            // Arrange - Sum = 100.02 (error = 0.02 > 0.01)
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.02 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 1000
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SimulatePayoutAsync(request));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);
        }

        #endregion

        #region Test Case #9: No TemplateId and no CustomDistribution

        /// <summary>
        /// Test Case #9: When no TemplateId and no CustomDistribution provided, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenNoTemplateIdAndNoCustomDistribution_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                TemplateId = null,
                CustomDistribution = null,
                TotalPrizePool = 1000
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SimulatePayoutAsync(request));
            Assert.Equal("Please provide either a TemplateId or a CustomDistribution.", ex.Message);
        }

        #endregion

        #region Test Case #10: CustomDistribution is empty list

        /// <summary>
        /// Test Case #10: When CustomDistribution is empty list, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenCustomDistributionIsEmpty_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                TemplateId = null,
                CustomDistribution = new List<RankPercentDto>(), // Empty list
                TotalPrizePool = 1000
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SimulatePayoutAsync(request));
            Assert.Equal("Please provide either a TemplateId or a CustomDistribution.", ex.Message);
        }

        #endregion

        #region Test Case #11: TotalPrizePool is negative - BELOW boundary

        /// <summary>
        /// Test Case #11: When TotalPrizePool is negative, returns empty breakdown
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(-1000)]
        [InlineData(-0.01)]
        public async Task SimulatePayoutAsync_WhenTotalPrizePoolIsNegative_ReturnsEmptyBreakdown(decimal prizePool)
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                },
                TotalPrizePool = prizePool
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(prizePool, result.TotalPrize);
            Assert.Empty(result.Breakdown);
        }

        #endregion

        #region Test Case #12: TotalPrizePool is zero - AT boundary

        /// <summary>
        /// Test Case #12: When TotalPrizePool is zero, returns empty breakdown
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenTotalPrizePoolIsZero_ReturnsEmptyBreakdown()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                },
                TotalPrizePool = 0
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(0, result.TotalPrize);
            Assert.Empty(result.Breakdown);
        }

        #endregion

        #region Test Case #13: TotalPrizePool is small positive - ABOVE boundary

        /// <summary>
        /// Test Case #13: When TotalPrizePool is small positive (0.01), calculates breakdown
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenTotalPrizePoolIsSmallPositive_CalculatesBreakdown()
        {
            // Arrange
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60 },
                    new() { Rank = 2, Percent = 40 }
                },
                TotalPrizePool = 0.01m
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Should calculate (even if very small amounts)
            Assert.Equal(0.01m, result.TotalPrize);
            Assert.Equal(2, result.Breakdown.Count);
            Assert.NotEmpty(result.Breakdown);
        }

        #endregion

        #region Test Case #14: Distribution is empty after deserialize

        /// <summary>
        /// Test Case #14: When template PercentJson is empty array, returns empty breakdown
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenDistributionIsEmptyAfterDeserialize_ReturnsEmptyBreakdown()
        {
            // Arrange
            var template = new PayoutTemplate
            {
                Id = 1,
                Name = "Empty Array Template",
                PercentJson = "[]", // Empty array
                OwnerUserId = "user-1",
                MinPlayers = 2,
                MaxPlayers = 10,
                Places = 0
            };
            _dbContext.PayoutTemplates.Add(template);
            await _dbContext.SaveChangesAsync();

            var request = new PayoutSimulationRequestDto
            {
                TemplateId = 1,
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Returns empty breakdown due to !distribution.Any()
            Assert.Equal(1000, result.TotalPrize);
            Assert.Empty(result.Breakdown);
        }

        #endregion

        #region Test Case #15: Rounding difference exists - adjusts first rank

        /// <summary>
        /// Test Case #15: When rounding difference exists, adjusts first rank amount
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenRoundingDifferenceExists_AdjustsFirstRank()
        {
            // Arrange - 33.33% + 33.33% + 33.34% = 100%, but rounding may cause issues
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 33.33 },
                    new() { Rank = 2, Percent = 33.33 },
                    new() { Rank = 3, Percent = 33.34 }
                },
                TotalPrizePool = 100
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Total must equal TotalPrizePool after adjustment
            var totalAmount = result.Breakdown.Sum(x => x.Amount);
            Assert.Equal(100, totalAmount);
            Assert.Equal(3, result.Breakdown.Count);
        }

        #endregion

        #region Test Case #16: No rounding difference - no adjustment

        /// <summary>
        /// Test Case #16: When no rounding difference, no adjustment needed
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenNoRoundingDifference_NoAdjustment()
        {
            // Arrange - 50% + 50% = 100% exactly, no rounding issues
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 50 },
                    new() { Rank = 2, Percent = 50 }
                },
                TotalPrizePool = 100
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(2, result.Breakdown.Count);
            Assert.Equal(50, result.Breakdown[0].Amount); // Exactly 50
            Assert.Equal(50, result.Breakdown[1].Amount); // Exactly 50
            Assert.Equal(100, result.Breakdown.Sum(x => x.Amount));
        }

        #endregion

        #region Test Case #17: Verify breakdown ordered by rank

        /// <summary>
        /// Test Case #17: Verify breakdown is ordered by rank ascending
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_VerifyBreakdownOrderedByRank()
        {
            // Arrange - Distribution NOT in order
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 3, Percent = 20 },
                    new() { Rank = 1, Percent = 50 },
                    new() { Rank = 2, Percent = 30 }
                },
                TotalPrizePool = 1000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert - Verify ordered by Rank ascending
            Assert.Equal(3, result.Breakdown.Count);
            Assert.Equal(1, result.Breakdown[0].Rank);
            Assert.Equal(2, result.Breakdown[1].Rank);
            Assert.Equal(3, result.Breakdown[2].Rank);
            Assert.Equal(500, result.Breakdown[0].Amount); // 50% of 1000
            Assert.Equal(300, result.Breakdown[1].Amount); // 30% of 1000
            Assert.Equal(200, result.Breakdown[2].Amount); // 20% of 1000
        }

        #endregion

        #region Test Case #18: Multiple ranks calculate correct amounts

        /// <summary>
        /// Test Case #18: When multiple ranks, calculates correct amounts with proper rounding
        /// </summary>
        [Fact]
        public async Task SimulatePayoutAsync_WhenMultipleRanks_CalculatesCorrectAmounts()
        {
            // Arrange - 5 ranks with various percentages
            var request = new PayoutSimulationRequestDto
            {
                CustomDistribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 40 },
                    new() { Rank = 2, Percent = 25 },
                    new() { Rank = 3, Percent = 15 },
                    new() { Rank = 4, Percent = 12 },
                    new() { Rank = 5, Percent = 8 }
                },
                TotalPrizePool = 10000
            };

            // Act
            var result = await _sut.SimulatePayoutAsync(request);

            // Assert
            Assert.Equal(10000, result.TotalPrize);
            Assert.Equal(5, result.Breakdown.Count);

            // Verify each amount: Amount = TotalPrizePool * (Percent/100)
            Assert.Equal(4000, result.Breakdown[0].Amount);  // 40% of 10000
            Assert.Equal(2500, result.Breakdown[1].Amount);  // 25% of 10000
            Assert.Equal(1500, result.Breakdown[2].Amount);  // 15% of 10000
            Assert.Equal(1200, result.Breakdown[3].Amount);  // 12% of 10000
            Assert.Equal(800, result.Breakdown[4].Amount);   // 8% of 10000

            // Verify total equals TotalPrizePool
            Assert.Equal(10000, result.Breakdown.Sum(x => x.Amount));
        }

        #endregion
    }
}
