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
    
}