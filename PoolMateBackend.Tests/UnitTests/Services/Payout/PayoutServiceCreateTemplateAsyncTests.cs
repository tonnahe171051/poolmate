using System.Text.Json;
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
    /// Unit Tests for PayoutService.CreateTemplateAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 13 (based on CreateTemplateAsync_TestCases.md)
    /// </summary>
    public class PayoutServiceCreateTemplateAsyncTests : IDisposable
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
        public PayoutServiceCreateTemplateAsyncTests()
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

        #region Test Case #1: MinPlayers > MaxPlayers

        /// <summary>
        /// Test Case #1: When MinPlayers greater than MaxPlayers, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test Template",
                MinPlayers = 10,
                MaxPlayers = 5,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.CreateTemplateAsync(userId, dto));
            Assert.Equal("Min Players (10) cannot be greater than Max Players (5).", ex.Message);

            // Verify DB was NOT called - no templates should be saved
            Assert.Empty(_dbContext.PayoutTemplates);
        }

        #endregion

        #region Test Case #2: MinPlayers = MaxPlayers (Boundary - At boundary)

        /// <summary>
        /// Test Case #2: When MinPlayers equals MaxPlayers, returns template successfully
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenMinPlayersEqualsMaxPlayers_ReturnsTemplate()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Equal Players Template",
                MinPlayers = 5,
                MaxPlayers = 5, // SAME as MinPlayers - at boundary
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.MinPlayers);
            Assert.Equal(5, result.MaxPlayers);
            Assert.Equal("Equal Players Template", result.Name);
        }

        #endregion

        #region Test Case #3: MinPlayers < MaxPlayers (Boundary - Below boundary)

        /// <summary>
        /// Test Case #3: When MinPlayers less than MaxPlayers, returns template successfully
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenMinPlayersLessThanMaxPlayers_ReturnsTemplate()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Normal Template",
                MinPlayers = 4,
                MaxPlayers = 8, // Greater than MinPlayers - normal case
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60 },
                    new() { Rank = 2, Percent = 40 }
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.MinPlayers);
            Assert.Equal(8, result.MaxPlayers);
        }

        #endregion

        #region Test Case #4: TotalPercent = 99.98 (Boundary - Below valid range)

        /// <summary>
        /// Test Case #4: When total percent is 99.98 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException()
        {
            // Arrange - Sum = 99.98 (error = 0.02 > 0.01)
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.98 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 99.98
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.CreateTemplateAsync(userId, dto));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);

            // Verify DB was NOT called
            Assert.Empty(_dbContext.PayoutTemplates);
        }

        #endregion

        #region Test Case #5: TotalPercent = 99.99 (Boundary - At valid boundary)

        /// <summary>
        /// Test Case #5: When total percent is 99.99 (error = 0.01), returns template (valid)
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenTotalPercentIs99_99_ReturnsTemplate()
        {
            // Arrange - Sum = 99.99 (error = 0.01, exactly at boundary, should be valid)
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Boundary Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.99 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 99.99
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert - Should NOT throw
            Assert.NotNull(result);
            Assert.Equal("Boundary Test Template", result.Name);
        }

        #endregion

        #region Test Case #6: TotalPercent = 100.01 (Boundary - At valid boundary)

        /// <summary>
        /// Test Case #6: When total percent is 100.01 (error = 0.01), returns template (valid)
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenTotalPercentIs100_01_ReturnsTemplate()
        {
            // Arrange - Sum = 100.01 (error = 0.01, exactly at boundary, should be valid)
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Boundary Upper Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.01 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 100.01
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert - Should NOT throw
            Assert.NotNull(result);
            Assert.Equal("Boundary Upper Test Template", result.Name);
        }

        #endregion

        #region Test Case #7: TotalPercent = 100.02 (Boundary - Above valid range)

        /// <summary>
        /// Test Case #7: When total percent is 100.02 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenTotalPercentIs100_02_ThrowsInvalidOperationException()
        {
            // Arrange - Sum = 100.02 (error = 0.02 > 0.01)
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.02 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 100.02
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.CreateTemplateAsync(userId, dto));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);

            // Verify DB was NOT called
            Assert.Empty(_dbContext.PayoutTemplates);
        }

        #endregion

        #region Test Case #8: Happy path - All valid, returns correct DTO

        /// <summary>
        /// Test Case #8: When all data is valid, returns correct DTO with all properties
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenAllValid_ReturnsCorrectDto()
        {
            // Arrange
            var userId = "user-123";
            var distribution = new List<RankPercentDto>
            {
                new() { Rank = 1, Percent = 60 },
                new() { Rank = 2, Percent = 30 },
                new() { Rank = 3, Percent = 10 }
            };
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Standard Payout",
                MinPlayers = 4,
                MaxPlayers = 16,
                Distribution = distribution
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0); // Id should be generated
            Assert.Equal("Standard Payout", result.Name);
            Assert.Equal(4, result.MinPlayers);
            Assert.Equal(16, result.MaxPlayers);
            Assert.Equal(3, result.Places); // Distribution.Count
            Assert.Equal(distribution, result.Distribution);
        }

        #endregion

        #region Test Case #9: Happy path - Saves entity to database

        /// <summary>
        /// Test Case #9: When valid, saves entity to database correctly
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WhenValid_SavesEntityToDatabase()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Database Save Test",
                MinPlayers = 2,
                MaxPlayers = 10,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert - Verify entity was saved to database
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.NotNull(savedEntity);
            Assert.Equal("Database Save Test", savedEntity.Name);
            Assert.Equal("user-123", savedEntity.OwnerUserId);
            Assert.Equal(2, savedEntity.MinPlayers);
            Assert.Equal(10, savedEntity.MaxPlayers);
        }

        #endregion

        #region Test Case #10: Edge - Name is trimmed

        /// <summary>
        /// Test Case #10: Verify Name is trimmed (leading/trailing spaces removed)
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_VerifyNameIsTrimmed()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "  Template With Spaces  ", // Spaces at both ends
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert
            Assert.Equal("Template With Spaces", result.Name); // Trimmed in DTO
            
            // Verify entity in DB is also trimmed
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.Equal("Template With Spaces", savedEntity!.Name);
        }

        #endregion

        #region Test Case #11: Edge - OwnerUserId is set correctly

        /// <summary>
        /// Test Case #11: Verify OwnerUserId is set correctly from userId parameter
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_VerifyOwnerUserIdIsSet()
        {
            // Arrange
            var userId = "specific-user-456";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Owner Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 100 }
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert - Verify OwnerUserId in database
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.NotNull(savedEntity);
            Assert.Equal("specific-user-456", savedEntity.OwnerUserId);
        }

        #endregion

        #region Test Case #12: Edge - Places equals Distribution.Count

        /// <summary>
        /// Test Case #12: Verify Places equals Distribution.Count
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_VerifyPlacesEqualsDistributionCount()
        {
            // Arrange
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Places Count Test",
                MinPlayers = 4,
                MaxPlayers = 16,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 50 },
                    new() { Rank = 2, Percent = 25 },
                    new() { Rank = 3, Percent = 15 },
                    new() { Rank = 4, Percent = 7 },
                    new() { Rank = 5, Percent = 3 }
                    // 5 items
                }
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert
            Assert.Equal(5, result.Places);
            
            // Verify entity in DB
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.Equal(5, savedEntity!.Places);
        }

        #endregion

        #region Test Case #13: Edge - PercentJson is serialized correctly

        /// <summary>
        /// Test Case #13: Verify PercentJson is serialized correctly as JSON string
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_VerifyPercentJsonIsSerialized()
        {
            // Arrange
            var userId = "user-123";
            var distribution = new List<RankPercentDto>
            {
                new() { Rank = 1, Percent = 60 },
                new() { Rank = 2, Percent = 40 }
            };
            var dto = new CreatePayoutTemplateDto
            {
                Name = "JSON Serialization Test",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = distribution
            };

            // Act
            var result = await _sut.CreateTemplateAsync(userId, dto);

            // Assert - Verify PercentJson in database
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.NotNull(savedEntity);
            Assert.NotNull(savedEntity.PercentJson);
            Assert.NotEmpty(savedEntity.PercentJson);

            // Verify JSON can be deserialized back to original data
            var deserialized = JsonSerializer.Deserialize<List<RankPercentDto>>(savedEntity.PercentJson);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Count);
            Assert.Equal(1, deserialized[0].Rank);
            Assert.Equal(60, deserialized[0].Percent);
            Assert.Equal(2, deserialized[1].Rank);
            Assert.Equal(40, deserialized[1].Percent);
        }

        #endregion
    }
}

