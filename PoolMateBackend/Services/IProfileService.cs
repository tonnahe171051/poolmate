using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.UserProfile;

namespace PoolMate.Api.Services
{
    public interface IProfileService
    {
        Task<Response> UpdateAsync(string userId, UpdateProfileModel model, CancellationToken ct);
        Task<Response> MeAsync(string userId, CancellationToken ct);
        Task<Response> GetUserProfileAsync(string targetUserId, CancellationToken ct);
    }

}
