using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.PlayerProfile;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class PlayerProfileService : IPlayerProfileService
{
    private readonly ApplicationDbContext _db;

    public PlayerProfileService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CreatePlayerProfileResponseDto?> CreatePlayerProfileAsync(
        string userId,
        ApplicationUser user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var exists = await _db.Players.AsNoTracking().AnyAsync(p => p.UserId == userId, ct);
        if (exists)
        {
            throw new InvalidOperationException("User already has a player profile.");
        }

        string fullNameMap = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullNameMap))
        {
            fullNameMap = user.UserName ?? "Unknown Player";
        }
        
        string baseSlug = SlugHelper.GenerateSlug(fullNameMap);
        string finalSlug = baseSlug;
        int count = 1;
        while (await _db.Players.AsNoTracking().AnyAsync(p => p.Slug == finalSlug, ct))
        {
            finalSlug = $"{baseSlug}-{count}";
            count++;
        }

        var newPlayer = new Player
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Email = user.Email,
            FullName = fullNameMap,
            Slug = finalSlug, 
            Nickname = user.Nickname,
            Phone = user.PhoneNumber,
            Country = user.Country,
            City = user.City,
            SkillLevel = null
        };

        _db.Players.Add(newPlayer);
        await _db.SaveChangesAsync(ct);
        
        return new CreatePlayerProfileResponseDto
        {
            FullName = newPlayer.FullName,
            Nickname = newPlayer.Nickname,
            Email = newPlayer.Email,
            Phone = newPlayer.Phone,
            Country = newPlayer.Country,
            City = newPlayer.City,
            CreatedAt = newPlayer.CreatedAt,
            Message = "Player profile created automatically from account info"
        };
    }

    public async Task UpdatePlayerFromUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        // 1. Tìm hồ sơ Player của User này
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        // Nếu chưa có hồ sơ Player thì bỏ qua, không cần sync
        if (player == null) return;

        // 2. Map dữ liệu từ User -> Player
        
        // Xử lý FullName (Giống logic lúc Create)
        string fullNameMap = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullNameMap))
        {
            fullNameMap = user.UserName ?? "Unknown Player";
        }

        // Cập nhật các trường
        player.FullName = fullNameMap;
        player.Nickname = user.Nickname;
        player.Email = user.Email;
        player.Phone = user.PhoneNumber;
        player.Country = user.Country;
        player.City = user.City;
        
        // Cập nhật Slug nếu tên thay đổi
        string baseSlug = SlugHelper.GenerateSlug(fullNameMap);
        if (player.Slug != baseSlug)
        {
            // Kiểm tra xem slug mới đã tồn tại chưa
            string finalSlug = baseSlug;
            int count = 1;
            while (await _db.Players.AsNoTracking().AnyAsync(p => p.Slug == finalSlug && p.Id != player.Id, ct))
            {
                finalSlug = $"{baseSlug}-{count}";
                count++;
            }
            player.Slug = finalSlug;
        }

        // 3. Lưu thay đổi
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<PlayerProfileDetailDto>> GetMyPlayerProfilesAsync(
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<PlayerProfileDetailDto>();
        }

        var profiles = await _db.Players
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlayerProfileDetailDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Slug = p.Slug,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return profiles;
    }

    public async Task<PagingList<MatchHistoryDto>> GetMatchHistoryAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // BƯỚC 1: Query trực tiếp từ bảng Match (Dựa trên Navigation Property)
        var query = _db.Matches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatus.Completed &&
                        ((m.Player1Tp != null && m.Player1Tp.PlayerId == playerId) ||
                         (m.Player2Tp != null && m.Player2Tp.PlayerId == playerId)));

        var totalCount = await query.CountAsync(ct);

        // BƯỚC 2: Projection (Select) thông minh
        var items = await query
            .OrderByDescending(m => m.Tournament.StartUtc)
            .ThenByDescending(m => m.RoundNo)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                Match = m,
                IsPlayer1 = (m.Player1Tp != null && m.Player1Tp.PlayerId == playerId)
            })
            .Select(x => new MatchHistoryDto
            {
                MatchId = x.Match.Id,
                TournamentId = x.Match.TournamentId,
                TournamentName = x.Match.Tournament.Name,
                TournamentDate = x.Match.Tournament.StartUtc,
                GameType = x.Match.Tournament.GameType.ToString(),

                StageType = x.Match.Stage.Type.ToString(),
                BracketSide = x.Match.Bracket.ToString(),
                RoundName = "Round " + x.Match.RoundNo,

                // Logic chọn đối thủ
                OpponentName = x.IsPlayer1
                    ? (x.Match.Player2Tp != null ? x.Match.Player2Tp.DisplayName : "Bye")
                    : (x.Match.Player1Tp != null ? x.Match.Player1Tp.DisplayName : "Bye"),

                // OpponentId (PlayerId của đối thủ)
                OpponentId = x.IsPlayer1
                    ? (x.Match.Player2Tp != null ? x.Match.Player2Tp.PlayerId : null)
                    : (x.Match.Player1Tp != null ? x.Match.Player1Tp.PlayerId : null),

                // Logic tính điểm
                Score = x.IsPlayer1
                    ? (x.Match.ScoreP1 ?? 0) + " - " + (x.Match.ScoreP2 ?? 0)
                    : (x.Match.ScoreP2 ?? 0) + " - " + (x.Match.ScoreP1 ?? 0),

                RaceTo = x.Match.RaceTo ?? 0,

                // Logic thắng thua
                Result = x.Match.WinnerTpId == (x.IsPlayer1 ? x.Match.Player1TpId : x.Match.Player2TpId)
                    ? "Win"
                    : "Loss",

                MatchDate = x.Match.ScheduledUtc
            })
            .ToListAsync(ct);

        return PagingList<MatchHistoryDto>.Create(items, totalCount, pageIndex, pageSize);
    }

    public async Task<PlayerStatsDto> GetPlayerStatsAsync(int playerId, CancellationToken ct = default)
    {
        var tpIds = await _db.TournamentPlayers
            .AsNoTracking()
            .Where(tp => tp.PlayerId == playerId)
            .Select(tp => tp.Id)
            .ToListAsync(ct);

        if (tpIds.Count == 0)
        {
            return new PlayerStatsDto();
        }

        // 2. Lấy tất cả trận đấu đã kết thúc
        var matches = await _db.Matches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatus.Completed &&
                        ((m.Player1TpId.HasValue && tpIds.Contains(m.Player1TpId.Value)) ||
                         (m.Player2TpId.HasValue && tpIds.Contains(m.Player2TpId.Value))))
            .OrderByDescending(m => m.Tournament.StartUtc) 
            .ThenByDescending(m => m.ScheduledUtc)
            .Select(m => new
            {
                m.Id,
                m.WinnerTpId,
                GameType = m.Tournament.GameType // Lấy Enum GameType
            })
            .ToListAsync(ct);

        // 3. Tính toán thống kê (In-Memory)
        int totalWins = 0;
        int totalLosses = 0;
        var recentForm = new List<string>();

        // Dictionary để gom nhóm theo GameType
        var gameTypeStats = new Dictionary<GameType, (int Wins, int Losses)>();

        foreach (var m in matches)
        {
            // Xác định Thắng/Thua
            // Nếu WinnerId nằm trong danh sách ID của mình -> Thắng
            bool isWin = m.WinnerTpId.HasValue && tpIds.Contains(m.WinnerTpId.Value);

            if (isWin) totalWins++;
            else totalLosses++;

            // Tính Recent Form (chỉ lấy 5 trận đầu tiên vì list đã sort desc)
            if (recentForm.Count < 5)
            {
                recentForm.Add(isWin ? "W" : "L");
            }

            // Gom nhóm GameType
            if (!gameTypeStats.ContainsKey(m.GameType))
            {
                gameTypeStats[m.GameType] = (0, 0);
            }

            var current = gameTypeStats[m.GameType];
            if (isWin) current.Wins++;
            else current.Losses++;
            gameTypeStats[m.GameType] = current;
        }

        // 4. Tính tổng số giải đã tham gia
        // (Đếm số lượng TournamentPlayer unique của player này)
        var totalTournaments = tpIds.Count;
        // 5. Đóng gói kết quả
        var totalMatches = matches.Count;
        var winRate = totalMatches > 0
            ? Math.Round((double)totalWins / totalMatches * 100, 1)
            : 0;

        var statsByGameType = gameTypeStats.Select(x =>
        {
            var totalGameMatches = x.Value.Wins + x.Value.Losses;
            return new GameTypeStatsDto
            {
                GameType = x.Key.ToString(), // Enum to String ("NineBall")
                Wins = x.Value.Wins,
                Losses = x.Value.Losses,
                WinRate = totalGameMatches > 0
                    ? Math.Round((double)x.Value.Wins / totalGameMatches * 100, 1)
                    : 0
            };
        }).ToList();

        return new PlayerStatsDto
        {
            TotalMatches = totalMatches,
            TotalWins = totalWins,
            TotalLosses = totalLosses,
            WinRate = winRate,
            TotalTournaments = totalTournaments,
            RecentForm = recentForm, // ["W", "W", "L", "W", "L"]
            StatsByGameType = statsByGameType
        };
    }

    public async Task<PlayerProfileDetailDto?> GetPlayerBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var player = await _db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

        if (player == null)
        {
            return null;
        }

        return new PlayerProfileDetailDto
        {
            Id = player.Id,
            FullName = player.FullName,
            Slug = player.Slug,
            Nickname = player.Nickname,
            Email = player.Email,
            Phone = player.Phone,
            Country = player.Country,
            City = player.City,
            SkillLevel = player.SkillLevel,
            CreatedAt = player.CreatedAt
        };
    }

    public async Task<PagingList<PlayerTournamentDto>> GetMyTournamentsHistoryAsync(
        int playerId, 
        int pageIndex = 1, 
        int pageSize = 20, 
        CancellationToken ct = default)
    {
        // Query TournamentPlayer để lấy tất cả tournaments mà player đã tham gia
        var query = _db.TournamentPlayers
            .AsNoTracking()
            .Where(tp => tp.PlayerId == playerId)
            .Include(tp => tp.Tournament)
                .ThenInclude(t => t.Venue)
            .OrderByDescending(tp => tp.Tournament.StartUtc)
            .Select(tp => new PlayerTournamentDto
            {
                // Thông tin giải đấu
                TournamentId = tp.TournamentId,
                TournamentName = tp.Tournament.Name,
                StartDate = tp.Tournament.StartUtc,
                EndDate = tp.Tournament.EndUtc,
                Status = tp.Tournament.Status.ToString(),
                
                // Thông tin chuyên môn
                GameType = tp.Tournament.GameType.ToString(),
                BracketType = tp.Tournament.BracketType.ToString(),
                
                // Địa điểm
                VenueName = tp.Tournament.Venue != null ? tp.Tournament.Venue.Name : null,
                
                // Thông tin cá nhân của Player tại giải này
                RegistrationStatus = tp.Status.ToString(),
                Seed = tp.Seed,
                SkillLevelSnapshot = tp.SkillLevel
            });

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagingList<PlayerTournamentDto>.Create(items, totalCount, pageIndex, pageSize);
    }

    // ========== PUBLIC APIs - Anyone can access ==========

    /// <summary>
    /// Lấy match history của bất kỳ player nào (public)
    /// </summary>
    public async Task<PagingList<MatchHistoryDto>> GetMatchHistoryByPlayerIdAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // Tái sử dụng logic từ GetMatchHistoryAsync
        return await GetMatchHistoryAsync(playerId, pageIndex, pageSize, ct);
    }

    /// <summary>
    /// Lấy tournament history của bất kỳ player nào (public)
    /// </summary>
    public async Task<PagingList<PlayerTournamentDto>> GetTournamentHistoryByPlayerIdAsync(
        int playerId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        return await GetMyTournamentsHistoryAsync(playerId, pageIndex, pageSize, ct);
    }
    
    public async Task<PagingList<PlayerListDto>> GetAllPlayersAsync(
        PlayerListFilterDto filter,
        CancellationToken ct = default)
    {
        var query = _db.Players.AsNoTracking();

        // Filter by HasSkillLevel
        if (filter.HasSkillLevel.HasValue)
        {
            if (filter.HasSkillLevel.Value)
            {
                // Players WITH skill level
                query = query.Where(p => p.SkillLevel.HasValue);
            }
            else
            {
                // Players WITHOUT skill level
                query = query.Where(p => !p.SkillLevel.HasValue);
            }
        }

        // Filter by search term (FullName or Nickname)
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchLower = filter.SearchTerm.ToLower();
            query = query.Where(p => 
                p.FullName.ToLower().Contains(searchLower) || 
                (p.Nickname != null && p.Nickname.ToLower().Contains(searchLower)));
        }

        // Filter by country
        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            query = query.Where(p => p.Country == filter.Country);
        }

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Get paginated items
        var items = await query
            .OrderBy(p => p.FullName) // Sort by name
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new PlayerListDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Slug = p.Slug, // Important: Include Slug for frontend routing
                Nickname = p.Nickname,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return PagingList<PlayerListDto>.Create(items, totalCount, filter.PageIndex, filter.PageSize);
    }
}