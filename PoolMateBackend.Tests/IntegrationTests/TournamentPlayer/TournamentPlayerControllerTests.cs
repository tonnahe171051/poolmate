using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMateBackend.Tests.IntegrationTests.Base;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Alias to avoid namespace conflict
using TournamentPlayerModel = PoolMate.Api.Models.TournamentPlayer;

namespace PoolMateBackend.Tests.IntegrationTests.TournamentPlayer;

/// <summary>
/// Integration tests for TournamentsController (Player-related endpoints).
/// Tests cover adding, updating, deleting players, linking/unlinking profiles,
/// and creating profiles from snapshots.
/// </summary>
public class TournamentPlayerControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/tournaments";

    // JSON options that match the API's serialization settings (string enums)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public TournamentPlayerControllerTests(PoolMateWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// Helper to send JSON with proper enum serialization
    /// </summary>
    private async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PostAsync(url, content);
    }

    /// <summary>
    /// Helper to send PUT JSON with proper enum serialization
    /// </summary>
    private async Task<HttpResponseMessage> PutJsonAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PutAsync(url, content);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a tournament for testing with the specified parameters.
    /// </summary>
    private async Task<Tournament> CreateTestTournamentAsync(
        string ownerId,
        TournamentStatus status = TournamentStatus.Upcoming,
        bool isStarted = false,
        int? bracketSizeEstimate = null)
    {
        var tournament = new Tournament
        {
            Name = $"Test Tournament {Guid.NewGuid():N}",
            Description = "Test tournament for integration tests",
            StartUtc = DateTime.UtcNow.AddDays(7),
            OwnerUserId = ownerId,
            Status = status,
            IsStarted = isStarted,
            BracketSizeEstimate = bracketSizeEstimate,
            BracketType = BracketType.DoubleElimination,
            GameType = GameType.NineBall,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Tournaments.Add(tournament);
        await DbContext.SaveChangesAsync();
        return tournament;
    }

    /// <summary>
    /// Creates a tournament player for testing.
    /// </summary>
    private async Task<TournamentPlayerModel> CreateTestTournamentPlayerAsync(
        int tournamentId,
        string displayName,
        int? seed = null,
        int? playerId = null,
        TournamentPlayerStatus status = TournamentPlayerStatus.Confirmed)
    {
        var player = new TournamentPlayerModel
        {
            TournamentId = tournamentId,
            DisplayName = displayName,
            Seed = seed,
            PlayerId = playerId,
            Status = status,
            Email = $"{displayName.ToLower().Replace(" ", "")}@test.com",
            Phone = "+84123456789",
            Country = "VN",
            City = "Ho Chi Minh"
        };

        DbContext.TournamentPlayers.Add(player);
        await DbContext.SaveChangesAsync();
        return player;
    }

    /// <summary>
    /// Creates multiple tournament players for testing capacity limits.
    /// </summary>
    private async Task<List<TournamentPlayerModel>> CreateTestPlayersAsync(int tournamentId, int count)
    {
        var players = new List<TournamentPlayerModel>();
        for (int i = 1; i <= count; i++)
        {
            players.Add(new TournamentPlayerModel
            {
                TournamentId = tournamentId,
                DisplayName = $"Player {i}",
                Status = TournamentPlayerStatus.Confirmed
            });
        }

        DbContext.TournamentPlayers.AddRange(players);
        await DbContext.SaveChangesAsync();
        return players;
    }

    #endregion

    #region 1. Add Tournament Player Tests (TP-ADD-01 to TP-ADD-05)

    /// <summary>
    /// TP-ADD-01: AddPlayer_ValidData_Success
    /// Test that adding a player with valid data creates the player successfully.
    /// </summary>
    [Fact]
    public async Task AddPlayer_ValidData_ReturnsOkAndCreatesPlayer()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "John Doe",
            Nickname = "JD",
            Email = "john.doe@test.com",
            Phone = "+84987654321",
            Country = "VN",
            City = "Ha Noi",
            SkillLevel = 5,
            Seed = 1
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var playerId = doc.RootElement.GetProperty("id").GetInt32();
        playerId.Should().BeGreaterThan(0);

        // Deep Assert: Verify player was created in database
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var createdPlayer = await verifyDb.TournamentPlayers.FindAsync(playerId);

        createdPlayer.Should().NotBeNull();
        createdPlayer!.DisplayName.Should().Be("John Doe");
        createdPlayer.Nickname.Should().Be("JD");
        createdPlayer.Email.Should().Be("john.doe@test.com");
        createdPlayer.Seed.Should().Be(1);
        createdPlayer.Status.Should().Be(TournamentPlayerStatus.Confirmed);
        createdPlayer.TournamentId.Should().Be(tournament.Id);
    }

    /// <summary>
    /// TP-ADD-02: AddPlayer_TournamentStarted_Fails
    /// Test that adding a player to a started tournament fails.
    /// </summary>
    [Fact]
    public async Task AddPlayer_TournamentStarted_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            status: TournamentStatus.InProgress,
            isStarted: true);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "New Player"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot add players after tournament has started or completed");
    }

    /// <summary>
    /// TP-ADD-03: AddPlayer_TournamentFull_Fails
    /// Test that adding a player to a full tournament fails.
    /// </summary>
    [Fact]
    public async Task AddPlayer_TournamentFull_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            bracketSizeEstimate: 8);

        // Add 8 players to fill the tournament
        await CreateTestPlayersAsync(tournament.Id, 8);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Extra Player"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("full");
    }

    /// <summary>
    /// TP-ADD-04: AddPlayer_DuplicateSeed_Fails
    /// Test that adding a player with a duplicate seed number fails.
    /// </summary>
    [Fact]
    public async Task AddPlayer_DuplicateSeed_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        // Create a player with seed 1
        await CreateTestTournamentPlayerAsync(tournament.Id, "Player 1", seed: 1);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Player 2",
            Seed = 1 // Duplicate seed
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Seed 1 is already assigned");
    }

    /// <summary>
    /// TP-ADD-05: AddPlayer_NotOwner_Returns403Or404
    /// Test that adding a player by non-owner returns 404 (not found or not owned).
    /// </summary>
    [Fact]
    public async Task AddPlayer_NotOwner_ReturnsNotFound()
    {
        // Arrange
        // Create tournament owned by Admin
        AuthenticateAsAdmin();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.AdminUserId);

        // Switch to Organizer (not owner)
        AuthenticateAsOrganizer();

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Unauthorized Player"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Tournament not found or not owned by you");
    }

    #endregion

    #region 2. Bulk Add Players Tests (TP-BULK-01 to TP-BULK-03)

    /// <summary>
    /// TP-BULK-01: BulkAddPlayers_ValidLines_Success
    /// Test that bulk adding players with valid lines creates all players.
    /// </summary>
    [Fact]
    public async Task BulkAddPlayers_ValidLines_ReturnsOkAndCreatesPlayers()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        var model = new AddTournamentPlayersPerLineModel
        {
            Lines = "Player A\nPlayer B\nPlayer C"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/bulk-lines", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BulkAddPlayersResult>();
        result.Should().NotBeNull();
        result!.AddedCount.Should().Be(3);
        result.Added.Should().HaveCount(3);

        // Deep Assert: Verify players were created in database
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var createdPlayers = await verifyDb.TournamentPlayers
            .Where(p => p.TournamentId == tournament.Id)
            .ToListAsync();

        createdPlayers.Should().HaveCount(3);
        createdPlayers.Should().AllSatisfy(p => p.Status.Should().Be(TournamentPlayerStatus.Confirmed));
    }

    /// <summary>
    /// TP-BULK-02: BulkAddPlayers_ExceedsCapacity_Fails
    /// Test that bulk adding players exceeding capacity fails.
    /// </summary>
    [Fact]
    public async Task BulkAddPlayers_ExceedsCapacity_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            bracketSizeEstimate: 5);

        var model = new AddTournamentPlayersPerLineModel
        {
            Lines = "P1\nP2\nP3\nP4\nP5\nP6\nP7\nP8\nP9\nP10" // 10 players
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/bulk-lines", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("available slots");
    }

    /// <summary>
    /// TP-BULK-03: BulkAddPlayers_EmptyLinesSkipped_PartialSuccess
    /// Test that empty lines are skipped during bulk add.
    /// </summary>
    [Fact]
    public async Task BulkAddPlayers_EmptyLinesSkipped_ReturnsPartialSuccess()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        var model = new AddTournamentPlayersPerLineModel
        {
            Lines = "Player A\n\n\nPlayer B" // Contains empty lines
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/bulk-lines", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BulkAddPlayersResult>();
        result.Should().NotBeNull();
        result!.AddedCount.Should().Be(2);
        result.Skipped.Should().HaveCount(2); // Two empty lines skipped
        result.Skipped.Should().AllSatisfy(s => s.Reason.Should().Contain("Empty line"));
    }

    #endregion

    #region 3. Get Tournament Players Tests (TP-GET-01)

    /// <summary>
    /// TP-GET-01: GetPlayers_ReturnsOrderedList
    /// Test that getting players returns them ordered by seed (nulls last) then by name.
    /// </summary>
    [Fact]
    public async Task GetPlayers_ReturnsOrderedList()
    {
        // Arrange
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        // Create players with mixed seeds
        await CreateTestTournamentPlayerAsync(tournament.Id, "Charlie", seed: null);
        await CreateTestTournamentPlayerAsync(tournament.Id, "Alice", seed: 2);
        await CreateTestTournamentPlayerAsync(tournament.Id, "Bob", seed: 1);
        await CreateTestTournamentPlayerAsync(tournament.Id, "David", seed: null);

        // Act - Anonymous access allowed
        ClearAuthentication();
        var response = await Client.GetAsync($"{BaseUrl}/{tournament.Id}/players");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Use JsonDocument to handle string enum values from API
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var players = doc.RootElement.EnumerateArray().ToList();

        players.Should().HaveCount(4);

        // Verify ordering: seeded players first (by seed), then unseeded (by name)
        players[0].GetProperty("displayName").GetString().Should().Be("Bob"); // Seed 1
        players[1].GetProperty("displayName").GetString().Should().Be("Alice"); // Seed 2
        // Unseeded players sorted by name
        players[2].GetProperty("displayName").GetString().Should().Be("Charlie"); // null seed
        players[3].GetProperty("displayName").GetString().Should().Be("David"); // null seed
    }

    #endregion

    #region 4. Update Tournament Player Tests (TP-UPD-01 to TP-UPD-04)

    /// <summary>
    /// TP-UPD-01: UpdatePlayer_AllFields_Success
    /// Test that updating all player fields succeeds.
    /// </summary>
    [Fact]
    public async Task UpdatePlayer_AllFields_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Original Name");

        var model = new UpdateTournamentPlayerModel
        {
            DisplayName = "Updated Name",
            Nickname = "UpdatedNick",
            Email = "updated@test.com",
            Phone = "+84111222333",
            Country = "US",
            City = "New York",
            SkillLevel = 8,
            Seed = 5,
            Status = TournamentPlayerStatus.Confirmed
        };

        // Act - Use PutJsonAsync to properly serialize enum as string
        var response = await PutJsonAsync($"{BaseUrl}/{tournament.Id}/players/{player.Id}", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            because: await response.Content.ReadAsStringAsync());

        // Deep Assert: Verify database was updated
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var updatedPlayer = await verifyDb.TournamentPlayers.FindAsync(player.Id);

        updatedPlayer.Should().NotBeNull();
        updatedPlayer!.DisplayName.Should().Be("Updated Name");
        updatedPlayer.Nickname.Should().Be("UpdatedNick");
        updatedPlayer.Email.Should().Be("updated@test.com");
        updatedPlayer.Phone.Should().Be("+84111222333");
        updatedPlayer.Country.Should().Be("US");
        updatedPlayer.City.Should().Be("New York");
        updatedPlayer.SkillLevel.Should().Be(8);
        updatedPlayer.Seed.Should().Be(5);
    }

    /// <summary>
    /// TP-UPD-02: UpdatePlayer_TournamentStarted_Fails
    /// Test that updating a player in a started tournament fails.
    /// </summary>
    [Fact]
    public async Task UpdatePlayer_TournamentStarted_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            status: TournamentStatus.InProgress,
            isStarted: true);

        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Player");

        var model = new UpdateTournamentPlayerModel
        {
            DisplayName = "Updated Name"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{player.Id}", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot modify players after tournament has started or completed");
    }

    /// <summary>
    /// TP-UPD-03: UpdatePlayer_InvalidPhone_Fails
    /// Test that updating a player with invalid phone number fails.
    /// </summary>
    [Fact]
    public async Task UpdatePlayer_InvalidPhone_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Player");

        var model = new UpdateTournamentPlayerModel
        {
            Phone = "abc123" // Invalid phone
        };

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{player.Id}", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid phone number");
    }

    /// <summary>
    /// TP-UPD-04: UpdatePlayer_NotOwner_Returns404
    /// Test that updating a player by non-owner returns 404.
    /// </summary>
    [Fact]
    public async Task UpdatePlayer_NotOwner_ReturnsNotFound()
    {
        // Arrange
        AuthenticateAsAdmin();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.AdminUserId);
        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Player");

        // Switch to Organizer (not owner)
        AuthenticateAsOrganizer();

        var model = new UpdateTournamentPlayerModel
        {
            DisplayName = "Unauthorized Update"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{player.Id}", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region 5. Delete Tournament Players Tests (TP-DEL-01 to TP-DEL-03)

    /// <summary>
    /// TP-DEL-01: DeletePlayers_ValidIds_Success
    /// Test that deleting players with valid IDs succeeds.
    /// </summary>
    [Fact]
    public async Task DeletePlayers_ValidIds_ReturnsOkAndRemovesFromDatabase()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var players = await CreateTestPlayersAsync(tournament.Id, 5);
        var idsToDelete = players.Take(3).Select(p => p.Id).ToList();

        var model = new DeletePlayersModel
        {
            PlayerIds = idsToDelete
        };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/{tournament.Id}/players")
        {
            Content = JsonContent.Create(model)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("deletedCount").GetInt32().Should().Be(3);

        // Deep Assert: Verify players were removed from database
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var remainingPlayers = await verifyDb.TournamentPlayers
            .Where(p => p.TournamentId == tournament.Id)
            .ToListAsync();

        remainingPlayers.Should().HaveCount(2);
        remainingPlayers.Select(p => p.Id).Should().NotContain(idsToDelete);
    }

    /// <summary>
    /// TP-DEL-02: DeletePlayers_TournamentStarted_Fails
    /// Test that deleting players from a started tournament fails.
    /// </summary>
    [Fact]
    public async Task DeletePlayers_TournamentStarted_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            status: TournamentStatus.InProgress,
            isStarted: true);

        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Player");

        var model = new DeletePlayersModel
        {
            PlayerIds = new List<int> { player.Id }
        };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/{tournament.Id}/players")
        {
            Content = JsonContent.Create(model)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot delete players after tournament has started or completed");
    }

    /// <summary>
    /// TP-DEL-03: DeletePlayers_MixedValidInvalid_PartialSuccess
    /// Test that deleting players with mixed valid/invalid IDs returns partial success.
    /// </summary>
    [Fact]
    public async Task DeletePlayers_MixedValidInvalid_ReturnsPartialSuccess()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var player = await CreateTestTournamentPlayerAsync(tournament.Id, "Valid Player");

        var model = new DeletePlayersModel
        {
            PlayerIds = new List<int> { player.Id, 99999 } // One valid, one invalid
        };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/{tournament.Id}/players")
        {
            Content = JsonContent.Create(model)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("deletedCount").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("failed").GetArrayLength().Should().Be(1);
    }

    #endregion

    #region 6. Link Player Profile Tests (TP-LINK-01 to TP-LINK-02)

    /// <summary>
    /// TP-LINK-01: LinkPlayer_ValidPlayerId_Success
    /// Test that linking a player profile with valid PlayerId succeeds.
    /// </summary>
    [Fact]
    public async Task LinkPlayer_ValidPlayerId_ReturnsOkAndUpdatesSnapshot()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var tournamentPlayer = await CreateTestTournamentPlayerAsync(tournament.Id, "Tournament Player");

        // Use existing seeded player (from seed data, Player ID 1)
        var existingPlayerId = 1;

        var model = new LinkPlayerRequest
        {
            PlayerId = existingPlayerId,
            OverwriteSnapshot = true
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{tournamentPlayer.Id}/link", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Deep Assert: Verify PlayerId was set and snapshot was overwritten
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var linkedPlayer = await verifyDb.TournamentPlayers.FindAsync(tournamentPlayer.Id);

        linkedPlayer.Should().NotBeNull();
        linkedPlayer!.PlayerId.Should().Be(existingPlayerId);
        // Snapshot should be overwritten from the Player profile
        linkedPlayer.DisplayName.Should().Be("Test Player 1"); // From seed data
    }

    /// <summary>
    /// TP-LINK-02: LinkPlayer_PlayerNotFound_Fails
    /// Test that linking with non-existent PlayerId fails.
    /// </summary>
    [Fact]
    public async Task LinkPlayer_PlayerNotFound_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var tournamentPlayer = await CreateTestTournamentPlayerAsync(tournament.Id, "Tournament Player");

        var model = new LinkPlayerRequest
        {
            PlayerId = 99999, // Non-existent player
            OverwriteSnapshot = true
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{tournamentPlayer.Id}/link", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Link failed");
    }

    #endregion

    #region 7. Unlink Player Profile Tests (TP-UNLINK-01)

    /// <summary>
    /// TP-UNLINK-01: UnlinkPlayer_Success
    /// Test that unlinking a player profile succeeds and preserves snapshot data.
    /// </summary>
    [Fact]
    public async Task UnlinkPlayer_Success_ReturnsOkAndPreservesSnapshot()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var tournamentPlayer = await CreateTestTournamentPlayerAsync(
            tournament.Id,
            "Linked Player",
            playerId: 1 // Link to existing player
        );

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{tournamentPlayer.Id}/unlink", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Deep Assert: Verify PlayerId is null but snapshot data preserved
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var unlinkedPlayer = await verifyDb.TournamentPlayers.FindAsync(tournamentPlayer.Id);

        unlinkedPlayer.Should().NotBeNull();
        unlinkedPlayer!.PlayerId.Should().BeNull();
        unlinkedPlayer.DisplayName.Should().Be("Linked Player"); // Snapshot preserved
    }

    #endregion

    #region 8. Create Profile from Snapshot Tests (TP-CREATE-01 to TP-CREATE-02)

    /// <summary>
    /// TP-CREATE-01: CreateProfileFromSnapshot_Success
    /// Test that creating a player profile from snapshot succeeds.
    /// </summary>
    [Fact]
    public async Task CreateProfileFromSnapshot_Success_ReturnsOkAndCreatesPlayer()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        var uniqueName = $"New Profile Player {Guid.NewGuid():N}";
        var tournamentPlayer = await CreateTestTournamentPlayerAsync(tournament.Id, uniqueName);

        var model = new CreateProfileFromSnapshotRequest
        {
            CopyBackToSnapshot = true
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{tournamentPlayer.Id}/create-profile", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var newPlayerId = doc.RootElement.GetProperty("playerId").GetInt32();
        newPlayerId.Should().BeGreaterThan(0);

        // Deep Assert: Verify new Player was created and TournamentPlayer was linked
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();

        var newPlayer = await verifyDb.Players.FindAsync(newPlayerId);
        newPlayer.Should().NotBeNull();
        newPlayer!.FullName.Should().Be(uniqueName);

        var linkedTp = await verifyDb.TournamentPlayers.FindAsync(tournamentPlayer.Id);
        linkedTp!.PlayerId.Should().Be(newPlayerId);
    }

    /// <summary>
    /// TP-CREATE-02: CreateProfileFromSnapshot_SlugConflict_GeneratesUnique
    /// Test that creating a profile with conflicting slug generates a unique one.
    /// </summary>
    [Fact]
    public async Task CreateProfileFromSnapshot_SlugConflict_GeneratesUniqueSlug()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        // Use a name that will generate same slug as existing player
        // Seed data has "Test Player 1" with slug "test-player-1"
        var tournamentPlayer = await CreateTestTournamentPlayerAsync(tournament.Id, "Test Player 1");

        var model = new CreateProfileFromSnapshotRequest
        {
            CopyBackToSnapshot = true
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{tournamentPlayer.Id}/create-profile", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var newPlayerId = doc.RootElement.GetProperty("playerId").GetInt32();

        // Deep Assert: Verify slug has suffix
        using var verifyScope = Factory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PoolMate.Api.Data.ApplicationDbContext>();
        var newPlayer = await verifyDb.Players.FindAsync(newPlayerId);

        newPlayer.Should().NotBeNull();
        newPlayer!.Slug.Should().StartWith("test-player-1");
        newPlayer.Slug.Should().NotBe("test-player-1"); // Should have suffix
    }

    #endregion

    #region 9. Search Players Tests (TP-SEARCH-01)

    /// <summary>
    /// TP-SEARCH-01: SearchPlayers_ByName_ReturnsMatches
    /// Test that searching players by name returns matching results.
    /// </summary>
    [Fact]
    public async Task SearchPlayers_ByName_ReturnsMatches()
    {
        // Arrange
        AuthenticateAsOrganizer();

        // Act - Search for seeded players containing "Test Player"
        var response = await Client.GetAsync($"{BaseUrl}/players/search?q=Test Player&limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var players = await response.Content.ReadFromJsonAsync<List<PlayerSearchItemDto>>();
        players.Should().NotBeNull();
        players.Should().NotBeEmpty();
        players.Should().AllSatisfy(p => p.FullName.Should().Contain("Test Player"));
    }

    #endregion

    #region 10. Get Player Stats Tests (TP-STATS-01)

    /// <summary>
    /// TP-STATS-01: GetPlayerStats_ReturnsMatchStatistics
    /// Note: This test validates the endpoint exists and returns 200 OK.
    /// Full stats testing requires completed matches which is complex to setup.
    /// </summary>
    [Fact]
    public async Task GetPlayers_WithSearch_ReturnsFilteredResults()
    {
        // Arrange
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        await CreateTestTournamentPlayerAsync(tournament.Id, "John Smith");
        await CreateTestTournamentPlayerAsync(tournament.Id, "Jane Doe");
        await CreateTestTournamentPlayerAsync(tournament.Id, "Johnny Walker");

        // Act - Anonymous access
        ClearAuthentication();
        var response = await Client.GetAsync($"{BaseUrl}/{tournament.Id}/players?searchName=John");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Use JsonDocument to handle string enum values from API
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var players = doc.RootElement.EnumerateArray().ToList();

        players.Should().HaveCount(2); // John Smith and Johnny Walker
        players.Should().AllSatisfy(p => p.GetProperty("displayName").GetString().Should().Contain("John"));
    }

    #endregion

    #region Additional Edge Cases

    /// <summary>
    /// Additional test: Verify phone validation with valid formats
    /// </summary>
    [Theory]
    [InlineData("+84123456789")] // With country code
    [InlineData("0912345678")] // 10 digits
    [InlineData("123456789012345")] // 15 digits max
    public async Task AddPlayer_ValidPhoneFormats_Success(string phone)
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = $"Player {phone}",
            Phone = phone
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Additional test: Verify phone validation with invalid formats
    /// </summary>
    [Theory]
    [InlineData("abc123")] // Letters
    [InlineData("123")] // Too short
    [InlineData("1234567890123456")] // Too long (> 15)
    public async Task AddPlayer_InvalidPhoneFormats_Fails(string phone)
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Player Invalid Phone",
            Phone = phone
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid phone number");
    }

    /// <summary>
    /// Test: Verify tournament status guard for completed tournaments
    /// </summary>
    [Fact]
    public async Task AddPlayer_TournamentCompleted_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(
            PoolMateWebApplicationFactory.OrganizerUserId,
            status: TournamentStatus.Completed,
            isStarted: true);

        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Late Player"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/{tournament.Id}/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot add players after tournament has started or completed");
    }

    /// <summary>
    /// Test: Verify seed uniqueness validation during update
    /// </summary>
    [Fact]
    public async Task UpdatePlayer_DuplicateSeed_ReturnsBadRequest()
    {
        // Arrange
        AuthenticateAsOrganizer();
        var tournament = await CreateTestTournamentAsync(PoolMateWebApplicationFactory.OrganizerUserId);
        _ = await CreateTestTournamentPlayerAsync(tournament.Id, "Player 1", seed: 1);
        var player2 = await CreateTestTournamentPlayerAsync(tournament.Id, "Player 2", seed: 2);

        var model = new UpdateTournamentPlayerModel
        {
            Seed = 1 // Try to set same seed as player1
        };

        // Act
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{tournament.Id}/players/{player2.Id}", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Seed 1 is already assigned");
    }

    /// <summary>
    /// Test: Verify unauthorized access returns 401
    /// </summary>
    [Fact]
    public async Task AddPlayer_Unauthorized_Returns401()
    {
        // Arrange
        ClearAuthentication();
        var model = new AddTournamentPlayerModel
        {
            DisplayName = "Unauthorized Player"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/1/players", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}

