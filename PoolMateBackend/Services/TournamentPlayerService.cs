using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class TournamentPlayerService : ITournamentPlayerService
{
    private readonly ApplicationDbContext _db;

    public TournamentPlayerService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static bool CanEditBracket(Tournament t)
        => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);

    private async Task<(bool CanAdd, int CurrentCount, int? MaxLimit)> CanAddPlayersAsync(
        int tournamentId,
        int playersToAdd,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament?.BracketSizeEstimate == null)
        {
            return (true, 0, null);
        }

        var currentCount = await _db.TournamentPlayers
            .CountAsync(x => x.TournamentId == tournamentId, ct);

        var maxLimit = tournament.BracketSizeEstimate ?? 256;
        var canAdd = (currentCount + playersToAdd) <= maxLimit;

        return (canAdd, currentCount, maxLimit);
    }

    private async Task ValidateSeedAsync(int tournamentId, int? seed, int? excludeTpId, CancellationToken ct)
    {
        if (!seed.HasValue) return;

        if (seed.Value <= 0)
            throw new InvalidOperationException("Seed must be a positive number.");

        var existingPlayer = await _db.TournamentPlayers
            .Where(x => x.TournamentId == tournamentId &&
                        x.Seed == seed.Value &&
                        (excludeTpId == null || x.Id != excludeTpId))
            .FirstOrDefaultAsync(ct);

        if (existingPlayer is not null)
        {
            throw new InvalidOperationException(
                $"Seed {seed.Value} is already assigned to player '{existingPlayer.DisplayName}' in this tournament.");
        }
    }

    public async Task<TournamentPlayer?> AddTournamentPlayerAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayerModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, 1, ct);
        if (!canAdd)
        {
            throw new InvalidOperationException(
                $"Cannot add player. Tournament is full ({currentCount}/{maxLimit}).");
        }

        if (model.PlayerId.HasValue)
        {
            var exists = await _db.TournamentPlayers
                .AnyAsync(x => x.TournamentId == tournamentId && x.PlayerId == model.PlayerId, ct);
            if (exists) throw new InvalidOperationException("This player is already in the tournament.");
        }

        await ValidateSeedAsync(tournamentId, model.Seed, null, ct);

        var tp = new TournamentPlayer
        {
            TournamentId = tournamentId,
            PlayerId = model.PlayerId,
            DisplayName = model.DisplayName.Trim(),
            Nickname = model.Nickname,
            Email = model.Email,
            Phone = model.Phone,
            City = model.City,
            Country = model.Country,
            SkillLevel = model.SkillLevel,
            Seed = model.Seed,
            Status = TournamentPlayerStatus.Confirmed
        };

        _db.TournamentPlayers.Add(tp);
        await _db.SaveChangesAsync(ct);
        return tp;
    }

    public async Task<BulkAddPlayersResult> BulkAddPlayersPerLineAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayersPerLineModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament is null) throw new InvalidOperationException("Tournament not found.");
        if (tournament.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException();

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var lines = (model.Lines ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(x => x.Trim())
            .ToList();

        var result = new BulkAddPlayersResult();
        if (lines.Count == 0) return result;

        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, lines.Count, ct);
        if (!canAdd)
        {
            var availableSlots = maxLimit.HasValue ? Math.Max(0, maxLimit.Value - currentCount) : lines.Count;
            throw new InvalidOperationException(
                $"Cannot add {lines.Count} players. Tournament has {availableSlots} available slots ({currentCount}/{maxLimit}).");
        }

        var toAdd = new List<TournamentPlayer>(capacity: lines.Count);
        var defaultStatus = TournamentPlayerStatus.Confirmed;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                result.Skipped.Add(new BulkAddPlayersResult.SkippedItem
                {
                    Line = raw,
                    Reason = "Empty line"
                });
                continue;
            }

            var name = raw.Trim();
            if (name.Length > 200)
            {
                name = name[..200];
            }

            toAdd.Add(new TournamentPlayer
            {
                TournamentId = tournamentId,
                DisplayName = name,
                Status = defaultStatus,
            });
        }

        if (toAdd.Count == 0) return result;

        _db.TournamentPlayers.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        result.AddedCount = toAdd.Count;
        result.Added = toAdd
            .Select(x => new BulkAddPlayersResult.Item
            {
                Id = x.Id,
                DisplayName = x.DisplayName
            })
            .ToList();

        return result;
    }

    public async Task<List<PlayerSearchItemDto>> SearchPlayersAsync(string q, int limit, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        if (limit <= 0 || limit > 50) limit = 10;

        var query = _db.Set<Player>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qLower = q.ToLower();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(qLower));
        }

        var items = await query
            .OrderBy(p => p.FullName)
            .Take(limit)
            .Select(p => new PlayerSearchItemDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<bool> LinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        LinkPlayerRequest request,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        var player = await _db.Set<Player>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PlayerId, ct);
        if (player is null) return false;

        tp.PlayerId = player.Id;

        if (request.OverwriteSnapshot)
        {
            tp.DisplayName = player.FullName;
            tp.Email = player.Email;
            tp.Phone = player.Phone;
            tp.Country = player.Country;
            tp.City = player.City;
            tp.SkillLevel = player.SkillLevel;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnlinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        tp.PlayerId = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int?> CreateProfileFromSnapshotAndLinkAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CreateProfileFromSnapshotRequest request,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return null;

        string baseSlug = SlugHelper.GenerateSlug(tp.DisplayName);
        string finalSlug = baseSlug;
        int count = 1;
        while (await _db.Players.AsNoTracking().AnyAsync(p => p.Slug == finalSlug, ct))
        {
            finalSlug = $"{baseSlug}-{count}";
            count++;
        }

        var player = new Player
        {
            FullName = tp.DisplayName,
            Slug = finalSlug,
            Email = tp.Email,
            Phone = tp.Phone,
            Country = tp.Country,
            City = tp.City,
            SkillLevel = tp.SkillLevel,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<Player>().Add(player);
        await _db.SaveChangesAsync(ct);

        tp.PlayerId = player.Id;

        if (request.CopyBackToSnapshot)
        {
            tp.DisplayName = player.FullName;
            tp.Email = player.Email;
            tp.Phone = player.Phone;
            tp.Country = player.Country;
            tp.City = player.City;
            tp.SkillLevel = player.SkillLevel;
        }

        await _db.SaveChangesAsync(ct);
        return player.Id;
    }

    public async Task<List<TournamentPlayerListDto>> GetTournamentPlayersAsync(
        int tournamentId,
        string? searchName,
        CancellationToken ct)
    {
        var query = _db.TournamentPlayers
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId);

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            var trimmedSearch = searchName.Trim().ToLower();
            query = query.Where(x => x.DisplayName.ToLower().Contains(trimmedSearch));
        }

        var items = await query
            .OrderBy(x => x.Seed ?? int.MaxValue)
            .ThenBy(x => x.DisplayName)
            .Select(x => new TournamentPlayerListDto
            {
                Id = x.Id,
                DisplayName = x.DisplayName,
                Email = x.Email,
                Phone = x.Phone,
                Country = x.Country,
                Seed = x.Seed,
                SkillLevel = x.SkillLevel,
                Status = x.Status,
                PlayerId = x.PlayerId
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<bool> UpdateTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        UpdateTournamentPlayerModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers
            .FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot modify players after tournament has started or completed.");
        }

        if (model.Seed != tp.Seed)
        {
            await ValidateSeedAsync(tournamentId, model.Seed, tpId, ct);
        }

        if (!string.IsNullOrWhiteSpace(model.DisplayName))
            tp.DisplayName = model.DisplayName.Trim();

        if (model.Nickname != null)
            tp.Nickname = model.Nickname.Trim();

        if (model.Email != null)
            tp.Email = model.Email.Trim();

        if (model.Phone != null)
            tp.Phone = model.Phone.Trim();

        if (model.Country != null)
            tp.Country = model.Country.Trim();

        if (model.City != null)
            tp.City = model.City.Trim();

        tp.SkillLevel = model.SkillLevel;
        tp.Seed = model.Seed;

        if (model.Status.HasValue)
            tp.Status = model.Status.Value;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeletePlayersResult?> DeleteTournamentPlayersAsync(
        int tournamentId,
        string ownerUserId,
        DeletePlayersModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot delete players after tournament has started or completed.");
        }

        var result = new DeletePlayersResult();

        if (model.PlayerIds.Count == 0) return result;

        var existingPlayers = await _db.TournamentPlayers
            .Where(x => x.TournamentId == tournamentId && model.PlayerIds.Contains(x.Id))
            .ToListAsync(ct);

        var existingIds = existingPlayers.Select(x => x.Id).ToHashSet();

        foreach (var requestedId in model.PlayerIds)
        {
            if (!existingIds.Contains(requestedId))
            {
                result.Failed.Add(new DeletePlayersResult.FailedItem
                {
                    PlayerId = requestedId,
                    Reason = "Player not found or doesn't belong to this tournament"
                });
            }
        }

        if (existingPlayers.Count > 0)
        {
            _db.TournamentPlayers.RemoveRange(existingPlayers);
            await _db.SaveChangesAsync(ct);

            result.DeletedCount = existingPlayers.Count;
            result.DeletedIds = existingPlayers.Select(x => x.Id).ToList();
        }

        return result;
    }
}
