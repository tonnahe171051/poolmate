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

    private static bool CanEditBracket(Tournament t)
        => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);
    // end helpers

    public async Task<int?> CreateAsync(string ownerUserId, CreateTournamentModel m, CancellationToken ct)
    {
        var isMulti = m.IsMultiStage ?? false;

        if (isMulti)
        {
            if (!m.AdvanceToStage2Count.HasValue || m.AdvanceToStage2Count <= 0)
                throw new InvalidOperationException("AdvanceToStage2Count is required for multi-stage tournaments.");

            var adv = m.AdvanceToStage2Count.Value;
            if (adv < 4)
                throw new InvalidOperationException(
                    "AdvanceToStage2Count must be at least 4 for multi-stage tournaments.");
            if ((adv & (adv - 1)) != 0)
                throw new InvalidOperationException("AdvanceToStage2Count must be a power of 2 (4,8,16,...)");
        }

        BracketType bracketType;
        BracketOrdering bracketOrdering;
        BracketOrdering stage2Ordering;

        if (isMulti)
        {
            // Multi-stage: Ưu tiên Stage1 fields
            bracketType = m.Stage1Type ?? m.BracketType ?? BracketType.DoubleElimination;
            bracketOrdering = m.Stage1Ordering ?? m.BracketOrdering ?? BracketOrdering.Random;
            stage2Ordering = m.Stage2Ordering ?? BracketOrdering.Random;

            // Validation: Single Elimination is not valid as Stage1 for multi-stage
            if (bracketType == BracketType.SingleElimination)
                throw new InvalidOperationException(
                    "Single Elimination is not compatible with multi-stage tournaments. Choose Double Elimination for Stage 1.");
        }
        else
        {
            // Single-stage: Chỉ dùng legacy fields, ignore Stage fields
            bracketType = m.BracketType ?? BracketType.DoubleElimination;
            bracketOrdering = m.BracketOrdering ?? BracketOrdering.Random;
            stage2Ordering = BracketOrdering.Random; // Default for single-stage
        }

        var t = new Tournament
        {
            Name = m.Name.Trim(),
            Description = m.Description,
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

            // MULTI-STAGE SETTINGS
            IsMultiStage = isMulti,
            AdvanceToStage2Count = isMulti ? m.AdvanceToStage2Count : null,
            Stage1Ordering = bracketOrdering, // Sync with BracketOrdering
            Stage2Ordering = stage2Ordering,

            // BRACKET SETTINGS
            BracketType = bracketType,
            BracketOrdering = bracketOrdering,
            WinnersRaceTo = m.WinnersRaceTo,
            LosersRaceTo = m.LosersRaceTo,
            FinalsRaceTo = m.FinalsRaceTo,
        };

        CalculateTotalPrize(t);

        _db.Tournaments.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task<bool> UpdateAsync(int id, string ownerUserId, UpdateTournamentModel m, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null || t.OwnerUserId != ownerUserId) return false;

        // 1. Update thông tin cơ bản
        if (!string.IsNullOrWhiteSpace(m.Name)) t.Name = m.Name.Trim();
        if (m.Description is not null) t.Description = m.Description;
        if (m.StartUtc.HasValue) t.StartUtc = m.StartUtc.Value;
        if (m.EndUtc.HasValue) t.EndUtc = m.EndUtc.Value;
        if (m.VenueId.HasValue) t.VenueId = m.VenueId.Value;
        if (m.IsPublic.HasValue) t.IsPublic = m.IsPublic.Value;
        if (m.OnlineRegistrationEnabled.HasValue) t.OnlineRegistrationEnabled = m.OnlineRegistrationEnabled.Value;

        // Update Game settings
        if (m.PlayerType.HasValue) t.PlayerType = m.PlayerType.Value;
        if (m.GameType.HasValue) t.GameType = m.GameType.Value;
        if (m.Rule.HasValue) t.Rule = m.Rule.Value;
        if (m.BreakFormat.HasValue) t.BreakFormat = m.BreakFormat.Value;

        // 2. Update thông tin phí (chưa tính toán)
        if (m.EntryFee.HasValue) t.EntryFee = m.EntryFee.Value;
        if (m.AdminFee.HasValue) t.AdminFee = m.AdminFee.Value;
        if (m.AddedMoney.HasValue) t.AddedMoney = m.AddedMoney.Value;
        if (m.PayoutMode.HasValue) t.PayoutMode = m.PayoutMode.Value;
        if (m.PayoutTemplateId.HasValue) t.PayoutTemplateId = m.PayoutTemplateId.Value;

        // Nếu user nhập TotalPrize mới (cho chế độ Custom)
        if (m.TotalPrize.HasValue)
            t.TotalPrize = Math.Max(0, m.TotalPrize.Value);

        // 3. Update cấu trúc giải (Bracket Settings)
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

            if (willBeMulti)
            {
                // Check single elimination conflict
                var effectiveStage1Type = m.Stage1Type ?? m.BracketType ?? t.BracketType;
                if (effectiveStage1Type == BracketType.SingleElimination)
                    throw new InvalidOperationException("Single Elimination cannot be used as Stage 1 for multi-stage tournaments.");

                // Validate Advance count
                if (m.AdvanceToStage2Count.HasValue)
                {
                    var adv = m.AdvanceToStage2Count.Value;
                    if (adv <= 0 || (adv & (adv - 1)) != 0)
                        throw new InvalidOperationException("AdvanceToStage2Count must be a power of 2 (4,8,16,...).");
                    if (adv < 4)
                        throw new InvalidOperationException("AdvanceToStage2Count must be at least 4.");
                }
            }

            t.IsMultiStage = willBeMulti;

            // Update BracketType
            if (willBeMulti)
            {
                if (m.Stage1Type.HasValue) t.BracketType = m.Stage1Type.Value;
                else if (m.BracketType.HasValue) t.BracketType = m.BracketType.Value;
            }
            else
            {
                if (m.BracketType.HasValue) t.BracketType = m.BracketType.Value;
            }

            // Update Ordering
            if (willBeMulti)
            {
                if (m.Stage1Ordering.HasValue)
                {
                    t.Stage1Ordering = m.Stage1Ordering.Value;
                    t.BracketOrdering = m.Stage1Ordering.Value;
                }
                else if (m.BracketOrdering.HasValue)
                {
                    t.Stage1Ordering = m.BracketOrdering.Value;
                    t.BracketOrdering = m.BracketOrdering.Value;
                }
            }
            else
            {
                if (m.BracketOrdering.HasValue)
                {
                    t.BracketOrdering = m.BracketOrdering.Value;
                    t.Stage1Ordering = m.BracketOrdering.Value;
                }
            }

            // Update Stage 2 settings
            if (willBeMulti)
            {
                if (m.AdvanceToStage2Count.HasValue) t.AdvanceToStage2Count = m.AdvanceToStage2Count.Value;
                else if (t.AdvanceToStage2Count.HasValue && t.AdvanceToStage2Count.Value < 4)
                    throw new InvalidOperationException("AdvanceToStage2Count must be at least 4 for multi-stage tournaments.");

                if (m.Stage2Ordering.HasValue) t.Stage2Ordering = m.Stage2Ordering.Value;
            }
            else
            {
                t.AdvanceToStage2Count = null;
                t.Stage2Ordering = BracketOrdering.Random;
            }

            // Update Race To
            if (m.WinnersRaceTo.HasValue) t.WinnersRaceTo = m.WinnersRaceTo.Value;
            if (m.LosersRaceTo.HasValue) t.LosersRaceTo = m.LosersRaceTo.Value;
            if (m.FinalsRaceTo.HasValue) t.FinalsRaceTo = m.FinalsRaceTo.Value;
        }

        // 4. ✅ TÍNH TOÁN LẠI TIỀN (Đặt ở cuối cùng)
        CalculateTotalPrize(t);

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

        var bracketExists = await _db.Matches.AnyAsync(m => m.TournamentId == id, ct);
        if (!bracketExists)
            throw new InvalidOperationException("Cannot start the tournament before a bracket is created.");

        var unconfirmedPlayers = await _db.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == id && tp.Status != TournamentPlayerStatus.Confirmed, ct);
        if (unconfirmedPlayers)
            throw new InvalidOperationException("All players must be confirmed before starting the tournament.");

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
                Venue = x.Venue == null
                    ? null
                    : new VenueDto
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

    public async Task<List<PayoutTemplateDto>> GetPayoutTemplatesAsync(string userId, CancellationToken ct)
    {
        var templates = await _db.PayoutTemplates
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
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
                Venue = x.Venue == null
                    ? null
                    : new VenueDto
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

                // ✅ MULTI-STAGE SETTINGS - MISSING!
                IsMultiStage = x.IsMultiStage,
                AdvanceToStage2Count = x.AdvanceToStage2Count,
                Stage1Ordering = x.Stage1Ordering,
                Stage2Ordering = x.Stage2Ordering,

                // Payout settings
                EntryFee = x.EntryFee,
                AdminFee = x.AdminFee,
                AddedMoney = x.AddedMoney,
                PayoutMode = x.PayoutMode,
                PayoutTemplateId = x.PayoutTemplateId,
                TotalPrize = x.TotalPrize,

                FlyerUrl = x.FlyerUrl,

                OwnerUserId = x.OwnerUserId,
                CreatorName = string.IsNullOrWhiteSpace(x.OwnerUser.FirstName) && string.IsNullOrWhiteSpace(x.OwnerUser.LastName)
                    ? x.OwnerUser.UserName!
                    : ((x.OwnerUser.FirstName ?? "").Trim() + " " + (x.OwnerUser.LastName ?? "").Trim()).Trim(),

                Venue = x.Venue == null
                    ? null
                    : new VenueDto
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
            throw new InvalidOperationException(
                "Tournament can only be deleted before it starts or after it's completed.");
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

    private void CalculateTotalPrize(Tournament t)
    {
        // Nếu là chế độ Template -> Hệ thống TỰ TÍNH
        if (t.PayoutMode == PayoutMode.Template)
        {
            var players = t.BracketSizeEstimate ?? 0;
            var entry = t.EntryFee ?? 0;
            var admin = t.AdminFee ?? 0;
            var added = t.AddedMoney ?? 0;

            // Công thức: (Số người * Phí tham dự) + Tiền tài trợ - (Số người * Phí Admin)
            var total = (players * entry) + added - (players * admin);

            // Đảm bảo không âm
            t.TotalPrize = Math.Max(0, total);
        }
        // Nếu là chế độ Custom -> Giữ nguyên số tiền user nhập (t.TotalPrize), chỉ đảm bảo không âm
        else if (t.PayoutMode == PayoutMode.Custom)
        {
            t.TotalPrize = Math.Max(0, t.TotalPrize ?? 0);
        }
    }
}