using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;
using PoolMate.Api.Dtos.Admin.Player;

namespace PoolMate.Api.Services;

public class AdminPlayerService : IAdminPlayerService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminPlayerService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }


    public async Task<PagingList<PlayerListDto>> GetPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.Players.AsNoTracking().AsQueryable();

        // 1. Search Tổng hợp
        if (!string.IsNullOrWhiteSpace(filter.SearchName))
        {
            var term = filter.SearchName.ToLower().Trim();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(term) ||
                (p.Email != null && p.Email.ToLower().Contains(term)) ||
                (p.Phone != null && p.Phone.Contains(term)) ||
                (p.Nickname != null && p.Nickname.ToLower().Contains(term))
            );
        }

        // 2. Search theo Giải đấu
        if (!string.IsNullOrWhiteSpace(filter.SearchTournament))
        {
            var tourTerm = filter.SearchTournament.ToLower().Trim();
            query = query.Where(p => p.TournamentPlayers.Any(tp =>
                tp.Tournament.Name.ToLower().Contains(tourTerm)));
        }

        // 3. Filter thuộc tính cơ bản
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(p => p.Country == filter.Country);

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(p => p.City == filter.City);

        if (filter.MinSkillLevel.HasValue)
            query = query.Where(p => p.SkillLevel >= filter.MinSkillLevel.Value);

        if (filter.MaxSkillLevel.HasValue)
            query = query.Where(p => p.SkillLevel <= filter.MaxSkillLevel.Value);

        // Xử lý lọc theo ngày tạo (CreatedFrom / CreatedTo)
        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            var toDate = filter.CreatedTo.Value.AddDays(1);
            query = query.Where(p => p.CreatedAt < toDate);
        }

        // 4. Filter Data Quality
        if (filter.HasEmail.HasValue)
            query = query.Where(p =>
                filter.HasEmail.Value ? (p.Email != null && p.Email != "") : (p.Email == null || p.Email == ""));

        if (filter.HasPhone.HasValue)
            query = query.Where(p =>
                filter.HasPhone.Value ? (p.Phone != null && p.Phone != "") : (p.Phone == null || p.Phone == ""));

        if (filter.HasSkillLevel.HasValue)
        {
            query = query.Where(p =>
                filter.HasSkillLevel.Value ? p.SkillLevel != null : p.SkillLevel == null);
        }

        // 5. Sắp xếp & Phân trang
        query = ApplySorting(query, filter.SortBy, filter.SortOrder);

        var totalRecords = await query.CountAsync(ct);

        var items = await query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new PlayerListDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return PagingList<PlayerListDto>.Create(items, totalRecords, filter.PageIndex, filter.PageSize);
    }

    public async Task<PlayerDetailDto?> GetPlayerDetailAsync(int playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.TournamentPlayers)
            .ThenInclude(tp => tp.Tournament)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player == null) return null;

        // 1. Map thông tin cơ bản
        var result = new PlayerDetailDto
        {
            Id = player.Id,
            FullName = player.FullName,
            Nickname = player.Nickname,
            Email = player.Email,
            Phone = player.Phone,
            Country = player.Country,
            City = player.City,
            SkillLevel = player.SkillLevel,
            CreatedAt = player.CreatedAt,
            LinkedUserId = player.UserId,
            LinkedUserEmail = player.User?.Email,
            LinkedUserAvatar = player.User?.ProfilePicture,
            DataIssues = new List<string>()
        };

        // 2.Kiểm tra Data Quality tại chỗ
        if (string.IsNullOrWhiteSpace(player.Phone))
            result.DataIssues.Add("Missing Phone");

        if (player.SkillLevel == null)
            result.DataIssues.Add("Missing Skill Level");

        if (!string.IsNullOrWhiteSpace(player.Email) && !PlayerDataValidator.IsValidEmail(player.Email))
            result.DataIssues.Add("Invalid Email Format");

        if (!player.TournamentPlayers.Any())
            result.DataIssues.Add("Never Played");

        // 3. Tính toán thống kê giải đấu
        var tournaments = player.TournamentPlayers.ToList();
        var tournamentDates = tournaments
            .Where(tp => tp.Tournament != null)
            .Select(tp => tp.Tournament.StartUtc)
            .OrderBy(d => d)
            .ToList();

        result.TournamentStats = new TournamentStatsDto
        {
            TotalTournaments = tournaments.Count,
            CompletedTournaments = tournaments.Count(tp =>
                tp.Tournament != null && tp.Tournament.EndUtc.HasValue && tp.Tournament.EndUtc.Value < DateTime.UtcNow),
            ActiveTournaments = tournaments.Count(tp => tp.Tournament != null &&
                                                        tp.Tournament.StartUtc <= DateTime.UtcNow &&
                                                        (!tp.Tournament.EndUtc.HasValue ||
                                                         tp.Tournament.EndUtc.Value >= DateTime.UtcNow)),
            FirstTournamentDate = tournamentDates.FirstOrDefault(),
            LastTournamentDate = tournamentDates.LastOrDefault()
        };

        // 4. Lấy danh sách giải gần đây (Top 10)
        result.RecentTournaments = tournaments
            .Where(tp => tp.Tournament != null)
            .OrderByDescending(tp => tp.Tournament.StartUtc)
            .Take(10)
            .Select(tp => new PlayerTournamentHistoryDto
            {
                TournamentId = tp.TournamentId,
                TournamentName = tp.Tournament.Name,
                TournamentDate = tp.Tournament.StartUtc,
                TournamentStatus = GetTournamentStatus(tp.Tournament),
                Seed = tp.Seed,
                PlayerStatus = tp.Status.ToString()
            })
            .ToList();

        return result;
    }


    public async Task<PlayerStatisticsDto> GetPlayerStatisticsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);
        var allPlayers = await _db.Players
            .AsNoTracking()
            .Include(p => p.TournamentPlayers)
            .ThenInclude(tp => tp.Tournament)
            .ToListAsync(ct);

        var totalPlayers = allPlayers.Count;
        var claimedPlayers = allPlayers.Count(p => !string.IsNullOrEmpty(p.UserId));
        var unclaimedPlayers = totalPlayers - claimedPlayers;

        // Recent activity
        var playersCreatedLast30Days = allPlayers.Count(p => p.CreatedAt >= last30Days);
        var playersCreatedLast7Days = allPlayers.Count(p => p.CreatedAt >= last7Days);
        var playersCreatedToday = allPlayers.Count(p => p.CreatedAt.Date == today);

        // Skill level distribution
        var skillLevelGroups = allPlayers
            .GroupBy(p => p.SkillLevel)
            .Select(g => new SkillLevelDistributionDto
            {
                SkillLevel = g.Key,
                Count = g.Count(),
                Percentage = totalPlayers > 0
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2)
                    : 0
            })
            .OrderBy(x => x.SkillLevel ?? int.MaxValue)
            .ToList();

        // Geographic distribution - Top 10 countries
        var topCountries = allPlayers
            .Where(p => !string.IsNullOrWhiteSpace(p.Country))
            .GroupBy(p => p.Country)
            .Select(g => new GeographicDistributionDto
            {
                Location = g.Key!,
                Count = g.Count(),
                Percentage = totalPlayers > 0
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // Geographic distribution - Top 10 cities
        var topCities = allPlayers
            .Where(p => !string.IsNullOrWhiteSpace(p.City))
            .GroupBy(p => p.City)
            .Select(g => new GeographicDistributionDto
            {
                Location = g.Key!,
                Count = g.Count(),
                Percentage = totalPlayers > 0
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // Tournament activity
        var playersWithTournaments = allPlayers.Count(p => p.TournamentPlayers.Any());
        var playersWithoutTournaments = totalPlayers - playersWithTournaments;

        // Active players (có tournament trong 30 ngày qua)
        var activePlayersLast30Days = allPlayers
            .Count(p => p.TournamentPlayers.Any(tp =>
                tp.Tournament != null &&
                tp.Tournament.StartUtc >= last30Days));

        // Monthly growth trend (last 6 months)
        var monthlyGrowth = new List<PlayerGrowthTrendDto>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = now.AddMonths(-i).Date;
            var monthStartFirstDay = new DateTime(monthStart.Year, monthStart.Month, 1);
            var monthEnd = monthStartFirstDay.AddMonths(1);

            var newPlayersInMonth = allPlayers.Count(p =>
                p.CreatedAt >= monthStartFirstDay &&
                p.CreatedAt < monthEnd);

            var totalPlayersUpToMonth = allPlayers.Count(p => p.CreatedAt < monthEnd);

            monthlyGrowth.Add(new PlayerGrowthTrendDto
            {
                Year = monthStartFirstDay.Year,
                Month = monthStartFirstDay.Month,
                MonthName = monthStartFirstDay.ToString("MMM yyyy"),
                NewPlayers = newPlayersInMonth,
                TotalPlayers = totalPlayersUpToMonth
            });
        }

        return new PlayerStatisticsDto
        {
            TotalPlayers = totalPlayers,
            ClaimedPlayers = claimedPlayers,
            UnclaimedPlayers = unclaimedPlayers,
            PlayersCreatedLast30Days = playersCreatedLast30Days,
            PlayersCreatedLast7Days = playersCreatedLast7Days,
            PlayersCreatedToday = playersCreatedToday,
            SkillLevelDistribution = skillLevelGroups,
            TopCountries = topCountries,
            TopCities = topCities,
            PlayersWithTournaments = playersWithTournaments,
            PlayersWithoutTournaments = playersWithoutTournaments,
            ActivePlayersLast30Days = activePlayersLast30Days,
            MonthlyGrowth = monthlyGrowth
        };
    }

    private string GetTournamentStatus(Tournament tournament)
    {
        var now = DateTime.UtcNow;
        if (tournament.StartUtc > now)
            return "Upcoming";
        if (tournament.EndUtc.HasValue && tournament.EndUtc.Value < now)
            return "Completed";
        return "InProgress";
    }

    private IQueryable<Player> ApplySorting(IQueryable<Player> query, string sortBy, string sortOrder)
    {
        var isDesc = sortOrder != null && sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Player> Sort<TKey>(System.Linq.Expressions.Expression<Func<Player, TKey>> keySelector)
        {
            return isDesc
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector);
        }

        var orderedQuery = (sortBy?.ToLower()) switch
        {
            "fullname" => Sort(p => p.FullName),
            "email" => Sort(p => p.Email),
            "phone" => Sort(p => p.Phone),
            "skilllevel" => Sort(p => p.SkillLevel),
            "country" => Sort(p => p.Country),
            "city" => Sort(p => p.City),
            _ => isDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };
        return orderedQuery.ThenBy(p => p.Id);
    }

    public async Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var oneYearAgo = now.AddYears(-1);
        var query = _db.Players.AsNoTracking();

        // Task A: Đếm số người "Inactive"
        // Logic: Đã từng chơi VÀ (Không có giải nào trong 1 năm qua)
        var inactivePlayers = await query
            .Where(p => p.TournamentPlayers.Any())
            .Where(p => !p.TournamentPlayers.Any(tp => tp.Tournament.StartUtc >= oneYearAgo))
            .CountAsync(ct);

        // Task B: Đếm số người chưa từng chơi
        var neverPlayedTournament = await query
            .Where(p => !p.TournamentPlayers.Any())
            .CountAsync(ct);

        // Task C: Tải dữ liệu THÔ về RAM
        var players = await query.Select(p => new
        {
            p.Id,
            p.Email,
            p.Phone,
            p.SkillLevel,
            p.Country,
            p.City
        }).ToListAsync(ct);

        // Task D: Tổng số (Lấy luôn từ list trong RAM cho nhanh, đỡ phải query DB lần nữa)
        var totalPlayers = players.Count;

        // 3. Xử lý Validate & Đếm trên RAM (Logic cũ giữ nguyên)
        int missingEmail = 0, missingPhone = 0, missingSkill = 0, missingLocation = 0;
        int invalidEmail = 0, invalidPhone = 0, invalidSkill = 0;
        int playersWithIssuesCount = 0;

        var emailFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in players)
        {
            bool hasIssue = false;
            // Check Missing
            if (string.IsNullOrWhiteSpace(p.Email))
            {
                missingEmail++;
                hasIssue = true;
            }

            if (string.IsNullOrWhiteSpace(p.Phone))
            {
                missingPhone++;
                hasIssue = true;
            }

            if (p.SkillLevel == null)
            {
                missingSkill++;
                hasIssue = true;
            }

            if (string.IsNullOrWhiteSpace(p.Country) || string.IsNullOrWhiteSpace(p.City))
            {
                missingLocation++;
                hasIssue = true;
            }

            // Check Invalid
            if (!string.IsNullOrWhiteSpace(p.Email))
            {
                if (!PlayerDataValidator.IsValidEmail(p.Email))
                {
                    invalidEmail++;
                    hasIssue = true;
                }

                var emailKey = p.Email.Trim();
                if (!emailFrequency.ContainsKey(emailKey)) emailFrequency[emailKey] = 0;
                emailFrequency[emailKey]++;
            }

            if (!string.IsNullOrWhiteSpace(p.Phone) && !PlayerDataValidator.IsValidPhone(p.Phone))
            {
                invalidPhone++;
                hasIssue = true;
            }

            if (p.SkillLevel.HasValue && !PlayerDataValidator.IsValidSkillLevel(p.SkillLevel.Value))
            {
                invalidSkill++;
                hasIssue = true;
            }

            if (hasIssue) playersWithIssuesCount++;
        }

        // Đếm số nhóm email bị trùng
        int potentialDuplicates = emailFrequency.Count(kv => kv.Value > 1);

        // 4. Đóng gói kết quả
        var issues = new DataQualityIssuesDto
        {
            MissingEmail = missingEmail,
            MissingPhone = missingPhone,
            MissingSkillLevel = missingSkill,
            MissingLocation = missingLocation,
            InvalidEmail = invalidEmail,
            InvalidPhone = invalidPhone,
            InvalidSkillLevel = invalidSkill,
            InactivePlayers = inactivePlayers,
            NeverPlayedTournament = neverPlayedTournament,
            PotentialDuplicates = potentialDuplicates
        };

        var overview = new DataQualityOverviewDto
        {
            TotalPlayers = totalPlayers,
            PlayersWithIssues = playersWithIssuesCount,
            IssuePercentage = totalPlayers > 0 ? Math.Round((double)playersWithIssuesCount / totalPlayers * 100, 2) : 0,
            HealthyPlayers = totalPlayers - playersWithIssuesCount,
            HealthyPercentage = totalPlayers > 0
                ? Math.Round((double)(totalPlayers - playersWithIssuesCount) / totalPlayers * 100, 2)
                : 0
        };

        var recommendations = new List<string>();
        if (potentialDuplicates > 0)
            recommendations.Add($"{potentialDuplicates} duplicate email groups found - Use 'Merge' tool to fix.");
        if (issues.MissingPhone > totalPlayers * 0.5)
            recommendations.Add("High rate of missing phone numbers - Consider making Phone required.");
        if (issues.InactivePlayers > totalPlayers * 0.3)
            recommendations.Add($"{issues.InactivePlayers} inactive players - Consider archiving old data.");

        return new DataQualityReportDto
        {
            Overview = overview,
            Issues = issues,
            Recommendations = recommendations,
            GeneratedAt = now
        };
    }

    public async Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(string issueType, int pageIndex = 1,
        int pageSize = 20, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var oneYearAgo = now.AddYears(-1);
        var issueTypeLower = issueType.Trim().ToLower(); 

        var query = _db.Players.AsNoTracking();

        // 1. XÂY DỰNG FILTER (SQL WHERE)
        switch (issueTypeLower)
        {
            case "missing-email":
                query = query.Where(p => p.Email == null || p.Email == "");
                break;
            case "missing-phone":
                query = query.Where(p => p.Phone == null || p.Phone == "");
                break;
            case "missing-skill":
                query = query.Where(p => p.SkillLevel == null);
                break;
            case "inactive-1y":
                query = query
                    .Where(p => p.TournamentPlayers.Any())
                    .Where(p => !p.TournamentPlayers.Any(tp => tp.Tournament.StartUtc >= oneYearAgo));
                break;
            case "never-played":
                query = query.Where(p => !p.TournamentPlayers.Any());
                break;
            case "invalid-email":
                // Lọc sơ bộ: chỉ lấy người CÓ email để check sau
                query = query.Where(p => p.Email != null && p.Email != "");
                break;
            case "invalid-phone":
                // Lọc sơ bộ: chỉ lấy người CÓ phone
                query = query.Where(p => p.Phone != null && p.Phone != "");
                break;
            case "potential-duplicates":
                // A. Tìm danh sách Email bị trùng 
                var duplicateEmails = _db.Players
                    .Where(p => p.Email != null && p.Email != "")
                    .GroupBy(p => p.Email)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                // B. Tìm danh sách Phone bị trùng 
                var duplicatePhones = _db.Players
                    .Where(p => p.Phone != null && p.Phone != "")
                    .GroupBy(p => p.Phone)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                // C. Tìm nhóm "Tên + Thành phố + Skill" bị trùng 
                // (Lưu ý: Nếu DB lớn, phần này có thể nặng, có thể bỏ qua nếu chỉ cần Email/Phone)
                var duplicateContexts = _db.Players
                    .Where(p => p.City != null && p.SkillLevel != null)
                    .GroupBy(p => new { p.FullName, p.City, p.SkillLevel })
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                // D. GỘP LẠI: Lấy tất cả Player dính vào 1 trong 3 trường hợp trên
                query = query.Where(p =>
                    (p.Email != null && duplicateEmails.Contains(p.Email)) ||
                    (p.Phone != null && duplicatePhones.Contains(p.Phone)) ||
                    (p.City != null && p.SkillLevel != null &&
                     duplicateContexts.Any(x =>
                         x.FullName == p.FullName && x.City == p.City && x.SkillLevel == p.SkillLevel))
                );
                break;
            default:
                return new PlayersWithIssuesDto();
        }

        // Áp dụng sắp xếp ĐẶC BIỆT cho trường hợp trùng lặp
        // Mục đích: Để các bản ghi trùng nhau nằm cạnh nhau trong danh sách trả về
        if (issueTypeLower == "potential-duplicates")
        {
            query = query.OrderBy(p => p.Email)
                .ThenBy(p => p.Phone)
                .ThenBy(p => p.FullName);
        }

        // Chuẩn bị Projection (chưa chạy)
        var projection = query.Select(p => new PlayerIssueDto
        {
            Id = p.Id,
            FullName = p.FullName,
            Email = p.Email,
            Phone = p.Phone,
            CreatedAt = p.CreatedAt,
            LastTournamentDate = (issueTypeLower == "inactive-1y")
                ? p.TournamentPlayers.Max(tp => tp.Tournament.StartUtc)
                : null
        });

        // 2. THỰC THI & PHÂN TRANG (Chia 2 luồng)
        List<PlayerIssueDto> pagedResult;
        int totalCount;

        // LUỒNG A: Xử lý In-Memory (Cho nhóm Invalid cần Regex)
        if (issueTypeLower.StartsWith("invalid-"))
        {
            // Giới hạn lấy tối đa 2000 bản ghi để check Regex trong RAM
            var candidates = await projection.Take(2000).ToListAsync(ct);
            List<PlayerIssueDto> invalidItems;

            if (issueTypeLower == "invalid-email")
            {
                invalidItems = candidates
                    .Where(x => !PlayerDataValidator.IsValidEmail(x.Email))
                    .ToList();
                invalidItems.ForEach(x => x.Issues.Add("Invalid email format"));
            }
            else // invalid-phone
            {
                invalidItems = candidates
                    .Where(x => !PlayerDataValidator.IsValidPhone(x.Phone))
                    .ToList();
                invalidItems.ForEach(x => x.Issues.Add("Invalid phone format"));
            }

            // Phân trang thủ công trên List
            totalCount = invalidItems.Count;
            pagedResult = invalidItems
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        // LUỒNG B: Xử lý SQL thuần (Cho nhóm Missing/Activity/Duplicates - TỐI ƯU HƠN)
        else
        {
            // Đếm tổng số bản ghi thỏa mãn điều kiện SQL
            totalCount = await query.CountAsync(ct);

            var queryPaged = projection;

            // QUAN TRỌNG: Chỉ áp dụng sort mặc định nếu KHÔNG PHẢI là tìm trùng lặp
            // Nếu là trùng lặp, ta giữ nguyên thứ tự sort Email/Phone đã định nghĩa ở trên
            if (issueTypeLower != "potential-duplicates")
            {
                queryPaged = queryPaged.OrderBy(p => p.Id);
            }

            // Cắt trang ngay tại Database
            pagedResult = await queryPaged
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Gán text lỗi
            string issueText = issueTypeLower switch
            {
                "missing-email" => "Missing email",
                "missing-phone" => "Missing phone",
                "missing-skill" => "Missing skill level",
                "inactive-1y" => "Inactive > 1 year",
                "never-played" => "Never played",
                "potential-duplicates" => "Potential duplicate", 
                _ => "Unknown issue"
            };

            pagedResult.ForEach(x => x.Issues.Add(issueText));
        }

        return new PlayersWithIssuesDto
        {
            Players = pagedResult,
            TotalCount = totalCount
        };
    }

    public Task<ValidationResultDto> ValidatePlayerDataAsync(ValidatePlayerDto request)
    {
        var result = new ValidationResultDto { IsValid = true };

        if (!string.IsNullOrWhiteSpace(request.Email) && !PlayerDataValidator.IsValidEmail(request.Email))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.Email),
                ErrorMessage = "Invalid email format",
                SuggestedFix = "Use a valid email like name@example.com"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !PlayerDataValidator.IsValidPhone(request.Phone))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.Phone),
                ErrorMessage = "Invalid phone format",
                SuggestedFix = "Use international format, e.g., +84901234567"
            });
        }

        if (request.SkillLevel.HasValue && !PlayerDataValidator.IsValidSkillLevel(request.SkillLevel.Value))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.SkillLevel),
                ErrorMessage = "Skill level must be between 1 and 10"
            });
        }

        return Task.FromResult(result);
    }

    public async Task<Response> ExportPlayersAsync(PlayerFilterDto filter, bool includeTournamentHistory, string format,
        CancellationToken ct)
    {
        try
        {
            var query = _db.Players.AsNoTracking().AsQueryable();

            // 2. Tái sử dụng bộ lọc (DRY - Don't Repeat Yourself)
            query = ApplyPlayerFilters(query, filter);
            // 3. Tối ưu hóa việc lấy dữ liệu (Conditional Loading)
            List<Player> players;
            if (includeTournamentHistory)
            {
                // Chỉ Include khi user thực sự cần xuất lịch sử
                players = await query
                    .Include(p => p.TournamentPlayers)
                    .ThenInclude(tp => tp.Tournament)
                    .OrderByDescending(p => p.CreatedAt) // Nên sort để file đẹp
                    .ToListAsync(ct);
            }
            else
            {
                // Nếu không cần lịch sử, chỉ lấy thông tin cơ bản (Query nhẹ hơn rất nhiều)
                players = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(ct);
            }

            // 4. Xử lý CSV
            var sb = new System.Text.StringBuilder();
            if (!includeTournamentHistory)
            {
                sb.AppendLine("PlayerId,FullName,Nickname,Email,Phone,Country,City,SkillLevel,CreatedAt");
                foreach (var p in players)
                {
                    sb.AppendLine(
                        $"{p.Id}," +
                        $"{EscapeCsv(p.FullName)}," +
                        $"{EscapeCsv(p.Nickname)}," +
                        $"{EscapeCsv(p.Email)}," +
                        $"{EscapeCsv(p.Phone)}," +
                        $"{EscapeCsv(p.Country)}," +
                        $"{EscapeCsv(p.City)}," +
                        $"{(p.SkillLevel?.ToString() ?? "")}," +
                        $"{p.CreatedAt:yyyy-MM-dd HH:mm:ss}"
                    );
                }
            }
            else
            {
                sb.AppendLine(
                    "PlayerId,FullName,Email,Phone,Country,City,SkillLevel,CreatedAt,TournamentsCount,LastTournamentDate,TournamentHistory");
                foreach (var p in players)
                {
                    var tournaments = p.TournamentPlayers
                        .Where(tp => tp.Tournament != null)
                        .OrderByDescending(tp => tp.Tournament!.StartUtc)
                        .Select(tp => new
                        {
                            tp.TournamentId,
                            Name = tp.Tournament!.Name,
                            Date = tp.Tournament!.StartUtc
                        })
                        .ToList();
                    // Format history: "ID:Name:Date | ID:Name:Date"
                    var historyString = string.Join(" | ",
                        tournaments.Select(t => $"{t.TournamentId}:{t.Name}:{t.Date:yyyy-MM-dd}"));

                    var lastDate = tournaments.FirstOrDefault()?.Date;
                    sb.AppendLine(
                        $"{p.Id}," +
                        $"{EscapeCsv(p.FullName)}," +
                        $"{EscapeCsv(p.Email)}," +
                        $"{EscapeCsv(p.Phone)}," +
                        $"{EscapeCsv(p.Country)}," +
                        $"{EscapeCsv(p.City)}," +
                        $"{(p.SkillLevel?.ToString() ?? "")}," +
                        $"{p.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                        $"{tournaments.Count}," +
                        $"{(lastDate.HasValue ? lastDate.Value.ToString("yyyy-MM-dd") : "")}," +
                        $"{EscapeCsv(historyString)}" // Escape cả history string vì nó có thể chứa ký tự lạ
                    );
                }
            }

            var fileName = includeTournamentHistory
                ? $"players_history_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                : $"players_list_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            // Trả về object chứa content để Controller chuyển thành File
            var exportResult = new FileExportDto
            {
                FileName = fileName,
                ContentType = "text/csv",
                Content = sb.ToString()
            };

            return Response.Ok(exportResult);
        }
        catch (Exception ex)
        {
            return Response.Error($"Error exporting players: {ex.Message}");
        }
    }

    public async Task<Response> MergePlayersAsync(MergePlayerRequestDto request, CancellationToken ct = default)
{
    // 1. Validate Input
    if (request.SourcePlayerIds == null || !request.SourcePlayerIds.Any())
        return Response.Error("No source players provided.");
    
    if (request.SourcePlayerIds.Contains(request.TargetPlayerId))
        return Response.Error("Target player cannot be in the source list.");

    // Bắt đầu Transaction để đảm bảo an toàn dữ liệu
    await using var transaction = await _db.Database.BeginTransactionAsync(ct);
    try
    {
        // 2. Lấy hồ sơ Gốc (Target)
        var targetPlayer = await _db.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == request.TargetPlayerId, ct);

        if (targetPlayer == null)
            return Response.Error($"Target player (ID: {request.TargetPlayerId}) not found.");

        // 3. Lấy danh sách hồ sơ Rác (Source)
        var sourcePlayers = await _db.Players
            .Where(p => request.SourcePlayerIds.Contains(p.Id))
            .ToListAsync(ct);

        if (sourcePlayers.Count != request.SourcePlayerIds.Count)
            return Response.Error("One or more source players not found.");

        // 4. LOGIC GỘP (QUAN TRỌNG)

        // A. Chuẩn bị dữ liệu để chuyển Lịch Sử Thi Đấu
        
        // Lấy tất cả lịch sử thi đấu của các Source Player
        var sourceHistory = await _db.TournamentPlayers
            .Where(tp => request.SourcePlayerIds.Contains(tp.PlayerId ?? 0))
            .ToListAsync(ct);

        // 🔥 FIX QUAN TRỌNG: Lấy danh sách các giải đấu mà Target Player ĐÃ tham gia
        // Mục đích: Tránh gộp vào giải mà Target đã có mặt -> Gây lỗi trùng lặp (Unique Constraint)
        var targetTournamentIds = await _db.TournamentPlayers
            .Where(tp => tp.PlayerId == targetPlayer.Id)
            .Select(tp => tp.TournamentId)
            .ToListAsync(ct);
        
        // Dùng HashSet để tra cứu cho nhanh
        var targetTournamentIdSet = new HashSet<int>(targetTournamentIds);
        int movedCount = 0;

        foreach (var record in sourceHistory)
        {
            // Kiểm tra: Nếu Target Player ĐÃ ở trong giải đấu này rồi
            if (targetTournamentIdSet.Contains(record.TournamentId))
            {
                // Kịch bản: Cả Source và Target cùng đánh 1 giải.
                // Giải pháp: KHÔNG gộp record này sang Target.
                // Khi Source Player bị xóa (dòng lệnh cuối), record này sẽ tự động set PlayerId = NULL (do OnDelete.SetNull).
                // Các trận đấu (Match) của record này vẫn tồn tại nhưng sẽ không link tới Player nào cả.
                continue; 
            }

            // Nếu Target chưa có trong giải này -> An toàn để chuyển ownership
            record.PlayerId = targetPlayer.Id;
            movedCount++;
        }

        // B. Xử lý Tài khoản liên kết (User Link Safety)
        // Nếu Target chưa có User, mà một trong các Source lại có User -> Chuyển User sang Target
        if (targetPlayer.UserId == null)
        {
            var sourceWithUser = sourcePlayers.FirstOrDefault(p => p.UserId != null);
            if (sourceWithUser != null)
            {
                targetPlayer.UserId = sourceWithUser.UserId;
                // Clear bên source để tránh lỗi unique UserID (nếu có constraint 1-1)
                sourceWithUser.UserId = null;
            }
        }
        else
        {
            // Nếu Target đã có User, mà Source cũng có User khác -> CHẶN
            // Vì không thể gộp 2 tài khoản đăng nhập khác nhau làm 1 được.
            if (sourcePlayers.Any(p => p.UserId != null && p.UserId != targetPlayer.UserId))
            {
                await transaction.RollbackAsync(ct);
                return Response.Error(
                    "Cannot merge: One of the source players belongs to a different User account (Email conflict).");
            }
        }

        // 5. Xóa các Source Player
        // Nhờ cấu hình OnDelete(SetNull), các TournamentPlayers còn sót lại (do trùng giải) sẽ tự update PlayerId = NULL
        _db.Players.RemoveRange(sourcePlayers);
        
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Response.Ok(new
        {
            MergedCount = sourcePlayers.Count,
            TargetPlayerName = targetPlayer.FullName,
            TargetPlayerId = targetPlayer.Id,
            MovedTournamentRecords = movedCount
        }, "Players merged successfully.");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(ct);
        return Response.Error($"Merge failed: {ex.Message}");
    }
}
    

    private IQueryable<Player> ApplyPlayerFilters(IQueryable<Player> query, PlayerFilterDto filter)
    {
        // 1. Search Tổng hợp (Tên, Email, SĐT, Nickname)
        if (!string.IsNullOrWhiteSpace(filter.SearchName))
        {
            var term = filter.SearchName.ToLower().Trim();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(term) ||
                (p.Email != null && p.Email.ToLower().Contains(term)) ||
                (p.Phone != null && p.Phone.Contains(term)) ||
                (p.Nickname != null && p.Nickname.ToLower().Contains(term))
            );
        }

        // 2. Search theo tên Giải đấu (Dùng Any tối ưu)
        if (!string.IsNullOrWhiteSpace(filter.SearchTournament))
        {
            var tourTerm = filter.SearchTournament.ToLower().Trim();
            query = query.Where(p => p.TournamentPlayers.Any(tp =>
                tp.Tournament.Name.ToLower().Contains(tourTerm)));
        }

        // 3. Các bộ lọc thuộc tính chính xác
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(p => p.Country == filter.Country);

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(p => p.City == filter.City);

        // 4. Lọc theo khoảng Skill Level
        if (filter.MinSkillLevel.HasValue)
            query = query.Where(p => p.SkillLevel >= filter.MinSkillLevel.Value);

        if (filter.MaxSkillLevel.HasValue)
            query = query.Where(p => p.SkillLevel <= filter.MaxSkillLevel.Value);

        // 5. Lọc theo ngày tạo
        if (filter.CreatedFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
        {
            // Cộng thêm 1 ngày để lấy trọn vẹn ngày kết thúc
            var toDate = filter.CreatedTo.Value.AddDays(1);
            query = query.Where(p => p.CreatedAt < toDate);
        }

        // 6. Các bộ lọc chất lượng dữ liệu (Data Quality Flags)
        if (filter.HasEmail.HasValue)
        {
            query = query.Where(p => filter.HasEmail.Value
                ? (p.Email != null && p.Email != "")
                : (p.Email == null || p.Email == ""));
        }

        if (filter.HasPhone.HasValue)
        {
            query = query.Where(p => filter.HasPhone.Value
                ? (p.Phone != null && p.Phone != "")
                : (p.Phone == null || p.Phone == ""));
        }

        if (filter.HasSkillLevel.HasValue)
        {
            query = query.Where(p => filter.HasSkillLevel.Value
                ? p.SkillLevel != null
                : p.SkillLevel == null);
        }

        return query;
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains("\""))
        {
            value = value.Replace("\"", "\"\"");
        }

        return $"\"{value}\"";
    }
}