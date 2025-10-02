using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

    public async Task<int?> CreateAsync(string ownerUserId, CreateTournamentModel m, CancellationToken ct)
    {
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

            // fees
            EntryFee = m.EntryFee,
            AdminFee = m.AdminFee,
            AddedMoney = m.AddedMoney,

            // payout
            PayoutMode = m.PayoutMode ?? PayoutMode.Template,
            PayoutTemplateId = m.PayoutTemplateId,
            TotalPrize = m.TotalPrize, // Custom

            // settings (enum + rule)
            PlayerType = m.PlayerType ?? PlayerType.Singles,
            BracketType = m.BracketType ?? BracketType.DoubleElimination,
            GameType = m.GameType ?? GameType.NineBall,
            BracketOrdering = m.BracketOrdering ?? BracketOrdering.Random,
            WinnersRaceTo = m.WinnersRaceTo,
            LosersRaceTo = m.LosersRaceTo,
            FinalsRaceTo = m.FinalsRaceTo,
            Rule = m.Rule ?? Rule.WNT,
            BreakFormat = m.BreakFormat ?? BreakFormat.WinnerBreak
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
        if (m.Description != null) t.Description = m.Description;
        if (m.StartUtc.HasValue) t.StartUtc = m.StartUtc.Value;
        if (m.EndUtc.HasValue) t.EndUtc = m.EndUtc.Value;
        if (m.VenueId.HasValue) t.VenueId = m.VenueId.Value;

        if (m.IsPublic.HasValue) t.IsPublic = m.IsPublic.Value;
        if (m.OnlineRegistrationEnabled.HasValue)
            t.OnlineRegistrationEnabled = m.OnlineRegistrationEnabled.Value;

        if (m.BracketSizeEstimate.HasValue) t.BracketSizeEstimate = m.BracketSizeEstimate.Value;

        // ------- settings (enum + rule) -------
        if (m.PlayerType.HasValue) t.PlayerType = m.PlayerType.Value;
        if (m.BracketType.HasValue) t.BracketType = m.BracketType.Value;
        if (m.GameType.HasValue) t.GameType = m.GameType.Value;
        if (m.BracketOrdering.HasValue) t.BracketOrdering = m.BracketOrdering.Value;

        if (m.WinnersRaceTo.HasValue) t.WinnersRaceTo = m.WinnersRaceTo.Value;
        if (m.LosersRaceTo.HasValue) t.LosersRaceTo = m.LosersRaceTo.Value;
        if (m.FinalsRaceTo.HasValue) t.FinalsRaceTo = m.FinalsRaceTo.Value;

        if (m.Rule.HasValue) t.Rule = m.Rule.Value;
        if (m.BreakFormat.HasValue) t.BreakFormat = m.BreakFormat.Value;

        // ------- fees -------
        if (m.EntryFee.HasValue) t.EntryFee = m.EntryFee.Value;
        if (m.AdminFee.HasValue) t.AdminFee = m.AdminFee.Value;
        if (m.AddedMoney.HasValue) t.AddedMoney = m.AddedMoney.Value;

        // ------- payout mode/template -------
        if (m.PayoutMode.HasValue) t.PayoutMode = m.PayoutMode.Value;
        if (m.PayoutTemplateId.HasValue) t.PayoutTemplateId = m.PayoutTemplateId.Value;

        // Nếu đang Custom và FE muốn sửa tổng thưởng, cho phép
        if (t.PayoutMode == PayoutMode.Custom && m.TotalPrize.HasValue)
            t.TotalPrize = Math.Max(0, m.TotalPrize.Value);

        // Với Template (hoặc chuyển từ Custom → Template), luôn recalc & lưu
        if (t.PayoutMode == PayoutMode.Template)
            ApplyPayout(t);

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

    public Task<Tournament?> GetAsync(int id, CancellationToken ct)
        => _db.Tournaments
             .Include(x => x.Venue)
             .Include(x => x.PayoutTemplate)
             .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PayoutPreviewResponse> PreviewPayoutAsync(PreviewPayoutRequest m, CancellationToken ct)
    {
        var resp = new PayoutPreviewResponse { Players = Math.Max(0, m.Players) };

        // Custom
        if (m.IsCustom)
        {
            resp.Total = Math.Max(0, m.TotalPrizeWhenCustom ?? 0m);
            return resp;
        }

        //Template: tính total
        var total = ComputeTotal(m.Players, m.EntryFee, m.AdminFee, m.AddedMoney);
        resp.Total = total;

        // Nếu không có templateId thì chỉ trả total
        if (!m.PayoutTemplateId.HasValue) return resp;

        //Lấy template và parse %
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

        // Bù sai số do round (nếu có) 
        var sum = resp.Breakdown.Sum(x => x.Amount);
        if (resp.Breakdown.Count > 0 && sum != total)
        {
            var diff = total - sum;
            resp.Breakdown[0].Amount += diff;
        }

        return resp;
    }
}
