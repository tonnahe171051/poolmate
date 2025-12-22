using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Hubs;
using PoolMate.Api.Models;
using System.Text;
using PoolMate.Api.Dtos.Admin.Player;
using GeographicDistributionDto = PoolMate.Api.Dtos.Admin.Users.GeographicDistributionDto;

namespace PoolMate.Api.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AdminUserService> _logger;
    private readonly IBannedUserCacheService _bannedUserCache;
    private readonly IHubContext<AppHub> _hubContext;

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db,
        ILogger<AdminUserService> logger,
        IBannedUserCacheService bannedUserCache,
        IHubContext<AppHub> hubContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
        _bannedUserCache = bannedUserCache;
        _hubContext = hubContext;
    }

    public async Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct)
    {
        try
        {
            // BƯỚC 1: Base Query (Chỉ đọc)
            var query = _db.Users.AsNoTracking().AsQueryable();

            // BƯỚC 2: SEARCH - Tìm kiếm đa năng
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.UserName.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term) ||
                    (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                    (u.Nickname != null && u.Nickname.ToLower().Contains(term)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(term))
                );
            }

            // BƯỚC 3: FILTER THEO ROLE
            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var roleName = filter.Role.Trim();
                var role = await _roleManager.FindByNameAsync(roleName);

                if (role != null)
                {
                    var userIdsInRole = _db.UserRoles
                        .Where(ur => ur.RoleId == role.Id)
                        .Select(ur => ur.UserId);

                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
                else
                {
                    return Response.Ok(PagingList<AdminUserListDto>.Create(new List<AdminUserListDto>(), 0, 1, 10));
                }
            }

            // BƯỚC 4: CÁC FILTER KHÁC
            if (filter.EmailConfirmed.HasValue)
                query = query.Where(u => u.EmailConfirmed == filter.EmailConfirmed.Value);

            if (filter.PhoneNumberConfirmed.HasValue)
                query = query.Where(u => u.PhoneNumberConfirmed == filter.PhoneNumberConfirmed.Value);

            if (filter.TwoFactorEnabled.HasValue)
                query = query.Where(u => u.TwoFactorEnabled == filter.TwoFactorEnabled.Value);

            if (filter.LockoutEnabled.HasValue)
                query = query.Where(u => u.LockoutEnabled == filter.LockoutEnabled.Value);

            if (filter.IsLockedOut.HasValue)
            {
                var now = DateTimeOffset.UtcNow;
                if (filter.IsLockedOut.Value)
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now); // Đang bị khóa
                else
                    query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now); // Không bị khóa
            }

            if (!string.IsNullOrWhiteSpace(filter.Country))
                query = query.Where(u => u.Country == filter.Country);

            if (!string.IsNullOrWhiteSpace(filter.City))
                query = query.Where(u => u.City == filter.City);

            if (filter.CreatedFrom.HasValue)
                query = query.Where(u => u.CreatedAt >= filter.CreatedFrom.Value);

            if (filter.CreatedTo.HasValue)
            {
                var toDate = filter.CreatedTo.Value.AddDays(1);
                query = query.Where(u => u.CreatedAt < toDate);
            }

            // BƯỚC 5: ĐẾM & SẮP XẾP
            var totalRecords = await query.CountAsync(ct);
            query = ApplySorting(query, filter.SortBy, filter.IsDescending);

            // BƯỚC 6: PHÂN TRANG (Lấy dữ liệu User trước)
            var pageIndex = Math.Max(1, filter.PageIndex);
            var pageSize = Math.Max(1, filter.PageSize); // Đảm bảo không chia cho 0 hoặc âm
            var users = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // BƯỚC 7: LẤY ROLES RIÊNG (Kỹ thuật tối ưu)
            var userIds = users.Select(u => u.Id).ToList();

            // Query bảng UserRoles join với Roles
            var userRolesMap = await _db.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_db.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name })
                .ToListAsync(ct);

            var linkedPlayersMap = await _db.Players
                .AsNoTracking()
                .Where(p => p.UserId != null && userIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.Id, p.FullName, p.CreatedAt }) // Chỉ lấy cột cần thiết
                .ToListAsync(ct);

            // BƯỚC 8: MAP DỮ LIỆU TRÊN RAM (DTO Mapping)
            var userDtos = users.Select(u =>
            {
                var linkedPlayer = linkedPlayersMap
                    .Where(p => p.UserId == u.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
                return new AdminUserListDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    FullName = $"{u.FirstName} {u.LastName}".Trim(),
                    Nickname = u.Nickname,
                    Country = u.Country,
                    City = u.City,
                    AvatarUrl = u.ProfilePicture,

                    EmailConfirmed = u.EmailConfirmed,
                    TwoFactorEnabled = u.TwoFactorEnabled,
                    LockoutEnabled = u.LockoutEnabled,
                    LockoutEnd = u.LockoutEnd?.DateTime,
                    IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                    AccessFailedCount = u.AccessFailedCount,
                    CreatedAt = u.CreatedAt,
                    Roles = userRolesMap
                        .Where(r => r.UserId == u.Id)
                        .Select(r => r.RoleName!)
                        .ToList(),
                    LinkedPlayerId = linkedPlayer?.Id,
                    LinkedPlayerName = linkedPlayer?.FullName
                };
            }).ToList();

            var result = PagingList<AdminUserListDto>.Create(
                userDtos,
                totalRecords,
                pageIndex,
                pageSize
            );

            return Response.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users list with filter {@Filter}", filter);
            return Response.Error("Internal server error while fetching users.");
        }
    }

    private IQueryable<ApplicationUser> ApplySorting(IQueryable<ApplicationUser> query, string? sortBy,
        bool isDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query.OrderByDescending(u => u.CreatedAt);

        return sortBy.ToLower() switch
        {
            "username" => isDescending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            "email" => isDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "createdat" => isDescending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            "firstname" => isDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            // Mẹo: Sort theo FullName bằng cách sort FirstName then LastName
            "fullname" => isDescending
                ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName)
                : query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };
    }

    public async Task<Response> GetUserDetailAsync(string userId, CancellationToken ct)
    {
        try
        {
            // 1. Lấy thông tin User
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Response.Error("User not found");
            // 2. Lấy Roles
            var roles = await _userManager.GetRolesAsync(user);
            // 3. Lấy số liệu thống kê (Dùng AsNoTracking để tối ưu đọc)
            var claimedPlayersCount = await _db.Players.AsNoTracking()
                .CountAsync(p => p.UserId == userId, ct);
            var tournamentsJoinedCount = await _db.TournamentPlayers.AsNoTracking()
                .Where(tp => tp.Player != null && tp.Player.UserId == userId)
                .Select(tp => tp.TournamentId)
                .Distinct()
                .CountAsync(ct);
            var postsCreatedCount = await _db.Posts.AsNoTracking()
                .CountAsync(p => p.UserId == userId, ct);
            var venuesCreatedCount = await _db.Venues.AsNoTracking()
                .CountAsync(v => v.CreatedByUserId == userId, ct);
            var tournamentsOrganizedCount = await _db.Tournaments.AsNoTracking()
                .CountAsync(t => t.OwnerUserId == userId, ct);
            // 4. Tính toán thời gian hoạt động (Activity)
            var lastTournamentDate = await _db.TournamentPlayers.AsNoTracking()
                .Where(tp => tp.Player != null && tp.Player.UserId == userId)
                .Select(tp => tp.Tournament.StartUtc)
                .DefaultIfEmpty() // Tránh lỗi nếu list rỗng
                .MaxAsync(ct);

            DateTime? lastTourTime = await _db.TournamentPlayers.AsNoTracking()
                .Where(tp => tp.Player != null && tp.Player.UserId == userId)
                .MaxAsync(tp => (DateTime?)tp.Tournament.StartUtc, ct);

            DateTime? lastPostTime = await _db.Posts.AsNoTracking()
                .Where(p => p.UserId == userId)
                .MaxAsync(p => (DateTime?)p.CreatedAt, ct);

            var lastActivityDate = user.CreatedAt;
            if (lastTourTime.HasValue && lastTourTime > lastActivityDate) lastActivityDate = lastTourTime.Value;
            if (lastPostTime.HasValue && lastPostTime > lastActivityDate) lastActivityDate = lastPostTime.Value;
            var daysSinceLastActivity = (int)(DateTime.UtcNow - lastActivityDate).TotalDays;
            var isActive = daysSinceLastActivity <= 30;
            var mainPlayer = await _db.Players.AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.FullName })
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);

            // 6. Map sang DTO
            var detailDto = new AdminUserDetailDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Nickname = user.Nickname,
                Country = user.Country,
                City = user.City,
                AvatarUrl = user.ProfilePicture,
                CreatedAt = user.CreatedAt,
                Roles = roles.ToList(),
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                AccessFailedCount = user.AccessFailedCount,
                TwoFactorEnabled = user.TwoFactorEnabled,
                LinkedPlayerId = mainPlayer?.Id,
                LinkedPlayerName = mainPlayer?.FullName,

                ActivityStats = new UserActivityStatsDto
                {
                    FailedLoginAttempts = user.AccessFailedCount,
                    LastActivityDate = lastActivityDate,
                    DaysSinceLastActivity = daysSinceLastActivity,
                    IsActive = isActive
                },
                RelatedData = new UserRelatedDataDto
                {
                    ClaimedPlayersCount = claimedPlayersCount,
                    TournamentsJoinedCount = tournamentsJoinedCount,
                    PostsCreatedCount = postsCreatedCount,
                    VenuesCreatedCount = venuesCreatedCount,
                    TournamentsOrganizedCount = tournamentsOrganizedCount
                }
            };

            return Response.Ok(detailDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user detail for userId={UserId}", userId);
            return Response.Error("Error fetching user detail");
        }
    }

    public async Task<Response> GetUserStatisticsAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);
            // Base query (Chưa chạy)
            var usersQuery = _db.Users.AsNoTracking();
            // 1. Tổng quan
            var totalUsers = await usersQuery.CountAsync(ct);

            var activeUsers = await usersQuery
                .CountAsync(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow, ct);

            var lockedUsers = await usersQuery
                .CountAsync(
                    u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow &&
                         u.LockoutEnd != DateTimeOffset.MaxValue, ct);

            var deactivatedUsers = await usersQuery
                .CountAsync(u => u.LockoutEnd == DateTimeOffset.MaxValue, ct);

            // 2. Email (Đã bỏ Phone)
            var emailConfirmedCount = await usersQuery.CountAsync(u => u.EmailConfirmed, ct);

            // 3. Hoạt động gần đây
            var usersCreatedToday = await usersQuery.CountAsync(u => u.CreatedAt >= today, ct);
            var usersCreatedLast7Days = await usersQuery.CountAsync(u => u.CreatedAt >= last7Days, ct);
            var usersCreatedLast30Days = await usersQuery.CountAsync(u => u.CreatedAt >= last30Days, ct);

            // 4. Phân bố Role 
            var roleDistData = await _db.UserRoles
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .GroupBy(name => name)
                .Select(g => new { RoleName = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            // 5. Phân bố địa lý (Country)
            var countryDistData = await usersQuery
                .Where(u => u.Country != null && u.Country != "")
                .GroupBy(u => u.Country)
                .Select(g => new { Country = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(ct);

            // 6. Tăng trưởng 6 tháng
            var sixMonthsAgo = now.AddMonths(-5).Date;
            var startOfSixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

            var monthlyGrowthData = await usersQuery
                .Where(u => u.CreatedAt >= startOfSixMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var roleDist = roleDistData.Select(x => new RoleDistributionDto
            {
                RoleName = x.RoleName ?? "Unknown",
                Count = x.Count,
                Percentage = totalUsers > 0 ? Math.Round((double)x.Count / totalUsers * 100, 2) : 0
            }).ToList();

            var countryDist = countryDistData.Select(x => new GeographicDistributionDto
            {
                Location = x.Country!,
                Count = x.Count,
                Percentage = totalUsers > 0 ? Math.Round((double)x.Count / totalUsers * 100, 2) : 0
            }).ToList();

            // Xử lý biểu đồ tăng trưởng
            var monthlyGrowth = new List<UserGrowthTrendDto>();
            var currentTotal = totalUsers;

            for (int i = 0; i < 6; i++)
            {
                var targetDate = now.AddMonths(-i);
                var monthItem = monthlyGrowthData
                    .FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);

                var newUsersCount = monthItem?.Count ?? 0;

                monthlyGrowth.Add(new UserGrowthTrendDto
                {
                    Year = targetDate.Year,
                    Month = targetDate.Month,
                    MonthName = targetDate.ToString("MMM yyyy"),
                    NewUsers = newUsersCount,
                    TotalUsers = currentTotal
                });

                currentTotal -= newUsersCount;
            }

            monthlyGrowth.Reverse();
            var statistics = new UserStatisticsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                LockedUsers = lockedUsers,
                DeactivatedUsers = deactivatedUsers,
                EmailConfirmedUsers = emailConfirmedCount,
                EmailUnconfirmedUsers = totalUsers - emailConfirmedCount,
                EmailConfirmationRate = totalUsers > 0
                    ? Math.Round((double)emailConfirmedCount / totalUsers * 100, 2)
                    : 0,
                UsersCreatedLast7Days = usersCreatedLast7Days,
                UsersCreatedLast30Days = usersCreatedLast30Days,
                UsersCreatedToday = usersCreatedToday,

                RoleDistribution = roleDist,
                TopCountries = countryDist,
                MonthlyGrowth = monthlyGrowth
            };

            return Response.Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user statistics");
            return Response.Error("Error fetching user statistics");
        }
    }

    public async Task<Response> GetUserActivityLogAsync(string userId, CancellationToken ct)
    {
        try
        {
            // 1. Check User
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // 2. Khởi tạo Query Base (Chưa chạy)
            var playersQuery = _db.Players.AsNoTracking().Where(p => p.UserId == userId);
            var postsQuery = _db.Posts.AsNoTracking().Where(p => p.UserId == userId);
            var tournamentsQuery = _db.TournamentPlayers.AsNoTracking()
                .Where(tp => tp.Player != null && tp.Player.UserId == userId);

            // 3. THỰC THI TUẦN TỰ (Bỏ Task.WhenAll)
            // A. Đếm số lượng
            var countPlayers = await playersQuery.CountAsync(ct);
            var countPosts = await postsQuery.CountAsync(ct);
            var countTournaments = await tournamentsQuery
                .Select(tp => tp.TournamentId)
                .Distinct()
                .CountAsync(ct);

            // B. Lấy ngày hoạt động gần nhất
            var lastTournamentDate = await tournamentsQuery
                .MaxAsync(tp => (DateTime?)tp.Tournament.StartUtc, ct);

            var lastPostDate = await postsQuery
                .MaxAsync(p => (DateTime?)p.CreatedAt, ct);

            // C. Lấy danh sách chi tiết (Top 10)
            var recentTournaments = await tournamentsQuery
                .Include(tp => tp.Tournament)
                .OrderByDescending(tp => tp.Tournament.StartUtc)
                .Take(10)
                .Select(tp => new ActivityEntryDto
                {
                    ActivityType = "Tournament",
                    Description = $"Joined tournament: {tp.Tournament.Name}",
                    ActivityDate = tp.Tournament.StartUtc,
                    RelatedEntityId = tp.Tournament.Id.ToString(),
                    RelatedEntityName = tp.Tournament.Name
                })
                .ToListAsync(ct);

            var recentPosts = await postsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new ActivityEntryDto
                {
                    ActivityType = "Post",
                    Description = p.Content != null && p.Content.Length > 50
                        ? "Created post: " + p.Content.Substring(0, 50) + "..."
                        : "Created post: " + (p.Content ?? "No content"),
                    ActivityDate = p.CreatedAt,
                    RelatedEntityId = p.Id.ToString(),
                    RelatedEntityName = p.Content ?? "Post"
                })
                .ToListAsync(ct);

            // 4. Tổng hợp dữ liệu 
            var activityDates = new[] { lastTournamentDate, lastPostDate, user.CreatedAt };
            var lastActivityAt = activityDates.Where(d => d.HasValue).Max();
            var allActivities = new List<ActivityEntryDto>();
            allActivities.AddRange(recentTournaments);
            allActivities.AddRange(recentPosts);

            var finalActivities = allActivities
                .OrderByDescending(a => a.ActivityDate)
                .Take(20)
                .ToList();

            // 5. Map kết quả
            var activitySummary = new UserActivitySummaryDto
            {
                TotalPlayers = countPlayers,
                TotalTournaments = countTournaments,
                TotalPosts = countPosts,
                AccountCreatedAt = user.CreatedAt,
                LockoutEnd = user.LockoutEnd?.DateTime,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
            };

            return Response.Ok(new UserActivityLogDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                ActivitySummary = activitySummary,
                RecentActivities = finalActivities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user activity log for userId={UserId}", userId);
            return Response.Error($"Error fetching user activity log: {ex.Message}");
        }
    }


    public async Task<Response> DeactivateUserAsync(string userId, string adminId, CancellationToken ct)
    {
        try
        {
            // ═══════════════════════════════════════════════════════════════════
            // CHECK 1: SELF-BAN PREVENTION
            // Admin không được tự khóa chính mình
            // ═══════════════════════════════════════════════════════════════════
            if (userId == adminId)
            {
                _logger.LogWarning("Admin {AdminId} attempted to deactivate themselves.", adminId);
                return Response.Error("You cannot deactivate your own account.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // ═══════════════════════════════════════════════════════════════════
            // CHECK 2: ALREADY LOCKED OUT
            // ═══════════════════════════════════════════════════════════════════
            var isAlreadyLockedOut = await _userManager.IsLockedOutAsync(user);
            if (isAlreadyLockedOut)
            {
                return Response.Error("User is already deactivated");
            }

            // ═══════════════════════════════════════════════════════════════════
            // CHECK 3: ROLE-BASED PROTECTION (STRICT MODE)
            // Logic: Nếu target là Admin -> CHẶN TUYỆT ĐỐI.
            // Không sử dụng Force flag ở đây. Để khóa Admin, cần quy trình khác
            // hoặc thao tác trực tiếp vào DB để đảm bảo an toàn.
            // ═══════════════════════════════════════════════════════════════════
            var userRoles = await _userManager.GetRolesAsync(user);

            // Kiểm tra xem user có phải là Admin không
            var isAdmin = userRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                _logger.LogWarning(
                    "Attempt to deactivate protected Admin account: User {UserId} ({UserName}). Request denied.",
                    userId, user.UserName);

                // Trả về lỗi ngay lập tức
                return Response.Error(
                    "Cannot deactivate this user. Admin accounts are protected and cannot be deactivated via this API.");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 1: ENSURE LOCKOUT IS ENABLED
            // ═══════════════════════════════════════════════════════════════════
            if (!user.LockoutEnabled)
            {
                var enableLockoutResult = await _userManager.SetLockoutEnabledAsync(user, true);
                if (!enableLockoutResult.Succeeded)
                {
                    var errors = string.Join(", ", enableLockoutResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to enable lockout for user {UserId}: {Errors}", userId, errors);
                    return Response.Error($"Failed to prepare user for deactivation: {errors}");
                }

                _logger.LogInformation("Enabled lockout for user {UserId} before deactivation.", userId);
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 2: SET LOCKOUT END DATE (Permanent)
            // ═══════════════════════════════════════════════════════════════════
            var setLockoutResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!setLockoutResult.Succeeded)
            {
                var errors = string.Join(", ", setLockoutResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to set lockout end date for user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to deactivate user: {errors}");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 3: UPDATE SECURITY STAMP (Invalidate Tokens)
            // ═══════════════════════════════════════════════════════════════════
            var updateStampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!updateStampResult.Succeeded)
            {
                var errors = string.Join(", ", updateStampResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update security stamp for user {UserId}: {Errors}", userId, errors);
                // Note: Continue - cache ban will still provide real-time protection
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 4: REAL-TIME CACHE BAN
            // ═══════════════════════════════════════════════════════════════════
            var cacheResult = await _bannedUserCache.BanUserAsync(
                userId,
                $"Deactivated by Admin {adminId}",
                ct);

            if (!cacheResult)
            {
                _logger.LogWarning(
                    "Failed to add user {UserId} to banned cache. Real-time lockout may be delayed.",
                    userId);
            }
            else
            {
                _logger.LogInformation("User {UserId} added to real-time ban cache.", userId);
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 5: SIGNALR NOTIFICATION (Real-time UX)
            // ═══════════════════════════════════════════════════════════════════
            var signalRNotified = false;
            try
            {
                await _hubContext.Clients.User(userId).SendAsync(
                    AppHubEvents.ReceiveLogoutCommand,
                    new
                    {
                        message = "Your account has been deactivated by an administrator.",
                        reason = "Account deactivation",
                        timestamp = DateTimeOffset.UtcNow,
                        requireLogout = true
                    },
                    ct); // Đừng quên truyền CancellationToken

                signalRNotified = true;
                _logger.LogInformation("SignalR logout command sent to user {UserId}.", userId);
            }
            catch (Exception signalREx)
            {
                _logger.LogWarning(signalREx, "Failed to send SignalR logout command to user {UserId}.", userId);
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 6: LOG SUCCESS AND RETURN
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogInformation(
                "User {UserId} ({UserName}) deactivated. Cache={CacheActive}, DB=Active, SignalR={SignalRNotified}",
                userId, user.UserName, cacheResult, signalRNotified);

            return Response.Ok(new
            {
                message = "User deactivated successfully. Account is blocked immediately.",
                userId = userId,
                userName = user.UserName,
                userRoles = userRoles,
                deactivatedAt = DateTimeOffset.UtcNow,
                deactivatedBy = adminId,
                lockoutEnd = DateTimeOffset.MaxValue,
                realTimeBanActive = cacheResult,
                signalRNotified = signalRNotified
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            return Response.Error("Error deactivating user");
        }
    }


    public async Task<Response> ReactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            // ═══════════════════════════════════════════════════════════════════
            // STEP 1: FIND USER
            // ═══════════════════════════════════════════════════════════════════
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 2: CHECK IF USER IS ACTUALLY LOCKED OUT
            // 
            // ✅ Using Standard Identity API instead of manual date check
            // IsLockedOutAsync properly checks both LockoutEnabled AND LockoutEnd
            // ═══════════════════════════════════════════════════════════════════
            var isCurrentlyLockedOut = await _userManager.IsLockedOutAsync(user);
            if (!isCurrentlyLockedOut)
            {
                return Response.Error("User is not currently deactivated");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 3: GET ROLES FOR LOGGING (Before any updates)
            // ═══════════════════════════════════════════════════════════════════
            var roles = await _userManager.GetRolesAsync(user);
            var previousLockoutEnd = user.LockoutEnd;
            var previousAccessFailedCount = user.AccessFailedCount;

            // ═══════════════════════════════════════════════════════════════════
            // STEP 4: UNLOCK USER (Using Standard Identity API)
            // 
            // ✅ SetLockoutEndDateAsync(user, null) is the proper way to unlock
            // - Handles all internal validations and triggers
            // - Sets LockoutEnd to null, effectively removing the lockout
            // ═══════════════════════════════════════════════════════════════════
            var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!unlockResult.Succeeded)
            {
                var errors = string.Join(", ", unlockResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to unlock user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to reactivate user: {errors}");
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 5: RESET ACCESS FAILED COUNT (Critical for Clean Slate)
            // 
            // ⚠️ WHY THIS IS CRUCIAL:
            // - If user had failed login attempts before being banned, 
            //   AccessFailedCount might be close to the lockout threshold
            // - Without reset, user could be immediately locked out again
            //   on their first failed login attempt after reactivation
            // - ResetAccessFailedCountAsync sets count to 0
            // ═══════════════════════════════════════════════════════════════════
            var resetCountResult = await _userManager.ResetAccessFailedCountAsync(user);
            if (!resetCountResult.Succeeded)
            {
                var errors = string.Join(", ", resetCountResult.Errors.Select(e => e.Description));
                _logger.LogWarning(
                    "Failed to reset access failed count for user {UserId}: {Errors}. " +
                    "User may still be at risk of immediate re-lockout.",
                    userId, errors);
                // Note: Don't fail the operation - lockout is already removed
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 6: UPDATE SECURITY STAMP (Invalidate all tokens)
            // 
            // ✅ WHY THIS IS IMPORTANT:
            // - Forces user to re-authenticate with fresh credentials
            // - Invalidates any tokens that might have been issued during lockout
            // - Ensures clean session state
            // ═══════════════════════════════════════════════════════════════════
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                var errors = string.Join(", ", stampResult.Errors.Select(e => e.Description));
                _logger.LogWarning(
                    "Failed to update security stamp for user {UserId}: {Errors}. " +
                    "Old tokens may remain valid until expiration.",
                    userId, errors);
                // Note: Don't fail - lockout is already removed
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 7: REMOVE FROM BANNED CACHE (Real-time unban)
            // 
            // ✅ WHY THIS IS ESSENTIAL:
            // - BannedUserMiddleware checks this cache on every request
            // - Without removal, user would still be blocked at middleware level
            // - Must be removed for immediate access restoration
            // ═══════════════════════════════════════════════════════════════════
            var cacheRemoved = await _bannedUserCache.UnbanUserAsync(userId, ct);
            if (!cacheRemoved)
            {
                _logger.LogWarning(
                    "Failed to remove user {UserId} from banned cache. " +
                    "User may need to wait for cache TTL to expire.",
                    userId);
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 8: LOG SUCCESS AND RETURN
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogInformation(
                "User {UserId} ({UserName}, roles: [{Roles}]) has been reactivated successfully. " +
                "Previous state: LockoutEnd={PreviousLockout}, AccessFailedCount={PreviousFailedCount}. " +
                "Current state: Unlocked, AccessFailedCount=0, SecurityStamp={StampUpdated}, CacheCleared={CacheCleared}",
                userId, user.UserName, string.Join(", ", roles),
                previousLockoutEnd, previousAccessFailedCount,
                stampResult.Succeeded, cacheRemoved);

            return Response.Ok(new
            {
                message = "User reactivated successfully. User must login with fresh credentials.",
                userId = userId,
                userName = user.UserName,
                userRoles = roles,
                reactivatedAt = DateTimeOffset.UtcNow,
                previousState = new
                {
                    lockoutEnd = previousLockoutEnd,
                    accessFailedCount = previousAccessFailedCount
                },
                currentState = new
                {
                    lockoutEnd = (DateTimeOffset?)null,
                    accessFailedCount = 0,
                    securityStampUpdated = stampResult.Succeeded,
                    cacheCleared = cacheRemoved
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user {UserId}", userId);
            return Response.Error("Error reactivating user");
        }
    }

    public async Task<Response> BulkDeactivateUsersAsync(BulkDeactivateUsersDto request, string adminId,
        CancellationToken ct)
    {
        try
        {
            var results = new List<BulkOperationItemResultDto>();
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var userId in request.UserIds)
            {
                try
                {
                    // ═══════════════════════════════════════════════════════════════
                    // CHECK 1: SELF-BAN PREVENTION (Giữ nguyên)
                    // Admin không được phép tự khóa chính mình
                    // ═══════════════════════════════════════════════════════════════
                    if (userId == adminId)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            Success = false,
                            ErrorMessage = "Cannot deactivate your own account", // Thông báo lỗi rõ ràng
                            Status = "Skipped"
                        });
                        skippedCount++;
                        _logger.LogWarning("Admin {AdminId} attempted to deactivate themselves in bulk operation.",
                            adminId);
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // CHECK 2: USER EXISTS
                    // ═══════════════════════════════════════════════════════════════
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            Success = false,
                            ErrorMessage = "User not found",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // CHECK 3: ALREADY LOCKED OUT
                    // ═══════════════════════════════════════════════════════════════
                    var isAlreadyLockedOut = await _userManager.IsLockedOutAsync(user);
                    if (isAlreadyLockedOut)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = "User is already deactivated",
                            Status = "Skipped"
                        });
                        skippedCount++;
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // CHECK 4: ROLE-BASED PROTECTION (Đã loại bỏ Force Flag)
                    // 
                    // Logic mới (An toàn tuyệt đối):
                    // - Nếu target là Admin -> Luôn luôn BỎ QUA (Skipped).
                    // - Không quan tâm cờ Force nữa.
                    // - Lý do: Chức năng Bulk rất dễ bấm nhầm, nên cấm tuyệt đối việc
                    //   khóa Admin ở đây. Nếu muốn khóa Admin, phải dùng chức năng Single.
                    // ═══════════════════════════════════════════════════════════════
                    var userRoles = await _userManager.GetRolesAsync(user);
                    var isAdmin = userRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

                    if (isAdmin)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage =
                                "Skipped: Target is Admin. Admin accounts cannot be deactivated in bulk mode.",
                            Status = "Skipped"
                        });
                        skippedCount++;
                        _logger.LogWarning("Bulk deactivate skipped Admin user {UserId}. Protected by role.", userId);
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 1: ENSURE LOCKOUT IS ENABLED
                    // ═══════════════════════════════════════════════════════════════
                    if (!user.LockoutEnabled)
                    {
                        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
                        if (!enableResult.Succeeded)
                        {
                            var errors = string.Join(", ", enableResult.Errors.Select(e => e.Description));
                            results.Add(new BulkOperationItemResultDto
                            {
                                UserId = userId,
                                UserName = user.UserName,
                                Success = false,
                                ErrorMessage = $"Failed to enable lockout: {errors}",
                                Status = "Failed"
                            });
                            failedCount++;
                            continue;
                        }
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 2: SET LOCKOUT END DATE
                    // ═══════════════════════════════════════════════════════════════
                    var lockoutResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                    if (!lockoutResult.Succeeded)
                    {
                        var errors = string.Join(", ", lockoutResult.Errors.Select(e => e.Description));
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = $"Failed to set lockout: {errors}",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 3: UPDATE SECURITY STAMP
                    // ═══════════════════════════════════════════════════════════════
                    var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                    // Note: Log warning if stamp update fails, but proceed because DB lockout is active
                    if (!stampResult.Succeeded)
                    {
                        _logger.LogWarning("Failed to update security stamp for user {UserId}", userId);
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 4: REAL-TIME CACHE BAN
                    // ═══════════════════════════════════════════════════════════════
                    var cacheSuccess = false;
                    try
                    {
                        cacheSuccess = await _bannedUserCache.BanUserAsync(
                            userId,
                            $"Bulk deactivation by Admin {adminId}. Reason: {request.Reason ?? "No reason provided"}",
                            ct);
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning(cacheEx, "Exception adding user {UserId} to ban cache.", userId);
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 5: SIGNALR NOTIFICATION
                    // ═══════════════════════════════════════════════════════════════
                    var signalRSuccess = false;
                    try
                    {
                        await _hubContext.Clients.User(userId).SendAsync(
                            AppHubEvents.ReceiveLogoutCommand,
                            new
                            {
                                message = "Your account has been deactivated by an administrator.",
                                reason = request.Reason ?? "Account deactivation",
                                timestamp = DateTimeOffset.UtcNow,
                                requireLogout = true
                            },
                            ct);
                        signalRSuccess = true;
                    }
                    catch (Exception signalREx)
                    {
                        _logger.LogWarning(signalREx, "Failed to send SignalR logout command to user {UserId}.",
                            userId);
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // SUCCESS
                    // ═══════════════════════════════════════════════════════════════
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        UserName = user.UserName,
                        Success = true,
                        Status = "Success"
                    });
                    successCount++;

                    _logger.LogInformation(
                        "User {UserId} ({UserName}) deactivated via bulk. Cache={CacheSuccess}, SignalR={SignalRSuccess}",
                        userId, user.UserName, cacheSuccess, signalRSuccess);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deactivating user {UserId} in bulk operation", userId);
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        Success = false,
                        ErrorMessage = "Internal error",
                        Status = "Failed"
                    });
                    failedCount++;
                }
            }

            var bulkResult = new BulkDeactivateResultDto
            {
                TotalRequested = request.UserIds.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                SkippedCount = skippedCount,
                Reason = request.Reason,
                Results = results,
                ProcessedAt = DateTime.UtcNow
            };

            return Response.Ok(bulkResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk deactivate operation");
            return Response.Error("Error processing bulk deactivate operation");
        }
    }

    public async Task<Response> BulkReactivateUsersAsync(BulkReactivateUsersDto request, CancellationToken ct)
    {
        try
        {
            var results = new List<BulkOperationItemResultDto>();
            var successCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var userId in request.UserIds)
            {
                try
                {
                    // ═══════════════════════════════════════════════════════════════
                    // STEP 1: FIND USER
                    // ═══════════════════════════════════════════════════════════════
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            Success = false,
                            ErrorMessage = "User not found",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 2: CHECK IF ACTUALLY LOCKED OUT (Standard Identity API)
                    // ═══════════════════════════════════════════════════════════════
                    var isCurrentlyLockedOut = await _userManager.IsLockedOutAsync(user);
                    if (!isCurrentlyLockedOut)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = "User is not currently deactivated",
                            Status = "Skipped"
                        });
                        skippedCount++;
                        continue;
                    }

                    // Save previous state for logging
                    var previousLockoutEnd = user.LockoutEnd;
                    var previousAccessFailedCount = user.AccessFailedCount;

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 3: UNLOCK USER (Standard Identity API)
                    // ═══════════════════════════════════════════════════════════════
                    var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
                    if (!unlockResult.Succeeded)
                    {
                        var errors = string.Join(", ", unlockResult.Errors.Select(e => e.Description));
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = $"Failed to unlock: {errors}",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 4: RESET ACCESS FAILED COUNT (Clean Slate)
                    // 
                    // ⚠️ CRUCIAL: Without this, user with previous failed attempts
                    // could be immediately re-locked on first failed login
                    // ═══════════════════════════════════════════════════════════════
                    var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
                    if (!resetResult.Succeeded)
                    {
                        _logger.LogWarning(
                            "Failed to reset access failed count for user {UserId} in bulk operation. " +
                            "User may be at risk of immediate re-lockout.",
                            userId);
                        // Note: Don't fail - lockout is already removed
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 5: UPDATE SECURITY STAMP (Invalidate tokens)
                    // ═══════════════════════════════════════════════════════════════
                    var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                    if (!stampResult.Succeeded)
                    {
                        _logger.LogWarning(
                            "Failed to update security stamp for user {UserId} in bulk operation. " +
                            "Old tokens may remain valid.",
                            userId);
                        // Note: Don't fail - lockout is already removed
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // STEP 6: REMOVE FROM BANNED CACHE (Best Effort)
                    // 
                    // Wrapped in try-catch: Cache failure should NOT fail reactivation
                    // DB is already updated, user can login after cache TTL expires
                    // ═══════════════════════════════════════════════════════════════
                    var cacheCleared = false;
                    try
                    {
                        cacheCleared = await _bannedUserCache.UnbanUserAsync(userId, ct);
                        if (!cacheCleared)
                        {
                            _logger.LogWarning(
                                "Failed to remove user {UserId} from banned cache in bulk operation. " +
                                "User may need to wait for cache TTL to expire.",
                                userId);
                        }
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning(cacheEx,
                            "Exception removing user {UserId} from banned cache. " +
                            "DB reactivation succeeded, cache will expire naturally.",
                            userId);
                        // Don't fail - DB update is successful
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // SUCCESS
                    // ═══════════════════════════════════════════════════════════════
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        UserName = user.UserName,
                        Success = true,
                        Status = "Success"
                    });
                    successCount++;

                    _logger.LogInformation(
                        "User {UserId} ({UserName}) reactivated via bulk operation. " +
                        "Previous: LockoutEnd={PrevLockout}, FailedCount={PrevFailed}. " +
                        "Current: Unlocked, FailedCount=0, CacheCleared={CacheCleared}. Reason: {Reason}",
                        userId, user.UserName,
                        previousLockoutEnd, previousAccessFailedCount,
                        cacheCleared, request.Reason ?? "No reason provided");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reactivating user {UserId} in bulk operation", userId);
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        Success = false,
                        ErrorMessage = "Internal error during reactivation",
                        Status = "Failed"
                    });
                    failedCount++;
                }
            }

            var bulkResult = new BulkReactivateResultDto
            {
                TotalRequested = request.UserIds.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                SkippedCount = skippedCount,
                Reason = request.Reason,
                Results = results,
                ProcessedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Bulk reactivate completed. Total={Total}, Success={Success}, Failed={Failed}, Skipped={Skipped}",
                request.UserIds.Count, successCount, failedCount, skippedCount);

            return Response.Ok(bulkResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk reactivate operation");
            return Response.Error("Error processing bulk reactivate operation");
        }
    }

    public async Task<Response> ExportUsersAsync(AdminUserFilterDto filter, CancellationToken ct)
    {
        try
        {
            // 1. Khởi tạo Query
            var query = _db.Users.AsNoTracking().AsQueryable();

            // 2. Áp dụng bộ lọc
            query = ApplyUserFilters(query, filter);

            // 3. Lấy dữ liệu User 
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync(ct);

            // 4. TỐI ƯU HIỆU NĂNG: Lấy Roles (Batch Query)
            var userIds = users.Select(u => u.Id).ToList();
            var userRolesMap = await _db.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
                .ToListAsync(ct);

            var rolesLookup = userRolesMap
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            // 5. Tạo CSV
            var csv = new StringBuilder();
            csv.Append('\uFEFF');

            csv.AppendLine("sep=,");

            csv.AppendLine(
                "UserId,UserName,Email,EmailConfirmed,PhoneNumber,PhoneConfirmed,FullName,Nickname,Country,City,Roles,CreatedAt,IsLockedOut,LockoutEnd");

            foreach (var user in users)
            {
                var roles = rolesLookup.ContainsKey(user.Id)
                    ? string.Join(";", rolesLookup[user.Id])
                    : "";

                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

                csv.AppendLine(
                    $"{user.Id}," +
                    $"{EscapeCsv(user.UserName)}," +
                    $"{EscapeCsv(user.Email)}," +
                    $"{user.EmailConfirmed}," +
                    $"{EscapeCsv(user.PhoneNumber)}," +
                    $"{user.PhoneNumberConfirmed}," +
                    $"{EscapeCsv(fullName)}," +
                    $"{EscapeCsv(user.Nickname)}," +
                    $"{EscapeCsv(user.Country)}," +
                    $"{EscapeCsv(user.City)}," +
                    $"{EscapeCsv(roles)}," + 
                    $"{user.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                    $"{isLockedOut}," +
                    $"{EscapeCsv(user.LockoutEnd?.ToString("yyyy-MM-dd HH:mm:ss"))}"
                );
            }

            var exportResult = new FileExportDto
            {
                FileName = $"users_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                ContentType = "text/csv",
                Content = csv.ToString()
            };

            return Response.Ok(exportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting users");
            return Response.Error("Error exporting users");
        }
    }
    
    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        if (value.Contains(",") || value.Contains("\""))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }

    private IQueryable<ApplicationUser> ApplyUserFilters(IQueryable<ApplicationUser> query, AdminUserFilterDto filter)
    {
        // 1. Search
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                (u.Nickname != null && u.Nickname.ToLower().Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term))
            );
        }

        // 2. Filters khác
        if (filter.EmailConfirmed.HasValue)
            query = query.Where(u => u.EmailConfirmed == filter.EmailConfirmed.Value);

        if (filter.PhoneNumberConfirmed.HasValue)
            query = query.Where(u => u.PhoneNumberConfirmed == filter.PhoneNumberConfirmed.Value);

        if (filter.LockoutEnabled.HasValue)
            query = query.Where(u => u.LockoutEnabled == filter.LockoutEnabled.Value);

        if (filter.IsLockedOut.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (filter.IsLockedOut.Value)
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            else
                query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(u => u.Country == filter.Country);

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(u => u.City == filter.City);

        if (filter.CreatedFrom.HasValue)
            query = query.Where(u => u.CreatedAt >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
        {
            var toDate = filter.CreatedTo.Value.AddDays(1);
            query = query.Where(u => u.CreatedAt < toDate);
        }

        return query;
    }
}