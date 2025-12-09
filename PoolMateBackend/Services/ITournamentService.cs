using PoolMate.Api.Common;
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

        Task<PagingList<UserTournamentListDto>> GetTournamentsByUserAsync(
            string ownerUserId,
            string? searchName = null,
            TournamentStatus? status = null,
            int pageIndex = 1,
            int pageSize = 10,
            CancellationToken ct = default);

        Task<List<PayoutTemplateDto>> GetPayoutTemplatesAsync(string userId, CancellationToken ct);

        Task<PagingList<TournamentListDto>> GetTournamentsAsync(
            string? searchName = null,
            TournamentStatus? status = null,
            GameType? gameType = null,
            int pageIndex = 1,
            int pageSize = 10,
            CancellationToken ct = default);

        Task<TournamentDetailDto?> GetTournamentDetailAsync(int id, CancellationToken ct);

        Task<bool> DeleteTournamentAsync(int id, string ownerUserId, CancellationToken ct);
    }
}