using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public interface ITournamentTableService
{
    Task<TournamentTable?> AddTournamentTableAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentTableModel model,
        CancellationToken ct);

    Task<BulkAddTablesResult?> AddMultipleTournamentTablesAsync(
        int tournamentId,
        string ownerUserId,
        AddMultipleTournamentTablesModel model,
        CancellationToken ct);

    Task<bool> UpdateTournamentTableAsync(
        int tournamentId,
        int tableId,
        string ownerUserId,
        UpdateTournamentTableModel model,
        CancellationToken ct);

    Task<DeleteTablesResult?> DeleteTournamentTablesAsync(
        int tournamentId,
        string ownerUserId,
        DeleteTablesModel model,
        CancellationToken ct);

    Task<List<TournamentTableDto>> GetTournamentTablesAsync(int tournamentId, CancellationToken ct);
}
