using PoolMate.Api.Dtos.PlayerProfile;

namespace PoolMate.Api.Services;

public interface IPlayerProfileService
{
    Task<CreatePlayerProfileResponseDto?> CreatePlayerProfileAsync(
        CreatePlayerProfileDto dto,
        string userId,
        CancellationToken ct = default);

    Task<List<PlayerProfileDetailDto>> GetMyPlayerProfilesAsync(
        string userId,
        CancellationToken ct = default);
    
}