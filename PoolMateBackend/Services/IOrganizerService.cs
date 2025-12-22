using PoolMate.Api.Dtos.Organizer;

namespace PoolMate.Api.Services
{
    public interface IOrganizerService
    {
        Task<OrganizerDto> RegisterAsync(string userId, RegisterOrganizerRequest request, CancellationToken ct = default);
        Task<OrganizerDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task<bool> IsEmailRegisteredAsync(string email, CancellationToken ct = default);
    }
}
