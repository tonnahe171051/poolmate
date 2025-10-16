using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class TournamentService : ITournamentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICloudinaryService _cloud;

    public TournamentService(ApplicationDbContext db, ICloudinaryService cloud)
    {
        _db = db;
        _cloud = cloud;
    }

    // helpers
    private static decimal Z(decimal? v) => v ?? 0m;
    private static decimal ComputeTotal(int players, decimal? entry, decimal? admin, decimal? added)
    {
        var total = players * Z(entry) + Z(added) - players * Z(admin);
        return Math.Round(Math.Max(0, total), 2, MidpointRounding.AwayFromZero);
    }
    private static void ApplyPayout(Tournament t)
    {
        if (t.PayoutMode == PayoutMode.Custom)
        {
            t.TotalPrize = Math.Max(0, t.TotalPrize ?? 0m);
            return;
        }
        var players = t.BracketSizeEstimate ?? 0;
        t.TotalPrize = ComputeTotal(players, t.EntryFee, t.AdminFee, t.AddedMoney);
    }

    private async Task<(bool CanAdd, int CurrentCount, int? MaxLimit)> CanAddPlayersAsync(
        int tournamentId, int playersToAdd, CancellationToken ct)
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

    private static bool CanEditBracket(Tournament t)
    => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);

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

        if (existingPlayer != null)
        {
            throw new InvalidOperationException($"Seed {seed.Value} is already assigned to player '{existingPlayer.DisplayName}' in this tournament.");
        }
    }
    // end helpers

    public async Task<int?> CreateAsync(string ownerUserId, CreateTournamentModel m, CancellationToken ct)
    {
        var isMulti = m.IsMultiStage ?? false;

        if (isMulti)
        {
            if (!m.AdvanceToStage2Count.HasValue || m.AdvanceToStage2Count <= 0)
                throw new InvalidOperationException("AdvanceToStage2Count is required for multi-stage tournaments.");

            var adv = m.AdvanceToStage2Count.Value;
            if ((adv & (adv - 1)) != 0)
                throw new InvalidOperationException("AdvanceToStage2Count must be a power of 2 (2,4,8,16,...)");
        }
        else
        {
            if (m.AdvanceToStage2Count.HasValue || m.Stage2Ordering.HasValue)
            {
                throw new InvalidOperationException("Cannot set Stage2 settings for single-stage tournaments.");
            }
        }

        var stage1Type = m.Stage1Type ?? m.BracketType ?? BracketType.DoubleElimination;
        var stage1Ordering = m.Stage1Ordering ?? m.BracketOrdering ?? BracketOrdering.Random;
        var stage2Ordering = isMulti ? (m.Stage2Ordering ?? BracketOrdering.Random) : BracketOrdering.Random;

        var t = new Tournament
        {
            Name = m.Name.Trim(),
            StartUtc = m.StartUtc,
            EndUtc = m.EndUtc,
            VenueId = m.VenueId,
            OwnerUserId = ownerUserId,
            Status = TournamentStatus.Upcoming,
            IsPublic = m.IsPublic,
            OnlineRegistrationEnabled = m.OnlineRegistrationEnabled,
            BracketSizeEstimate = m.BracketSizeEstimate,

            EntryFee = m.EntryFee,
            AdminFee = m.AdminFee,
            AddedMoney = m.AddedMoney,

            PayoutMode = m.PayoutMode ?? PayoutMode.Template,
            PayoutTemplateId = m.PayoutTemplateId,
            TotalPrize = m.TotalPrize,

            PlayerType = m.PlayerType ?? PlayerType.Singles,
            GameType = m.GameType ?? GameType.NineBall,
            Rule = m.Rule ?? Rule.WNT,
            BreakFormat = m.BreakFormat ?? BreakFormat.WinnerBreak,

            // multi-stage settings
            IsMultiStage = isMulti,
            AdvanceToStage2Count = isMulti ? m.AdvanceToStage2Count : null,
            Stage1Ordering = stage1Ordering,
            Stage2Ordering = stage2Ordering,

            // bracket settings
            BracketType = stage1Type,
            BracketOrdering = stage1Ordering,
            WinnersRaceTo = m.WinnersRaceTo,
            LosersRaceTo = m.LosersRaceTo,
            FinalsRaceTo = m.FinalsRaceTo,
        };

        ApplyPayout(t);

        _db.Tournaments.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task<bool> UpdateAsync(int id, string ownerUserId, UpdateTournamentModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        if (!string.IsNullOrWhiteSpace(m.Name)) t.Name = m.Name.Trim();
        if (m.Description is not null) t.Description = m.Description;
        if (m.StartUtc.HasValue) t.StartUtc = m.StartUtc.Value;
        if (m.EndUtc.HasValue) t.EndUtc = m.EndUtc.Value;
        if (m.VenueId.HasValue) t.VenueId = m.VenueId.Value;
        if (m.IsPublic.HasValue) t.IsPublic = m.IsPublic.Value;
        if (m.OnlineRegistrationEnabled.HasValue) t.OnlineRegistrationEnabled = m.OnlineRegistrationEnabled.Value;

        // Game settings
        if (m.PlayerType.HasValue) t.PlayerType = m.PlayerType.Value;
        if (m.GameType.HasValue) t.GameType = m.GameType.Value;
        if (m.Rule.HasValue) t.Rule = m.Rule.Value;
        if (m.BreakFormat.HasValue) t.BreakFormat = m.BreakFormat.Value;

        if (m.EntryFee.HasValue) t.EntryFee = m.EntryFee.Value;
        if (m.AdminFee.HasValue) t.AdminFee = m.AdminFee.Value;
        if (m.AddedMoney.HasValue) t.AddedMoney = m.AddedMoney.Value;
        if (m.PayoutMode.HasValue) t.PayoutMode = m.PayoutMode.Value;
        if (m.PayoutTemplateId.HasValue) t.PayoutTemplateId = m.PayoutTemplateId.Value;
        if (t.PayoutMode == PayoutMode.Custom && m.TotalPrize.HasValue)
            t.TotalPrize = Math.Max(0, m.TotalPrize.Value);
        if (t.PayoutMode == PayoutMode.Template)
            ApplyPayout(t);

        if (CanEditBracket(t))
        {
            if (m.BracketSizeEstimate.HasValue)
            {
                var currentPlayers = await _db.TournamentPlayers.CountAsync(x => x.TournamentId == id, ct);
                if (m.BracketSizeEstimate.Value < currentPlayers)
                    throw new InvalidOperationException($"Cannot reduce bracket size below current player count ({currentPlayers}).");
                t.BracketSizeEstimate = m.BracketSizeEstimate.Value;
            }

            var willBeMulti = m.IsMultiStage ?? t.IsMultiStage;
            t.IsMultiStage = willBeMulti;

            if (m.Stage1Type.HasValue) t.BracketType = m.Stage1Type.Value;
            if (m.Stage1Ordering.HasValue)
            {
                t.Stage1Ordering = m.Stage1Ordering.Value;
                t.BracketOrdering = m.Stage1Ordering.Value;
            }

            if (willBeMulti)
            {
                if (m.AdvanceToStage2Count.HasValue)
                {
                    var adv = m.AdvanceToStage2Count.Value;
                    if (adv <= 0 || (adv & (adv - 1)) != 0)
                        throw new InvalidOperationException("AdvanceToStage2Count must be a power of 2.");
                    t.AdvanceToStage2Count = adv;
                }
                if (m.Stage2Ordering.HasValue) t.Stage2Ordering = m.Stage2Ordering.Value;
            }
            else
            {
                t.AdvanceToStage2Count = null;
            }

            if (m.WinnersRaceTo.HasValue) t.WinnersRaceTo = m.WinnersRaceTo.Value;
            if (m.LosersRaceTo.HasValue) t.LosersRaceTo = m.LosersRaceTo.Value;
            if (m.FinalsRaceTo.HasValue) t.FinalsRaceTo = m.FinalsRaceTo.Value;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }


    public async Task<bool> UpdateFlyerAsync(int id, string ownerUserId, UpdateFlyerModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        if (!string.IsNullOrWhiteSpace(m.FlyerUrl) && !string.IsNullOrWhiteSpace(m.FlyerPublicId))
        {
            if (!string.IsNullOrEmpty(t.FlyerPublicId) &&
                t.FlyerPublicId != m.FlyerPublicId)
            {
                await _cloud.DeleteAsync(t.FlyerPublicId);
            }

            t.FlyerUrl = m.FlyerUrl;
            t.FlyerPublicId = m.FlyerPublicId;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> StartAsync(int id, string ownerUserId, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;
        if (t.IsStarted) return true;

        t.IsStarted = true;
        t.Status = TournamentStatus.InProgress;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EndAsync(int id, string ownerUserId, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        t.Status = TournamentStatus.Completed;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagingList<UserTournamentListDto>> GetTournamentsByUserAsync(
        string ownerUserId,
        string? searchName = null,
        TournamentStatus? status = null,
        int pageIndex = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _db.Tournaments
            .Include(x => x.Venue)
            .Where(x => x.OwnerUserId == ownerUserId);

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            var trimmedSearch = searchName.Trim();
            query = query.Where(x => x.Name.Contains(trimmedSearch));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalRecords = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserTournamentListDto
            {
                Id = x.Id,
                Name = x.Name,
                CreatedAt = x.CreatedAt,
                Status = x.Status,
                TotalPlayers = x.TournamentPlayers.Count,
                GameType = x.GameType,
                BracketType = x.BracketType,
                Venue = x.Venue == null ? null : new VenueDto
                {
                    Id = x.Venue.Id,
                    Name = x.Venue.Name,
                    Address = x.Venue.Address,
                    City = x.Venue.City
                }
            })
            .ToListAsync(ct);

        return PagingList<UserTournamentListDto>.Create(items, totalRecords, pageIndex, pageSize);
    }

    public async Task<PayoutPreviewResponse> PreviewPayoutAsync(PreviewPayoutRequest m, CancellationToken ct)
    {
        var resp = new PayoutPreviewResponse { Players = Math.Max(0, m.Players) };

        // Custom
        if (m.IsCustom)
        {
            resp.Total = Math.Max(0, m.TotalPrizeWhenCustom ?? 0m);
            return resp;
        }

        //Template
        var total = ComputeTotal(m.Players, m.EntryFee, m.AdminFee, m.AddedMoney);
        resp.Total = total;

        if (!m.PayoutTemplateId.HasValue) return resp;

        var tpl = await _db.PayoutTemplates.AsNoTracking()
                   .FirstOrDefaultAsync(x => x.Id == m.PayoutTemplateId.Value, ct);
        if (tpl is null) return resp;

        var items = JsonSerializer.Deserialize<List<RankPercent>>(tpl.PercentJson) ?? new();

        foreach (var i in items)
        {
            resp.Breakdown.Add(new PayoutPreviewResponse.BreakdownItem
            {
                Rank = i.rank,
                Percent = i.percent,
                Amount = Math.Round(total * (i.percent / 100m), 2, MidpointRounding.AwayFromZero)
            });
        }

        // lam tron
        var sum = resp.Breakdown.Sum(x => x.Amount);
        if (resp.Breakdown.Count > 0 && sum != total)
        {
            var diff = total - sum;
            resp.Breakdown[0].Amount += diff;
        }

        return resp;
    }

    public async Task<List<PayoutTemplateDto>> GetPayoutTemplatesAsync(CancellationToken ct)
    {
        var templates = await _db.PayoutTemplates
            .AsNoTracking()
            .OrderBy(x => x.MinPlayers)
            .ThenBy(x => x.Places)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.MinPlayers,
                x.MaxPlayers,
                x.Places,
                x.PercentJson
            })
            .ToListAsync(ct);

        var result = new List<PayoutTemplateDto>(templates.Count);

        foreach (var t in templates)
        {
            var percents = JsonSerializer.Deserialize<List<RankPercent>>(t.PercentJson) ?? new();
            result.Add(new PayoutTemplateDto(
                t.Id, t.Name, t.MinPlayers, t.MaxPlayers, t.Places, percents));
        }

        return result;
    }

    public async Task<PagingList<TournamentListDto>> GetTournamentsAsync(
         string? searchName = null,
         TournamentStatus? status = null,
         GameType? gameType = null,
         int pageIndex = 1,
         int pageSize = 10,
         CancellationToken ct = default)
    {
        var query = _db.Tournaments
            .Include(x => x.Venue)
            .Where(x => x.IsPublic);

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            var trimmedSearch = searchName.Trim();
            query = query.Where(x => x.Name.Contains(trimmedSearch));
        }

        // Filter by tournament status
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (gameType.HasValue)
            query = query.Where(x => x.GameType == gameType.Value);

        var totalRecords = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.StartUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TournamentListDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                StartUtc = x.StartUtc,
                FlyerUrl = x.FlyerUrl,
                GameType = x.GameType,
                BracketSizeEstimate = x.BracketSizeEstimate,
                WinnersRaceTo = x.WinnersRaceTo,
                EntryFee = x.EntryFee,
                Venue = x.Venue == null ? null : new VenueDto
                {
                    Id = x.Venue.Id,
                    Name = x.Venue.Name,
                    Address = x.Venue.Address,
                    City = x.Venue.City
                }
            })
            .ToListAsync(ct);

        return PagingList<TournamentListDto>.Create(items, totalRecords, pageIndex, pageSize);
    }

    public async Task<TournamentPlayer?> AddTournamentPlayerAsync(
        int tournamentId, string ownerUserId, AddTournamentPlayerModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(t))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, 1, ct);
        if (!canAdd)
        {
            throw new InvalidOperationException(
                $"Cannot add player. Tournament is full ({currentCount}/{maxLimit}).");
        }

        if (m.PlayerId.HasValue)
        {
            var exists = await _db.TournamentPlayers
                .AnyAsync(x => x.TournamentId == tournamentId && x.PlayerId == m.PlayerId, ct);
            if (exists) throw new InvalidOperationException("This player is already in the tournament.");
        }

        await ValidateSeedAsync(tournamentId, m.Seed, null, ct);

        var tp = new TournamentPlayer
        {
            TournamentId = tournamentId,
            PlayerId = m.PlayerId,
            DisplayName = m.DisplayName.Trim(),
            Nickname = m.Nickname,
            Email = m.Email,
            Phone = m.Phone,
            City = m.City,
            Country = m.Country,
            SkillLevel = m.SkillLevel,
            Seed = m.Seed,
            Status = TournamentPlayerStatus.Confirmed
        };

        _db.TournamentPlayers.Add(tp);
        await _db.SaveChangesAsync(ct);
        return tp;
    }

    public async Task<BulkAddPlayersResult> BulkAddPlayersPerLineAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayersPerLineModel m,
        CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (t is null) throw new InvalidOperationException("Tournament not found.");
        if (t.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException();

        if (!CanEditBracket(t))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var lines = (m.Lines ?? string.Empty)
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
                name = name.Substring(0, 200);
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
        int tournamentId, int tpId, string ownerUserId,
        LinkPlayerRequest m, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        var player = await _db.Set<Player>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.PlayerId, ct);
        if (player is null) return false;

        tp.PlayerId = player.Id;

        if (m.OverwriteSnapshot)
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
        int tournamentId, int tpId, string ownerUserId, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        tp.PlayerId = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    //create + link
    public async Task<int?> CreateProfileFromSnapshotAndLinkAsync(
        int tournamentId, int tpId, string ownerUserId,
        CreateProfileFromSnapshotRequest m, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return null;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return null;

        // tạo Player mới từ snapshot
        var p = new Player
        {
            FullName = tp.DisplayName,
            Email = tp.Email,
            Phone = tp.Phone,
            Country = tp.Country,
            City = tp.City,
            SkillLevel = tp.SkillLevel
        };

        _db.Set<Player>().Add(p);
        await _db.SaveChangesAsync(ct);

        tp.PlayerId = p.Id;

        if (m.CopyBackToSnapshot)
        {
            tp.DisplayName = p.FullName;
            tp.Email = p.Email;
            tp.Phone = p.Phone;
            tp.Country = p.Country;
            tp.City = p.City;
            tp.SkillLevel = p.SkillLevel;
        }

        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task<List<TournamentPlayerListDto>> GetTournamentPlayersAsync(
    int tournamentId,
    string? searchName = null,
    CancellationToken ct = default)
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
    UpdateTournamentPlayerModel m,
    CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers
            .FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        if (!CanEditBracket(t))
        {
            throw new InvalidOperationException("Cannot modify players after tournament has started or completed.");
        }

        if (m.Seed != tp.Seed)
        {
            await ValidateSeedAsync(tournamentId, m.Seed, tpId, ct);
        }

        if (!string.IsNullOrWhiteSpace(m.DisplayName))
            tp.DisplayName = m.DisplayName.Trim();

        if (m.Nickname != null)
            tp.Nickname = m.Nickname.Trim();

        if (m.Email != null)
            tp.Email = m.Email.Trim();

        if (m.Phone != null)
            tp.Phone = m.Phone.Trim();

        if (m.Country != null)
            tp.Country = m.Country.Trim();

        if (m.City != null)
            tp.City = m.City.Trim();

        tp.SkillLevel = m.SkillLevel;

        tp.Seed = m.Seed;

        if (m.Status.HasValue)
            tp.Status = m.Status.Value;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TournamentTable?> AddTournamentTableAsync(
        int tournamentId, string ownerUserId, AddTournamentTableModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return null;

        var table = new TournamentTable
        {
            TournamentId = tournamentId,
            Label = m.Label.Trim(),
            Manufacturer = m.Manufacturer?.Trim(),
            SizeFoot = m.SizeFoot,
            LiveStreamUrl = m.LiveStreamUrl?.Trim(),
            Status = TableStatus.Open,
            IsStreaming = false
        };

        _db.TournamentTables.Add(table);
        await _db.SaveChangesAsync(ct);
        return table;
    }

    public async Task<BulkAddTablesResult> AddMultipleTournamentTablesAsync(
        int tournamentId, string ownerUserId, AddMultipleTournamentTablesModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        var result = new BulkAddTablesResult();
        var toAdd = new List<TournamentTable>();

        for (int i = m.StartNumber; i <= m.EndNumber; i++)
        {
            toAdd.Add(new TournamentTable
            {
                TournamentId = tournamentId,
                Label = $"Table {i}",
                Manufacturer = m.Manufacturer?.Trim(),
                SizeFoot = m.SizeFoot,
                Status = TableStatus.Open,
                IsStreaming = false
            });
        }

        if (toAdd.Count == 0) return result;

        _db.TournamentTables.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        // Result
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
    int tournamentId, int tableId, string ownerUserId, UpdateTournamentTableModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        var table = await _db.TournamentTables
            .FirstOrDefaultAsync(x => x.Id == tableId && x.TournamentId == tournamentId, ct);
        if (table is null) return false;

        // Update fields
        if (!string.IsNullOrWhiteSpace(m.Label))
            table.Label = m.Label.Trim();

        if (m.Manufacturer != null)
            table.Manufacturer = m.Manufacturer.Trim();

        if (m.SizeFoot.HasValue)
            table.SizeFoot = m.SizeFoot.Value;

        if (m.Status.HasValue)
            table.Status = m.Status.Value;

        if (m.IsStreaming.HasValue)
            table.IsStreaming = m.IsStreaming.Value;

        if (m.LiveStreamUrl != null)
            table.LiveStreamUrl = m.LiveStreamUrl.Trim();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeleteTablesResult?> DeleteTournamentTablesAsync(
        int tournamentId, string ownerUserId, DeleteTablesModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(t))
        {
            throw new InvalidOperationException("Cannot delete tables after tournament has started or completed.");
        }

        var result = new DeleteTablesResult();

        if (m.TableIds.Count == 0) return result;

        // Get existing tables 
        var existingTables = await _db.TournamentTables
            .Where(x => x.TournamentId == tournamentId && m.TableIds.Contains(x.Id))
            .ToListAsync(ct);

        var existingIds = existingTables.Select(x => x.Id).ToHashSet();

        // Track not found table
        foreach (var requestedId in m.TableIds)
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
        int tournamentId, CancellationToken ct = default)
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

    public async Task<DeletePlayersResult?> DeleteTournamentPlayersAsync(
        int tournamentId, string ownerUserId, DeletePlayersModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(t))
        {
            throw new InvalidOperationException("Cannot delete players after tournament has started or completed.");
        }

        var result = new DeletePlayersResult();

        if (m.PlayerIds.Count == 0) return result;

        // Get existing tournament players
        var existingPlayers = await _db.TournamentPlayers
            .Where(x => x.TournamentId == tournamentId && m.PlayerIds.Contains(x.Id))
            .ToListAsync(ct);

        var existingIds = existingPlayers.Select(x => x.Id).ToHashSet();

        // Track not found players
        foreach (var requestedId in m.PlayerIds)
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
    public async Task<TournamentDetailDto?> GetTournamentDetailAsync(int id, CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .Include(x => x.Venue)
            .Include(x => x.OwnerUser)
            .Where(x => x.Id == id)
            .Select(x => new TournamentDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,

                // Tournament settings
                IsPublic = x.IsPublic,
                OnlineRegistrationEnabled = x.OnlineRegistrationEnabled,
                IsStarted = x.IsStarted,
                Status = x.Status,

                // Game settings
                PlayerType = x.PlayerType,
                BracketType = x.BracketType,
                GameType = x.GameType,
                BracketOrdering = x.BracketOrdering,
                BracketSizeEstimate = x.BracketSizeEstimate,
                WinnersRaceTo = x.WinnersRaceTo,
                LosersRaceTo = x.LosersRaceTo,
                FinalsRaceTo = x.FinalsRaceTo,
                Rule = x.Rule,
                BreakFormat = x.BreakFormat,

                EntryFee = x.EntryFee,
                AdminFee = x.AdminFee,
                AddedMoney = x.AddedMoney,
                PayoutMode = x.PayoutMode,
                PayoutTemplateId = x.PayoutTemplateId,
                TotalPrize = x.TotalPrize,

                FlyerUrl = x.FlyerUrl,

                CreatorName = (x.OwnerUser.FirstName + " " + x.OwnerUser.LastName) ?? x.OwnerUser.UserName!,
                Venue = x.Venue == null ? null : new VenueDto
                {
                    Id = x.Venue.Id,
                    Name = x.Venue.Name,
                    Address = x.Venue.Address,
                    City = x.Venue.City
                },

                TotalPlayers = x.TournamentPlayers.Count,
                TotalTables = x.Tables.Count
            })
            .FirstOrDefaultAsync(ct);

        return tournament;
    }
    public async Task<bool> DeleteTournamentAsync(int id, string ownerUserId, CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tournament is null || tournament.OwnerUserId != ownerUserId)
            return false;

        var canDelete = tournament.Status == TournamentStatus.Upcoming ||
                        tournament.Status == TournamentStatus.Completed;

        if (!canDelete)
        {
            throw new InvalidOperationException("Tournament can only be deleted before it starts or after it's completed.");
        }

        var deleteMatchesTask = _db.Matches
            .Where(x => x.TournamentId == id)
            .ExecuteDeleteAsync(ct);

        // handle parallel flyer deletion
        Task? deleteFlyerTask = null;
        if (!string.IsNullOrEmpty(tournament.FlyerPublicId))
        {
            deleteFlyerTask = _cloud.DeleteAsync(tournament.FlyerPublicId);
        }

        await deleteMatchesTask;
        if (deleteFlyerTask != null)
        {
            await deleteFlyerTask;
        }

        _db.Tournaments.Remove(tournament);
        await _db.SaveChangesAsync(ct);

        return true;
    }



}
