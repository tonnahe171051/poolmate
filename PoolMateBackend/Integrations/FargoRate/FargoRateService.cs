using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PoolMate.Api.Data;
using PoolMate.Api.Integrations.FargoRate.Models;
using System.Text.Json;

namespace PoolMate.Api.Integrations.FargoRate
{
    public class FargoRateService : IFargoRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FargoRateService> _logger;
        private readonly FargoRateOptions _options;
        private readonly ApplicationDbContext _dbContext;

        public FargoRateService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<FargoRateService> logger,
            IOptions<FargoRateOptions> options,
            ApplicationDbContext dbContext)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
            _dbContext = dbContext;
        }

        private async Task<FargoPlayerResult?> SearchPlayerByNameAsync(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                _logger.LogWarning("SearchPlayerByNameAsync called with empty player name");
                return null;
            }

            // Check cache first
            var cacheKey = $"fargo_search_{playerName.ToLowerInvariant()}";
            if (_cache.TryGetValue<FargoPlayerResult?>(cacheKey, out var cachedResult))
            {
                _logger.LogInformation("Cache hit for player: {PlayerName}", playerName);
                return cachedResult;
            }

            try
            {
                _logger.LogInformation("Searching Fargo for player: {PlayerName}", playerName);

                var response = await _httpClient.GetAsync($"indexsearch?q={Uri.EscapeDataString(playerName)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Fargo API returned status {StatusCode} for player: {PlayerName}",
                        response.StatusCode, playerName);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<FargoSearchResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (searchResponse?.Value == null || !searchResponse.Value.Any())
                {
                    _logger.LogInformation("No Fargo results found for player: {PlayerName}", playerName);

                    // Cache null result to avoid repeated API calls
                    _cache.Set(cacheKey, (FargoPlayerResult?)null, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

                    return null;
                }

                // Take first result (best match)
                var firstResult = FargoPlayerResult.FromFargoPlayerData(searchResponse.Value[0]);

                _logger.LogInformation("Found Fargo player: {FullName} (Rating: {Rating})",
                    firstResult.FullName, firstResult.Rating);

                // Cache the result
                _cache.Set(cacheKey, firstResult, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

                return firstResult;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while searching Fargo for player: {PlayerName}", playerName);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error for Fargo response, player: {PlayerName}", playerName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while searching Fargo for player: {PlayerName}", playerName);
                return null;
            }
        }

        public async Task<List<PlayerFargoSearchResult>> BatchSearchPlayersAsync(List<BatchSearchRequest> requests)
        {
            if (requests == null || !requests.Any())
            {
                _logger.LogWarning("BatchSearchPlayersAsync called with empty request list");
                return new List<PlayerFargoSearchResult>();
            }

            _logger.LogInformation("Starting batch Fargo search for {Count} players", requests.Count);

            var results = new List<PlayerFargoSearchResult>();

            foreach (var request in requests)
            {
                var result = new PlayerFargoSearchResult
                {
                    TournamentPlayerId = request.TournamentPlayerId,
                    OriginalName = request.PlayerName
                };

                try
                {
                    // Search for each player 
                    var fargoResult = await SearchPlayerByNameAsync(request.PlayerName);
                    result.FargoResult = fargoResult;

                    if (fargoResult != null)
                    {
                        _logger.LogInformation("Player {PlayerId} - {PlayerName}: Found (Rating: {Rating})",
                            request.TournamentPlayerId, request.PlayerName, fargoResult.Rating);
                    }
                    else
                    {
                        _logger.LogInformation("Player {PlayerId} - {PlayerName}: Not found",
                            request.TournamentPlayerId, request.PlayerName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching for player {PlayerId} - {PlayerName}",
                        request.TournamentPlayerId, request.PlayerName);

                    result.ErrorMessage = "Search failed due to an error";
                }

                results.Add(result);

                if (requests.IndexOf(request) < requests.Count - 1)
                {
                    await Task.Delay(100); // 100ms delay between requests
                }
            }

            _logger.LogInformation("Batch search completed. Found: {FoundCount}/{TotalCount}",
                results.Count(r => r.IsFound), results.Count);

            return results;
        }

        public async Task<int> ApplyFargoRatingsAsync(int tournamentId, List<ApplyFargoRatingRequest> requests)
        {
            if (requests == null || !requests.Any())
            {
                _logger.LogWarning("ApplyFargoRatingsAsync called with empty request list");
                return 0;
            }

            _logger.LogInformation("Starting apply Fargo ratings for tournament {TournamentId}, {Count} requests",
                tournamentId, requests.Count);

            var toApply = requests.Where(r => r.Apply && r.Rating.HasValue).ToList();

            if (!toApply.Any())
            {
                _logger.LogInformation("No ratings to apply (all skipped or invalid)");
                return 0;
            }

            var playerIds = toApply.Select(r => r.TournamentPlayerId).ToList();
            var playersToUpdate = await _dbContext.TournamentPlayers
                .Where(p => playerIds.Contains(p.Id))
                .ToListAsync();

            int updatedCount = 0;

            foreach (var request in toApply)
            {
                var player = playersToUpdate.FirstOrDefault(p => p.Id == request.TournamentPlayerId);

                if (player != null)
                {
                    player.SkillLevel = request.Rating.Value;
                    updatedCount++;

                    _logger.LogInformation(
                        "Updated player {PlayerId}: SkillLevel={Rating}",
                        player.Id, request.Rating.Value);
                }
                else
                {
                    _logger.LogWarning("Player {PlayerId} not found in database", request.TournamentPlayerId);
                }
            }

            _logger.LogInformation("Recalculating seeds for all players in tournament {TournamentId}", tournamentId);

            var allPlayersInTournament = await _dbContext.TournamentPlayers
                .Where(p => p.TournamentId == tournamentId)
                .OrderByDescending(p => p.SkillLevel ?? 0) 
                .ThenBy(p => p.Id) // neu skill level giong nhau, sap xep theo Id
                .ToListAsync();

            int seed = 1;
            foreach (var player in allPlayersInTournament)
            {
                player.Seed = seed;
                _logger.LogInformation(
                    "Assigned Seed {Seed} to Player {PlayerId} (SkillLevel: {SkillLevel})",
                    seed, player.Id, player.SkillLevel ?? 0);
                seed++;
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Apply completed. Updated {UpdatedCount} players, Recalculated {TotalCount} seeds",
                updatedCount, allPlayersInTournament.Count);

            return updatedCount;
        }
    }
}
