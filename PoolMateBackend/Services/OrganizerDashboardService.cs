using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Dashboard;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class OrganizerDashboardService : IOrganizerDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrganizerDashboardService> _logger;

    public OrganizerDashboardService(
        ApplicationDbContext db,
        ILogger<OrganizerDashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrganizerDashboardStatsDto> GetStatsAsync(string userId, CancellationToken ct = default)
    {
        // Query 1 lần, load tất cả tournaments của user vào memory
        var userTournaments = await _db.Tournaments
            .AsNoTracking()
            .Where(t => t.OwnerUserId == userId)
            .Select(t => new { t.Id, t.Name, t.Status })
            .ToListAsync(ct);

        // Đếm trên memory (không query DB nhiều lần)
        var activeTournaments = userTournaments.Count(t => t.Status == TournamentStatus.InProgress);
        var upcomingTournaments = userTournaments.Count(t => t.Status == TournamentStatus.Upcoming);
        var completedTournaments = userTournaments.Count(t => t.Status == TournamentStatus.Completed);

        _logger.LogInformation("Stats: Active={Active}, Upcoming={Upcoming}, Completed={Completed}", 
            activeTournaments, upcomingTournaments, completedTournaments);

        // 2. Thống kê người tham gia (Lượt đăng ký)
        var totalParticipants = await _db.TournamentPlayers
            .AsNoTracking()
            .CountAsync(tp => tp.Tournament.OwnerUserId == userId, ct);

        // 3. Thống kê trận đấu (Workload thực tế)
        var totalMatches = await _db.Matches
            .AsNoTracking()
            .CountAsync(m => m.Tournament.OwnerUserId == userId, ct);

        var totalTournaments = userTournaments.Count;
        var avgPlayers = totalTournaments > 0
            ? Math.Round((double)totalParticipants / totalTournaments, 1)
            : 0;

        return new OrganizerDashboardStatsDto
        {
            ActiveTournaments = activeTournaments,
            UpcomingTournaments = upcomingTournaments,
            CompletedTournaments = completedTournaments,
            TotalParticipants = totalParticipants,
            TotalMatches = totalMatches,
            AvgPlayersPerTournament = avgPlayers,
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<List<OrganizerActivityDto>> GetRecentActivitiesAsync(string userId, int limit,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        // A. VĐV đăng ký
        var registrations = await _db.TournamentPlayers.AsNoTracking()
            .Include(tp => tp.Tournament)
            .Where(tp => tp.Tournament.OwnerUserId == userId && tp.Tournament.CreatedAt >= since)
            .OrderByDescending(tp => tp.Id)
            .Take(limit)
            .Select(tp => new OrganizerActivityDto
            {
                CreatedAt = tp.Tournament.CreatedAt, // Tạm dùng ngày tạo giải nếu TP ko có CreatedAt
                Message = $"VĐV {tp.DisplayName} đăng ký giải {tp.Tournament.Name}",
                Type = "PlayerRegistration"
            })
            .ToListAsync(ct);

        // B. Giải mới tạo
        var created = await _db.Tournaments.AsNoTracking()
            .Where(t => t.OwnerUserId == userId && t.CreatedAt >= since)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new OrganizerActivityDto
            {
                CreatedAt = t.CreatedAt,
                Message = $"Bạn đã tạo giải đấu \"{t.Name}\"",
                Type = "TournamentCreated"
            })
            .ToListAsync(ct);

        // C. Giải bắt đầu
        var started = await _db.Tournaments.AsNoTracking()
            .Where(t => t.OwnerUserId == userId && t.Status == TournamentStatus.InProgress && t.UpdatedAt >= since)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(limit)
            .Select(t => new OrganizerActivityDto
            {
                CreatedAt = t.UpdatedAt,
                Message = $"Giải \"{t.Name}\" đã bắt đầu",
                Type = "TournamentStarted"
            })
            .ToListAsync(ct);

        // --- HẾT PHẦN QUERY ---

        // 2. Gộp và Sắp xếp lại trên RAM
        var allActivities = new List<OrganizerActivityDto>();
        allActivities.AddRange(registrations);
        allActivities.AddRange(created);
        allActivities.AddRange(started);

        return allActivities
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
    }
}