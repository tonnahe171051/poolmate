using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class TournamentTableService : ITournamentTableService
{
    private readonly ApplicationDbContext _db;

    public TournamentTableService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static bool CanEditBracket(Tournament t)
        => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);
     
    private const int MaxTablesPerTournament = 128;

    public async Task<TournamentTable?> AddTournamentTableAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentTableModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        var existingTableCount = await _db.TournamentTables.CountAsync(x => x.TournamentId == tournamentId, ct);
        if (existingTableCount >= MaxTablesPerTournament)
        {
            throw new InvalidOperationException($"Cannot add more than {MaxTablesPerTournament} tables to a tournament.");
        }    

        var table = new TournamentTable
        {
            TournamentId = tournamentId,
            Label = model.Label.Trim(),
            Manufacturer = model.Manufacturer?.Trim(),
            SizeFoot = model.SizeFoot,
            LiveStreamUrl = model.LiveStreamUrl?.Trim(),
            Status = TableStatus.Open,
            IsStreaming = false
        };

        _db.TournamentTables.Add(table);
        await _db.SaveChangesAsync(ct);
        return table;
    }

    public async Task<BulkAddTablesResult?> AddMultipleTournamentTablesAsync(
        int tournamentId,
        string ownerUserId,
        AddMultipleTournamentTablesModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        var existingTableCount = await _db.TournamentTables.CountAsync(x => x.TournamentId == tournamentId, ct);
        
        var tablesToAdd = model.EndNumber - model.StartNumber + 1;
        var totalAfterAdd = existingTableCount + tablesToAdd;
        
        if (totalAfterAdd > MaxTablesPerTournament)
        {
            var availableSlots = MaxTablesPerTournament - existingTableCount;
            throw new InvalidOperationException(
                $"Cannot add {tablesToAdd} tables. Tournament currently has {existingTableCount} tables and can only add {availableSlots} more (max {MaxTablesPerTournament}).");
        }

        var result = new BulkAddTablesResult();
        var toAdd = new List<TournamentTable>();

        for (var i = model.StartNumber; i <= model.EndNumber; i++)
        {
            toAdd.Add(new TournamentTable
            {
                TournamentId = tournamentId,
                Label = $"Table {i}",
                Manufacturer = model.Manufacturer?.Trim(),
                SizeFoot = model.SizeFoot,
                Status = TableStatus.Open,
                IsStreaming = false
            });
        }

        if (toAdd.Count == 0) return result;

        _db.TournamentTables.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        result.AddedCount = toAdd.Count;
        result.Added = toAdd
            .Select(x => new BulkAddTablesResult.Item
            {
                Id = x.Id,
                Label = x.Label
            })
            .ToList();

        return result;
    }

    public async Task<bool> UpdateTournamentTableAsync(
        int tournamentId,
        int tableId,
        string ownerUserId,
        UpdateTournamentTableModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var table = await _db.TournamentTables
            .FirstOrDefaultAsync(x => x.Id == tableId && x.TournamentId == tournamentId, ct);
        if (table is null) return false;

        if (!string.IsNullOrWhiteSpace(model.Label))
            table.Label = model.Label.Trim();

        if (model.Manufacturer != null)
            table.Manufacturer = model.Manufacturer.Trim();

        if (model.SizeFoot.HasValue)
            table.SizeFoot = model.SizeFoot.Value;

        if (model.Status.HasValue)
            table.Status = model.Status.Value;

        if (model.IsStreaming.HasValue)
            table.IsStreaming = model.IsStreaming.Value;

        if (model.LiveStreamUrl != null)
            table.LiveStreamUrl = model.LiveStreamUrl.Trim();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeleteTablesResult?> DeleteTournamentTablesAsync(
        int tournamentId,
        string ownerUserId,
        DeleteTablesModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot delete tables after tournament has started or completed.");
        }

        var result = new DeleteTablesResult();
        if (model.TableIds.Count == 0) return result;

        var existingTables = await _db.TournamentTables
            .Where(x => x.TournamentId == tournamentId && model.TableIds.Contains(x.Id))
            .ToListAsync(ct);

        var existingIds = existingTables.Select(x => x.Id).ToHashSet();

        foreach (var requestedId in model.TableIds)
        {
            if (!existingIds.Contains(requestedId))
            {
                result.Failed.Add(new DeleteTablesResult.FailedItem
                {
                    TableId = requestedId,
                    Reason = "Table not found or doesn't belong to this tournament"
                });
            }
        }

        if (existingTables.Count > 0)
        {
            _db.TournamentTables.RemoveRange(existingTables);
            await _db.SaveChangesAsync(ct);

            result.DeletedCount = existingTables.Count;
            result.DeletedIds = existingTables.Select(x => x.Id).ToList();
        }

        return result;
    }

    public async Task<List<TournamentTableDto>> GetTournamentTablesAsync(
        int tournamentId,
        CancellationToken ct)
    {
        var tables = await _db.TournamentTables
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId)
            .OrderBy(x => x.Label)
            .Select(x => new TournamentTableDto
            {
                Id = x.Id,
                Label = x.Label,
                Manufacturer = x.Manufacturer,
                SizeFoot = x.SizeFoot,
                Status = x.Status,
                IsStreaming = x.IsStreaming,
                LiveStreamUrl = x.LiveStreamUrl
            })
            .ToListAsync(ct);

        return tables;
    }
}
