using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoolMate.Api.Common;
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
    

    public async Task<PagingList<OrganizerPlayerListDto>> GetOrganizerPlayersAsync(
        string ownerUserId, 
        string? search, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default)
    {
        // 1. Query từ bảng TournamentPlayers
        var query = _db.TournamentPlayers
            .AsNoTracking()
            .Include(tp => tp.Tournament)
            // QUAN TRỌNG: Chỉ lấy VĐV thuộc các giải do User này làm chủ
            .Where(tp => tp.Tournament.OwnerUserId == ownerUserId); 

        // 2. Tìm kiếm theo tên VĐV hoặc tên giải đấu
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp => 
                tp.DisplayName.ToLower().Contains(s) || 
                tp.Tournament.Name.ToLower().Contains(s));
        }

        // 3. Đếm tổng số (Phục vụ phân trang)
        var totalCount = await query.CountAsync(ct);

        // 4. Lấy dữ liệu & Phân trang
        // Sắp xếp: VĐV tham gia gần nhất lên đầu
        var items = await query
            .OrderByDescending(tp => tp.Tournament.StartUtc) 
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(tp => new OrganizerPlayerListDto
            {
                TournamentPlayerId = tp.Id,
                DisplayName = tp.DisplayName,
                Email = tp.Email,
                Phone = tp.Phone,
                SkillLevel = tp.SkillLevel,
                
                TournamentId = tp.TournamentId,
                TournamentName = tp.Tournament.Name,
                JoinedDate = tp.Tournament.CreatedAt // Hoặc StartUtc
            })
            .ToListAsync(ct);

        return PagingList<OrganizerPlayerListDto>.Create(items, totalCount, pageIndex, pageSize);
    }

    public async Task<PagingList<OrganizerPlayerDto>> GetMyPlayersAsync(
        string userId, 
        int? tournamentId, // Lọc theo giải đấu cụ thể (optional)
        string? search, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default)
    {
        // 1. Khởi tạo Query
        var query = _db.TournamentPlayers
            .AsNoTracking()
            .Include(tp => tp.Tournament)
            // Luôn phải check OwnerUserId để đảm bảo bảo mật (không xem trộm giải người khác)
            .Where(tp => tp.Tournament.OwnerUserId == userId);

        // 2. 👇 LOGIC MỚI: Nếu có ID giải đấu thì lọc theo giải đó
        if (tournamentId.HasValue)
        {
            query = query.Where(tp => tp.TournamentId == tournamentId.Value);
        }

        // 3. Tìm kiếm theo tên VĐV hoặc tên giải
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(tp => 
                tp.DisplayName.ToLower().Contains(s) || 
                tp.Tournament.Name.ToLower().Contains(s));
        }

        // 4. Đếm tổng
        var totalCount = await query.CountAsync(ct);

        // 5. Lấy dữ liệu & Phân trang
        var items = await query
            .OrderByDescending(tp => tp.Tournament.StartUtc) // Mới nhất lên đầu
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(tp => new OrganizerPlayerDto
            {
                TournamentPlayerId = tp.Id,
                DisplayName = tp.DisplayName,
                Email = tp.Email,
                Phone = tp.Phone,
                SkillLevel = tp.SkillLevel,
                
                TournamentId = tp.TournamentId,
                TournamentName = tp.Tournament.Name,
                JoinedDate = tp.Tournament.CreatedAt,
                Status = tp.Status.ToString()
            })
            .ToListAsync(ct);

        return PagingList<OrganizerPlayerDto>.Create(items, totalCount, pageIndex, pageSize);
    }

    public async Task<PagingList<OrganizerTournamentDto>> GetMyTournamentsAsync(
        string userId, 
        string? search, 
        TournamentStatus? status, 
        int pageIndex, 
        int pageSize, 
        CancellationToken ct = default)
    {
        // 1. Khởi tạo Query
        var query = _db.Tournaments
            .AsNoTracking()
            .Where(t => t.OwnerUserId == userId);

        // 2. Lọc theo Trạng thái (nếu có)
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        // 3. Tìm kiếm theo tên
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(s));
        }

        // 4. Đếm tổng số
        var totalCount = await query.CountAsync(ct);

        // 5. Lấy dữ liệu & Phân trang
        // Sắp xếp: Giải mới tạo lên đầu
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new OrganizerTournamentDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status.ToString(),
                GameType = t.GameType.ToString(),
                StartDate = t.StartUtc,
                CreatedAt = t.CreatedAt,
                
                // Đếm số lượng VĐV và Trận đấu trong giải đó
                PlayerCount = t.TournamentPlayers.Count,
                MatchCount = t.Matches.Count
            })
            .ToListAsync(ct);

        return PagingList<OrganizerTournamentDto>.Create(items, totalCount, pageIndex, pageSize);
    }

    public async Task<TournamentOverviewDto?> GetTournamentOverviewAsync(
        int tournamentId, 
        string userId, 
        CancellationToken ct = default)
    {
        // 1. Kiểm tra quyền sở hữu
        var tournament = await _db.Tournaments.AsNoTracking()
            .Where(t => t.Id == tournamentId && t.OwnerUserId == userId)
            .Select(t => new { t.Id, t.Name, t.Status })
            .FirstOrDefaultAsync(ct);

        if (tournament == null) return null;

        // 2. Query các chỉ số (Chạy tuần tự an toàn)
        
        // A. Thống kê Match
        var matchesQuery = _db.Matches.AsNoTracking().Where(m => m.TournamentId == tournamentId);
        var totalMatches = await matchesQuery.CountAsync(ct);
        var completedMatches = await matchesQuery.CountAsync(m => m.Status == MatchStatus.Completed, ct);
        var inProgressMatches = await matchesQuery.CountAsync(m => m.Status == MatchStatus.InProgress, ct);
        
        // Trận "Ready": Chưa đấu nhưng đã có đủ P1 và P2 (Sẵn sàng gọi tên)
        var scheduledMatches = await matchesQuery.CountAsync(m => 
            m.Status == MatchStatus.NotStarted && 
            m.Player1TpId != null && 
            m.Player2TpId != null, ct);

        // B. Thống kê Player
        var playersQuery = _db.TournamentPlayers.AsNoTracking().Where(tp => tp.TournamentId == tournamentId);
        var totalPlayers = await playersQuery.CountAsync(ct);
        var confirmedPlayers = await playersQuery.CountAsync(tp => tp.Status == TournamentPlayerStatus.Confirmed, ct);
        
        // C. Thống kê Table
        var tablesQuery = _db.TournamentTables.AsNoTracking().Where(tt => tt.TournamentId == tournamentId);
        var totalTables = await tablesQuery.CountAsync(ct);
        // Bàn đang được sử dụng
        var activeTables = await tablesQuery.CountAsync(tt => tt.Status == TableStatus.InUse, ct);

        // 3. Tính toán %
        double progress = totalMatches > 0 
            ? Math.Round((double)completedMatches / totalMatches * 100, 1) 
            : 0;

        return new TournamentOverviewDto
        {
            TournamentId = tournament.Id,
            TournamentName = tournament.Name,
            Status = tournament.Status.ToString(),
            
            TotalMatches = totalMatches,
            CompletedMatches = completedMatches,
            InProgressMatches = inProgressMatches,
            ScheduledMatches = scheduledMatches,
            ProgressPercentage = progress,
            
            TotalPlayers = totalPlayers,
            ConfirmedPlayers = confirmedPlayers,
            UnconfirmedPlayers = totalPlayers - confirmedPlayers,
            
            TotalTables = totalTables,
            ActiveTables = activeTables,
            FreeTables = totalTables - activeTables
        };
    }
}