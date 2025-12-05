using PoolMate.Api.Common;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public interface IPlayerProfileService
{
    Task<CreatePlayerProfileResponseDto?> CreatePlayerProfileAsync(
        string userId,
        ApplicationUser user,
        CancellationToken ct = default);
    
    Task UpdatePlayerFromUserAsync(ApplicationUser user, CancellationToken ct = default);

    Task<List<PlayerProfileDetailDto>> GetMyPlayerProfilesAsync(
        string userId,
        CancellationToken ct = default);

    Task<PagingList<MatchHistoryDto>> GetMatchHistoryAsync(int playerId, int pageIndex = 1, int pageSize = 20,
        CancellationToken ct = default);

    Task<PlayerStatsDto> GetPlayerStatsAsync(int playerId, CancellationToken ct = default);
    

    Task<PlayerProfileDetailDto?> GetPlayerBySlugAsync(
        string slug,
        CancellationToken ct = default);

    Task<PagingList<PlayerTournamentDto>> GetMyTournamentsHistoryAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    
    Task<PagingList<MatchHistoryDto>> GetMatchHistoryByPlayerIdAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<PagingList<PlayerTournamentDto>> GetTournamentHistoryByPlayerIdAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    // List all players with pagination and filtering
    Task<PagingList<PlayerListDto>> GetAllPlayersAsync(
        PlayerListFilterDto filter,
        CancellationToken ct = default);
}