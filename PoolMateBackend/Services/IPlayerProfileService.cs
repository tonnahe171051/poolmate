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

    Task<List<PlayerProfileDetailDto>> GetMyPlayerProfilesAsync(
        string userId,
        CancellationToken ct = default);

    Task<PagingList<MatchHistoryDto>> GetMatchHistoryAsync(int playerId, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
}