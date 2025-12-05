using PoolMate.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Dtos.Response;
using System.Text.Json;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Payout;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class PayoutService : IPayoutService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(ApplicationDbContext db, ILogger<PayoutService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PayoutTemplateDto>> GetTemplatesAsync(string userId, CancellationToken ct = default)
    {
        // 1. Truy vấn Database (Lấy templates của user hiện tại)
        var entities = await _db.PayoutTemplates
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId) 
            .OrderBy(x => x.MinPlayers)
            .ThenBy(x => x.Places)
            .ToListAsync(ct);

        var result = new List<PayoutTemplateDto>();

        // 2. Duyệt và Map dữ liệu
        foreach (var entity in entities)
        {
            List<RankPercentDto> distribution;
            try
            {
                distribution = JsonSerializer.Deserialize<List<RankPercentDto>>(entity.PercentJson)
                               ?? new List<RankPercentDto>();
            }
            catch
            {
                distribution = new List<RankPercentDto>();
            }

            result.Add(new PayoutTemplateDto
            {
                Id = entity.Id,
                Name = entity.Name,
                MinPlayers = entity.MinPlayers,
                MaxPlayers = entity.MaxPlayers,
                Places = entity.Places,
                Distribution = distribution
            });
        }

        return result;
    }

    public async Task<PayoutTemplateDto?> GetTemplateByIdAsync(int id, string userId, CancellationToken ct = default)
    {
        var entity = await _db.PayoutTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct);
        if (entity == null) return null;

        List<RankPercentDto> distribution;
        try
        {
            distribution = JsonSerializer.Deserialize<List<RankPercentDto>>(entity.PercentJson)
                           ?? new List<RankPercentDto>();
        }
        catch
        {
            distribution = new List<RankPercentDto>();
        }

        return new PayoutTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            MinPlayers = entity.MinPlayers,
            MaxPlayers = entity.MaxPlayers,
            Places = entity.Places,
            Distribution = distribution
        };
    }

    public async Task<PayoutTemplateDto> CreateTemplateAsync(string userId, CreatePayoutTemplateDto dto,
        CancellationToken ct = default)
    {
        // 1. Validate Logic (Business Rules)
        // Rule A: Min < Max
        if (dto.MinPlayers > dto.MaxPlayers)
        {
            throw new InvalidOperationException(
                $"Min Players ({dto.MinPlayers}) cannot be greater than Max Players ({dto.MaxPlayers}).");
        }

        // Rule B: Tổng phần trăm phải là 100% (chấp nhận sai số nhỏ 0.01 do số thực)
        var totalPercent = dto.Distribution.Sum(x => x.Percent);
        if (Math.Abs(totalPercent - 100) > 0.01)
        {
            throw new InvalidOperationException(
                $"Total percentage must be exactly 100%. Current sum: {totalPercent}%.");
        }

        // Rule C: Kiểm tra trùng lặp khoảng (Optional - nâng cao)
        // 2. Map DTO -> Entity
        var entity = new PayoutTemplate
        {
            OwnerUserId = userId, // GÁN CHỦ SỞ HỮU
            Name = dto.Name.Trim(),
            MinPlayers = dto.MinPlayers,
            MaxPlayers = dto.MaxPlayers,
            Places = dto.Distribution.Count,
            PercentJson = JsonSerializer.Serialize(dto.Distribution)
        };
        // 3. Lưu vào Database
        _db.PayoutTemplates.Add(entity);
        await _db.SaveChangesAsync(ct);
        // 4. Map Entity -> DTO trả về
        return new PayoutTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            MinPlayers = entity.MinPlayers,
            MaxPlayers = entity.MaxPlayers,
            Places = entity.Places,
            Distribution = dto.Distribution
        };
    }

    public async Task<PayoutTemplateDto?> UpdateTemplateAsync(int id, string userId, CreatePayoutTemplateDto dto,
        CancellationToken ct = default)
    {
        // 1. Tìm Template trong DB và check ownership
        var entity = await _db.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct);
        if (entity == null) return null;
        // 2. Validate Logic (Business Rules) - Giống hệt hàm Create
        // Rule A: Min < Max
        if (dto.MinPlayers > dto.MaxPlayers)
        {
            throw new InvalidOperationException(
                $"Min Players ({dto.MinPlayers}) cannot be greater than Max Players ({dto.MaxPlayers}).");
        }

        // Rule B: Tổng phần trăm phải là 100%
        var totalPercent = dto.Distribution.Sum(x => x.Percent);
        if (Math.Abs(totalPercent - 100) > 0.01)
        {
            throw new InvalidOperationException(
                $"Total percentage must be exactly 100%. Current sum: {totalPercent}%.");
        }

        // 3. Cập nhật dữ liệu
        entity.Name = dto.Name.Trim();
        entity.MinPlayers = dto.MinPlayers;
        entity.MaxPlayers = dto.MaxPlayers;
        entity.Places = dto.Distribution.Count;
        entity.PercentJson = JsonSerializer.Serialize(dto.Distribution);

        await _db.SaveChangesAsync(ct);

        // 4. Lưu thay đổi
        await _db.SaveChangesAsync(ct);
        return new PayoutTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            MinPlayers = entity.MinPlayers,
            MaxPlayers = entity.MaxPlayers,
            Places = entity.Places,
            Distribution = dto.Distribution
        };
    }

    public async Task<Response> DeleteTemplateAsync(int id, string userId, CancellationToken ct = default)
    {
        var entity = await _db.PayoutTemplates.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct);
        if (entity == null)
        {
            return Response.Error("Payout template not found or you don't have permission to delete it");
        }
        var isInUse = await _db.Tournaments.AnyAsync(t => t.PayoutTemplateId == id, ct);
        if (isInUse)
        {
            return Response.Error(
                "Cannot delete this template because it is currently assigned to one or more tournaments. Please change the tournaments' settings first.");
        }
        _db.PayoutTemplates.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Response.Ok<object>(null, "Payout template deleted successfully");
    }

    public async Task<PayoutSimulationResultDto> SimulatePayoutAsync(
        PayoutSimulationRequestDto request,
        CancellationToken ct = default)
    {
        List<RankPercentDto> distribution;
        if (request.TemplateId.HasValue)
        {
            // Case A: Dùng Template có sẵn trong DB
            var template = await _db.PayoutTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TemplateId.Value, ct);

            if (template == null)
            {
                throw new InvalidOperationException($"Payout template with ID {request.TemplateId} not found.");
            }

            distribution = JsonSerializer.Deserialize<List<RankPercentDto>>(template.PercentJson)
                           ?? new List<RankPercentDto>();
        }
        else if (request.CustomDistribution != null && request.CustomDistribution.Any())
        {
            // Case B: Dùng công thức tùy chỉnh (Admin đang nhập thử)
            // Validate tổng % phải bằng 100
            var totalPercent = request.CustomDistribution.Sum(x => x.Percent);
            if (Math.Abs(totalPercent - 100) > 0.01)
            {
                throw new InvalidOperationException(
                    $"Total percentage must be exactly 100%. Current sum: {totalPercent}%.");
            }

            distribution = request.CustomDistribution;
        }
        else
        {
            throw new InvalidOperationException("Please provide either a TemplateId or a CustomDistribution.");
        }
        // 2. Khởi tạo kết quả
        var result = new PayoutSimulationResultDto
        {
            TotalPrize = request.TotalPrizePool,
            Breakdown = new List<PayoutBreakdownItemDto>()
        };

        if (request.TotalPrizePool <= 0 || !distribution.Any())
        {
            return result;
        }
        // 3. Tính tiền cho từng hạng
        decimal currentSum = 0;
        foreach (var item in distribution.OrderBy(x => x.Rank))
        {
            // Công thức: Tiền = Tổng quỹ * (Phần trăm / 100)
            var rawAmount = request.TotalPrizePool * (decimal)(item.Percent / 100.0);
            // Làm tròn đến 2 chữ số thập phân (quy tắc tiền tệ)
            var roundedAmount = Math.Round(rawAmount, 2, MidpointRounding.AwayFromZero);
            result.Breakdown.Add(new PayoutBreakdownItemDto
            {
                Rank = item.Rank,
                Percent = item.Percent,
                Amount = roundedAmount
            });

            currentSum += roundedAmount;
        }

        // 4. Xử lý số lẻ (Rounding Difference)
        if (currentSum != request.TotalPrizePool)
        {
            var diff = request.TotalPrizePool - currentSum;
            if (result.Breakdown.Count > 0)
            {
                result.Breakdown[0].Amount += diff;
            }
        }

        return result;
    }
}