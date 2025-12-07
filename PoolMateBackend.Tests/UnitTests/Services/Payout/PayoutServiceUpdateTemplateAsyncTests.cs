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
    /// Unit Tests for PayoutService.UpdateTemplateAsync
    /// Method: Solitary Unit Testing with InMemory Database
    /// Total Test Cases: 15 (based on UpdateTemplateAsync_TestCases.md)
    /// </summary>
    public class PayoutServiceUpdateTemplateAsyncTests : IDisposable
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
        public PayoutServiceUpdateTemplateAsyncTests()
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
        // HELPER METHOD: Create existing template in DB
        // ============================================
        private async Task<PayoutTemplate> CreateExistingTemplateAsync(
            int id, 
            string ownerUserId, 
            string name = "Existing Template")
        {
            var template = new PayoutTemplate
            {
                Id = id,
                OwnerUserId = ownerUserId,
                Name = name,
                MinPlayers = 2,
                MaxPlayers = 10,
                Places = 2,
                PercentJson = "[{\"Rank\":1,\"Percent\":60},{\"Rank\":2,\"Percent\":40}]"
            };
            _dbContext.PayoutTemplates.Add(template);
            await _dbContext.SaveChangesAsync();
            return template;
        }

        // ============================================
        // SECTION 4: TEST CASES
        // ============================================

        #region Test Case #1: Template not found

        /// <summary>
        /// Test Case #1: When template with given ID does not exist, returns null
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenTemplateNotFound_ReturnsNull()
        {
            // Arrange
            var id = 999; // Non-existent ID
            var userId = "user-123";
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };
            // No templates added to DB

            // Act
            var result = await _sut.UpdateTemplateAsync(id, userId, dto);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Test Case #2: User not owner

        /// <summary>
        /// Test Case #2: When user is not the owner of the template, returns null
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenUserNotOwner_ReturnsNull()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "other-user"); // Different owner
            
            var userId = "user-123"; // Current user trying to update
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Updated",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, userId, dto);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Test Case #3: MinPlayers > MaxPlayers

        /// <summary>
        /// Test Case #3: When MinPlayers greater than MaxPlayers, throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenMinPlayersGreaterThanMaxPlayers_ThrowsInvalidOperationException()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Updated",
                MinPlayers = 10, // Greater than MaxPlayers
                MaxPlayers = 5,
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.UpdateTemplateAsync(1, "user-123", dto));
            Assert.Equal("Min Players (10) cannot be greater than Max Players (5).", ex.Message);
        }

        #endregion

        #region Test Case #4: MinPlayers = MaxPlayers (Boundary - At boundary)

        /// <summary>
        /// Test Case #4: When MinPlayers equals MaxPlayers, returns updated template successfully
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenMinPlayersEqualsMaxPlayers_ReturnsUpdatedTemplate()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Equal Players Template",
                MinPlayers = 5,
                MaxPlayers = 5, // SAME as MinPlayers - at boundary
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.MinPlayers);
            Assert.Equal(5, result.MaxPlayers);
        }

        #endregion

        #region Test Case #5: MinPlayers < MaxPlayers (Boundary - Below boundary)

        /// <summary>
        /// Test Case #5: When MinPlayers less than MaxPlayers, returns updated template successfully
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenMinPlayersLessThanMaxPlayers_ReturnsUpdatedTemplate()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
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
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.MinPlayers);
            Assert.Equal(8, result.MaxPlayers);
        }

        #endregion

        #region Test Case #6: TotalPercent = 99.98 (Boundary - Below valid range)

        /// <summary>
        /// Test Case #6: When total percent is 99.98 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenTotalPercentIs99_98_ThrowsInvalidOperationException()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.98 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 99.98, error = 0.02 > 0.01
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.UpdateTemplateAsync(1, "user-123", dto));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);
        }

        #endregion

        #region Test Case #7: TotalPercent = 99.99 (Boundary - At valid boundary)

        /// <summary>
        /// Test Case #7: When total percent is 99.99 (error = 0.01), returns updated template (valid)
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenTotalPercentIs99_99_ReturnsUpdatedTemplate()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Boundary Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 59.99 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 99.99, error = 0.01 (exactly at boundary)
                }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Boundary Test Template", result.Name);
        }

        #endregion

        #region Test Case #8: TotalPercent = 100.01 (Boundary - At valid boundary)

        /// <summary>
        /// Test Case #8: When total percent is 100.01 (error = 0.01), returns updated template (valid)
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenTotalPercentIs100_01_ReturnsUpdatedTemplate()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Boundary Upper Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.01 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 100.01, error = 0.01 (exactly at boundary)
                }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Boundary Upper Test Template", result.Name);
        }

        #endregion

        #region Test Case #9: TotalPercent = 100.02 (Boundary - Above valid range)

        /// <summary>
        /// Test Case #9: When total percent is 100.02 (error > 0.01), throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenTotalPercentIs100_02_ThrowsInvalidOperationException()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Test Template",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 60.02 },
                    new() { Rank = 2, Percent = 40 }
                    // Total = 100.02, error = 0.02 > 0.01
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.UpdateTemplateAsync(1, "user-123", dto));
            Assert.Contains("Total percentage must be exactly 100%", ex.Message);
        }

        #endregion

        #region Test Case #10: Happy path - All valid, returns correct DTO

        /// <summary>
        /// Test Case #10: When all data is valid, returns correct updated DTO with all properties
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenAllValid_ReturnsCorrectDto()
        {
            // Arrange
            var existingTemplate = await CreateExistingTemplateAsync(42, "user-123", "Old Template");
            
            var distribution = new List<RankPercentDto>
            {
                new() { Rank = 1, Percent = 60 },
                new() { Rank = 2, Percent = 30 },
                new() { Rank = 3, Percent = 10 }
            };
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Updated Template",
                MinPlayers = 4,
                MaxPlayers = 16,
                Distribution = distribution
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(42, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("Updated Template", result.Name);
            Assert.Equal(4, result.MinPlayers);
            Assert.Equal(16, result.MaxPlayers);
            Assert.Equal(3, result.Places); // Distribution.Count
            Assert.Equal(distribution, result.Distribution);
        }

        #endregion

        #region Test Case #11: Happy path - Saves changes to database

        /// <summary>
        /// Test Case #11: When valid, saves changes to database correctly
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenValid_SavesChangesToDatabase()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123", "Old Template");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "Database Update Test",
                MinPlayers = 5,
                MaxPlayers = 15,
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert - Verify entity was updated in database
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == 1);
            Assert.NotNull(savedEntity);
            Assert.Equal("Database Update Test", savedEntity.Name);
            Assert.Equal(5, savedEntity.MinPlayers);
            Assert.Equal(15, savedEntity.MaxPlayers);
            Assert.Equal("user-123", savedEntity.OwnerUserId); // Owner should remain the same
        }

        #endregion

        #region Test Case #12: Edge - Name is trimmed

        /// <summary>
        /// Test Case #12: Verify Name is trimmed (leading/trailing spaces removed)
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_VerifyNameIsTrimmed()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123", "Old Template");
            
            var dto = new CreatePayoutTemplateDto
            {
                Name = "  Updated Template With Spaces  ", // Spaces at both ends
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = new List<RankPercentDto> { new() { Rank = 1, Percent = 100 } }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Template With Spaces", result.Name); // Trimmed in DTO
            
            // Verify entity in DB is also trimmed
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == 1);
            Assert.Equal("Updated Template With Spaces", savedEntity!.Name);
        }

        #endregion

        #region Test Case #13: Edge - Places equals Distribution.Count

        /// <summary>
        /// Test Case #13: Verify Places equals Distribution.Count
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_VerifyPlacesEqualsDistributionCount()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
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
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Places);
            
            // Verify entity in DB
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == 1);
            Assert.Equal(5, savedEntity!.Places);
        }

        #endregion

        #region Test Case #14: Edge - PercentJson is serialized correctly

        /// <summary>
        /// Test Case #14: Verify PercentJson is serialized correctly as JSON string
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_VerifyPercentJsonIsSerialized()
        {
            // Arrange
            await CreateExistingTemplateAsync(1, "user-123");
            
            var distribution = new List<RankPercentDto>
            {
                new() { Rank = 1, Percent = 70 },
                new() { Rank = 2, Percent = 30 }
            };
            var dto = new CreatePayoutTemplateDto
            {
                Name = "JSON Serialization Test",
                MinPlayers = 4,
                MaxPlayers = 8,
                Distribution = distribution
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert - Verify PercentJson in database
            var savedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == 1);
            Assert.NotNull(savedEntity);
            Assert.NotNull(savedEntity.PercentJson);
            Assert.NotEmpty(savedEntity.PercentJson);

            // Verify JSON can be deserialized back to original data
            var deserialized = JsonSerializer.Deserialize<List<RankPercentDto>>(savedEntity.PercentJson);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Count);
            Assert.Equal(1, deserialized[0].Rank);
            Assert.Equal(70, deserialized[0].Percent);
            Assert.Equal(2, deserialized[1].Rank);
            Assert.Equal(30, deserialized[1].Percent);
        }

        #endregion

        #region Test Case #15: Edge - Entity properties are updated correctly

        /// <summary>
        /// Test Case #15: Verify all entity properties are updated from old values to new values
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_VerifyEntityPropertiesAreUpdated()
        {
            // Arrange - Create template with OLD values
            var oldTemplate = new PayoutTemplate
            {
                Id = 1,
                OwnerUserId = "user-123",
                Name = "Old Name",
                MinPlayers = 2,
                MaxPlayers = 4,
                Places = 1,
                PercentJson = "[{\"Rank\":1,\"Percent\":100}]"
            };
            _dbContext.PayoutTemplates.Add(oldTemplate);
            await _dbContext.SaveChangesAsync();
            
            // Prepare NEW values
            var dto = new CreatePayoutTemplateDto
            {
                Name = "New Name",
                MinPlayers = 8,
                MaxPlayers = 32,
                Distribution = new List<RankPercentDto>
                {
                    new() { Rank = 1, Percent = 50 },
                    new() { Rank = 2, Percent = 30 },
                    new() { Rank = 3, Percent = 20 }
                }
            };

            // Act
            var result = await _sut.UpdateTemplateAsync(1, "user-123", dto);

            // Assert - Verify ALL properties are updated
            Assert.NotNull(result);
            
            // Verify in DB
            var updatedEntity = await _dbContext.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == 1);
            Assert.NotNull(updatedEntity);
            
            // Check each property was updated from OLD to NEW
            Assert.Equal("New Name", updatedEntity.Name);           // Was "Old Name"
            Assert.Equal(8, updatedEntity.MinPlayers);              // Was 2
            Assert.Equal(32, updatedEntity.MaxPlayers);             // Was 4
            Assert.Equal(3, updatedEntity.Places);                  // Was 1
            Assert.NotEqual("[{\"Rank\":1,\"Percent\":100}]", updatedEntity.PercentJson); // Changed
            
            // OwnerUserId should NOT change
            Assert.Equal("user-123", updatedEntity.OwnerUserId);
        }

        #endregion
    }
}

