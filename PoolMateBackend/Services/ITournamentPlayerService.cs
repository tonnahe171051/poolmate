using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public interface ITournamentPlayerService
{
    Task<TournamentPlayer?> AddTournamentPlayerAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayerModel model,
        CancellationToken ct);

    Task<BulkAddPlayersResult> BulkAddPlayersPerLineAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayersPerLineModel model,
        CancellationToken ct);

    Task<List<PlayerSearchItemDto>> SearchPlayersAsync(string q, int limit, CancellationToken ct);

    Task<bool> LinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        LinkPlayerRequest request,
        CancellationToken ct);

    Task<bool> UnlinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CancellationToken ct);

    Task<int?> CreateProfileFromSnapshotAndLinkAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CreateProfileFromSnapshotRequest request,
        CancellationToken ct);

    Task<List<TournamentPlayerListDto>> GetTournamentPlayersAsync(
        int tournamentId,
        string? searchName,
        CancellationToken ct);

    Task<bool> UpdateTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        UpdateTournamentPlayerModel model,
        CancellationToken ct);

    Task<DeletePlayersResult?> DeleteTournamentPlayersAsync(
        int tournamentId,
        string ownerUserId,
        DeletePlayersModel model,
        CancellationToken ct);

    Task<TournamentPlayer> RegisterForTournamentAsync(
        int tournamentId,
        string userId,
        RegisterForTournamentRequest request,
        CancellationToken ct);
}
