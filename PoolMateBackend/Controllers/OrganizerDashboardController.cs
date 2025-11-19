using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/organizer/dashboard")]
[Authorize] 
public class OrganizerDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrganizerDashboardController> _logger;

    public OrganizerDashboardController(
        ApplicationDbContext db,
        ILogger<OrganizerDashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }


    /// API 1: Lấy Số Liệu Tổng Quan (KPI Stats)
    [HttpGet("stats")]
    public async Task<ActionResult<OrganizerDashboardStatsDto>> GetStats(CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            var userTournaments = _db.Tournaments.Where(t => t.OwnerUserId == userId);

            // 1. Số giải đang InProgress
            var activeTournaments = await userTournaments
                .Where(t => t.Status == TournamentStatus.InProgress)
                .CountAsync(ct);

            // 2. Số giải Upcoming
            var upcomingTournaments = await userTournaments
                .Where(t => t.Status == TournamentStatus.Upcoming)
                .CountAsync(ct);

            // 3. Tổng số VĐV tham gia 
            var totalParticipants = await _db.TournamentPlayers
                .Where(tp => tp.Tournament.OwnerUserId == userId)
                .CountAsync(ct);

            // 4. Lấy dữ liệu tài chính từ tất cả tournaments (InProgress + Upcoming)
            var financialData = await (
                from t in _db.Tournaments
                where t.OwnerUserId == userId
                    && (t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Upcoming)
                select new
                {
                    t.Id,
                    EntryFee = t.EntryFee ?? 0,
                    AdminFee = t.AdminFee ?? 0,
                    AddedMoney = t.AddedMoney ?? 0,
                    ConfirmedCount = _db.TournamentPlayers
                        .Count(tp => tp.TournamentId == t.Id 
                            && tp.Status == TournamentPlayerStatus.Confirmed)
                }
            ).ToListAsync(ct);

            // 5. Tính TotalRevenue (Gross Revenue): Σ[(EntryFee + AdminFee) × Confirmed]
            var totalRevenue = financialData.Sum(x => (x.EntryFee + x.AdminFee) * x.ConfirmedCount);

            // 6. Tính NetProfit: Σ[(AdminFee × Confirmed) - AddedMoney]
            var netProfit = financialData.Sum(x => (x.AdminFee * x.ConfirmedCount) - x.AddedMoney);

            var stats = new OrganizerDashboardStatsDto
            {
                ActiveTournaments = activeTournaments,
                UpcomingTournaments = upcomingTournaments,
                TotalParticipants = totalParticipants,
                TotalRevenue = totalRevenue,
                NetProfit = netProfit,
                Timestamp = DateTime.UtcNow
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizer dashboard stats for user {UserId}", 
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
        }
    }
    
    /// API 2: Lịch sử Hoạt động Gần đây (Recent Activity)
    [HttpGet("activities")]
    public async Task<ActionResult<List<OrganizerActivityDto>>> GetRecentActivities(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var activities = new List<OrganizerActivityDto>();

            // Lấy các sự kiện gần đây (trong 30 ngày)
            var since = DateTime.UtcNow.AddDays(-30);

            // 1. VĐV mới đăng ký
            var recentRegistrations = await _db.TournamentPlayers
                .Where(tp => tp.Tournament.OwnerUserId == userId 
                    && tp.Tournament.CreatedAt >= since)
                .OrderByDescending(tp => tp.Id)
                .Take(limit / 2) // Lấy một phần từ registrations
                .Select(tp => new
                {
                    Time = tp.Tournament.CreatedAt,
                    PlayerName = tp.DisplayName,
                    TournamentName = tp.Tournament.Name,
                    Type = ActivityType.PlayerRegistration
                })
                .ToListAsync(ct);

            foreach (var reg in recentRegistrations)
            {
                activities.Add(new OrganizerActivityDto
                {
                    Time = FormatTime(reg.Time),
                    Message = $"VĐV {reg.PlayerName} đăng ký giải {reg.TournamentName}",
                    Type = reg.Type
                });
            }

            // 2. Giải đấu mới tạo
            var newTournaments = await _db.Tournaments
                .Where(t => t.OwnerUserId == userId && t.CreatedAt >= since)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new
                {
                    Time = t.CreatedAt,
                    TournamentName = t.Name,
                    Type = ActivityType.TournamentCreated
                })
                .ToListAsync(ct);

            foreach (var t in newTournaments)
            {
                activities.Add(new OrganizerActivityDto
                {
                    Time = FormatTime(t.Time),
                    Message = $"Bạn đã tạo giải đấu \"{t.TournamentName}\"",
                    Type = t.Type
                });
            }

            // 3. Giải đấu đã bắt đầu
            var startedTournaments = await _db.Tournaments
                .Where(t => t.OwnerUserId == userId 
                    && t.Status == TournamentStatus.InProgress
                    && t.UpdatedAt >= since)
                .OrderByDescending(t => t.UpdatedAt)
                .Take(5)
                .Select(t => new
                {
                    Time = t.UpdatedAt,
                    TournamentName = t.Name,
                    Type = ActivityType.TournamentStarted
                })
                .ToListAsync(ct);

            foreach (var t in startedTournaments)
            {
                activities.Add(new OrganizerActivityDto
                {
                    Time = FormatTime(t.Time),
                    Message = $"Giải \"{t.TournamentName}\" đã bắt đầu",
                    Type = t.Type
                });
            }

            // 4. Giải đấu đã kết thúc
            var completedTournaments = await _db.Tournaments
                .Where(t => t.OwnerUserId == userId 
                    && t.Status == TournamentStatus.Completed
                    && t.EndUtc != null
                    && t.EndUtc >= since)
                .OrderByDescending(t => t.EndUtc)
                .Take(5)
                .Select(t => new
                {
                    Time = t.EndUtc!.Value,
                    TournamentName = t.Name,
                    Type = ActivityType.TournamentEnded
                })
                .ToListAsync(ct);

            foreach (var t in completedTournaments)
            {
                activities.Add(new OrganizerActivityDto
                {
                    Time = FormatTime(t.Time),
                    Message = $"Giải \"{t.TournamentName}\" đã kết thúc",
                    Type = t.Type
                });
            }

            // Sắp xếp theo thời gian giảm dần và lấy limit
            var sortedActivities = activities
                .OrderByDescending(a => ParseTimeForSort(a.Time))
                .Take(limit)
                .ToList();

            return Ok(sortedActivities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizer recent activities");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Format thời gian thành dạng dễ đọc
    /// </summary>
    private string FormatTime(DateTime time)
    {
        var now = DateTime.UtcNow;
        var diff = now - time;

        if (diff.TotalMinutes < 1)
            return "Vừa xong";
        
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} phút trước";
        
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} giờ trước";
        
        if (diff.TotalDays < 2)
            return "Hôm qua";
        
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} ngày trước";
        
        return time.ToString("dd/MM/yyyy HH:mm");
    }

    /// <summary>
    /// Parse time string để sort
    /// </summary>
    private DateTime ParseTimeForSort(string timeStr)
    {
        var now = DateTime.UtcNow;
        
        if (timeStr == "Vừa xong")
            return now;
        
        if (timeStr.Contains("phút trước"))
        {
            var minutes = int.Parse(timeStr.Split(' ')[0]);
            return now.AddMinutes(-minutes);
        }
        
        if (timeStr.Contains("giờ trước"))
        {
            var hours = int.Parse(timeStr.Split(' ')[0]);
            return now.AddHours(-hours);
        }
        
        if (timeStr == "Hôm qua")
            return now.AddDays(-1);
        
        if (timeStr.Contains("ngày trước"))
        {
            var days = int.Parse(timeStr.Split(' ')[0]);
            return now.AddDays(-days);
        }
        
        if (DateTime.TryParseExact(timeStr, "dd/MM/yyyy HH:mm", null, 
            System.Globalization.DateTimeStyles.None, out var parsed))
            return parsed;
        
        return now.AddYears(-1); // fallback
    }
}

