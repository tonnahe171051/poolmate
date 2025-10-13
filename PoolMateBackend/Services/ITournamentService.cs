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

        Task<List<PayoutTemplateDto>> GetPayoutTemplatesAsync(CancellationToken ct);
        Task<PagingList<TournamentListDto>> GetTournamentsAsync(
        string? searchName = null,
        TournamentStatus? status = null,
        GameType? gameType = null,
        int pageIndex = 1,
        int pageSize = 10,
        CancellationToken ct = default);
        Task<TournamentPlayer?> AddTournamentPlayerAsync(
        int tournamentId, string ownerUserId, AddTournamentPlayerModel m, CancellationToken ct);
        Task<BulkAddPlayersResult> BulkAddPlayersPerLineAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayersPerLineModel m,
        CancellationToken ct);
        Task<List<PlayerSearchItemDto>> SearchPlayersAsync(string q, int limit, CancellationToken ct);

        Task<bool> LinkTournamentPlayerAsync(int tournamentId, int tpId, string ownerUserId,
            LinkPlayerRequest m, CancellationToken ct);

        Task<bool> UnlinkTournamentPlayerAsync(int tournamentId, int tpId, string ownerUserId,
            CancellationToken ct);

        Task<int?> CreateProfileFromSnapshotAndLinkAsync(int tournamentId, int tpId, string ownerUserId,
            CreateProfileFromSnapshotRequest m, CancellationToken ct);
        Task<List<TournamentPlayerListDto>> GetTournamentPlayersAsync(
        int tournamentId,
        string? searchName = null,
        CancellationToken ct = default);
        Task<bool> UpdateTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        UpdateTournamentPlayerModel m,
        CancellationToken ct);
        Task<TournamentTable?> AddTournamentTableAsync(
        int tournamentId, string ownerUserId, AddTournamentTableModel m, CancellationToken ct);

        Task<BulkAddTablesResult> AddMultipleTournamentTablesAsync(
            int tournamentId, string ownerUserId, AddMultipleTournamentTablesModel m, CancellationToken ct);

        Task<bool> UpdateTournamentTableAsync(
            int tournamentId, int tableId, string ownerUserId, UpdateTournamentTableModel m, CancellationToken ct);

        Task<DeleteTablesResult?> DeleteTournamentTablesAsync(
            int tournamentId, string ownerUserId, DeleteTablesModel m, CancellationToken ct);

        Task<List<TournamentTableDto>> GetTournamentTablesAsync(
            int tournamentId, CancellationToken ct = default);

        Task<DeletePlayersResult?> DeleteTournamentPlayersAsync(
            int tournamentId, string ownerUserId, DeletePlayersModel m, CancellationToken ct);

        Task<TournamentDetailDto?> GetTournamentDetailAsync(int id, CancellationToken ct);

        Task<bool> DeleteTournamentAsync(int id, string ownerUserId, CancellationToken ct);








    }
}


