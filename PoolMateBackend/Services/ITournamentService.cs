using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public interface ITournamentService
    {
        Task<int?> CreateAsync(string ownerUserId, CreateTournamentModel m, CancellationToken ct);
        Task<bool> UpdateAsync(int id, string ownerUserId, UpdateTournamentModel m, CancellationToken ct);
        Task<bool> UpdateFlyerAsync(int id, string ownerUserId, UpdateFlyerModel m, CancellationToken ct);
        Task<bool> StartAsync(int id, string ownerUserId, CancellationToken ct);
        Task<bool> EndAsync(int id, string ownerUserId, CancellationToken ct);
        Task<PayoutPreviewResponse> PreviewPayoutAsync(PreviewPayoutRequest m, CancellationToken ct);
        Task<Tournament?> GetAsync(int id, CancellationToken ct);
        Task<List<PayoutTemplateDto>> GetPayoutTemplatesAsync(CancellationToken ct);
    }
}
