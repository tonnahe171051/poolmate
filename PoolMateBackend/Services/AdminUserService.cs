using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
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

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db,
        ILogger<AdminUserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
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
                LastActivityAt = lastActivityAt ?? user.CreatedAt,
                FailedLoginAttempts = user.AccessFailedCount,
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

    public async Task<Response> DeactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // 1. Kiểm tra xem user đã bị khóa trước đó chưa
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Response.Error("User is already deactivated");
            }

            // 2. Bảo vệ tài khoản VIP/Admin (nếu LockoutEnabled = false)
            if (!user.LockoutEnabled)
            {
                _logger.LogWarning(
                    "Attempt to deactivate protected account: User {UserId} ({UserName}). Request denied.",
                    userId, user.UserName);
                return Response.Error(
                    "Cannot deactivate this user. This is a protected account with lockout protection enabled.");
            }

            // 3. THỰC HIỆN KHÓA
            user.LockoutEnd = DateTimeOffset.MaxValue;
            var result = await _userManager.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to deactivate user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to deactivate user: {errors}");
            }

            // 4. Log & Return
            var roles = await _userManager.GetRolesAsync(user);
            _logger.LogInformation(
                "User {UserId} ({UserName}) has been deactivated successfully. Security stamp updated.",
                userId, user.UserName);

            return Response.Ok(new
            {
                message = "User deactivated successfully. All active sessions have been invalidated.",
                userId = userId,
                userName = user.UserName,
                deactivatedAt = DateTimeOffset.UtcNow
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
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // 1. Kiểm tra user có đang bị deactivate không
            if (!user.LockoutEnd.HasValue || user.LockoutEnd.Value <= DateTimeOffset.UtcNow)
            {
                return Response.Error("User is not currently deactivated");
            }

            // 2. Lấy roles để log (trước khi update)
            var roles = await _userManager.GetRolesAsync(user);
            // 3. THỰC HIỆN MỞ KHÓA
            user.LockoutEnd = null;
            var result = await _userManager.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to reactivate user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to reactivate user: {errors}");
            }

            // 4. Log & Return
            _logger.LogInformation(
                "User {UserId} ({UserName}, roles: {Roles}) has been reactivated successfully. Security stamp refreshed.",
                userId, user.UserName, string.Join(", ", roles));

            return Response.Ok(new
            {
                message = "User reactivated successfully. User needs to login again.",
                userId = userId,
                userName = user.UserName,
                reactivatedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user {UserId}", userId);
            return Response.Error("Error reactivating user");
        }
    }

    public async Task<Response> BulkDeactivateUsersAsync(BulkDeactivateUsersDto request, CancellationToken ct)
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

                    // 1. Check: Đã bị khóa chưa?
                    if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
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

                    // 2. Check: Tài khoản VIP (Protected)
                    if (!user.LockoutEnabled && !request.Force)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = "Protected account - use Force option to deactivate",
                            Status = "Skipped"
                        });
                        skippedCount++;
                        continue;
                    }

                    // 3. THỰC HIỆN KHÓA (REAL-TIME)
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    var result = await _userManager.UpdateSecurityStampAsync(user);

                    if (result.Succeeded)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = true,
                            Status = "Success"
                        });
                        successCount++;

                        _logger.LogInformation("User {UserId} deactivated via bulk operation.", userId);
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = errors,
                            Status = "Failed"
                        });
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deactivating user {UserId}", userId);
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

                    if (!user.LockoutEnd.HasValue || user.LockoutEnd.Value <= DateTimeOffset.UtcNow)
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

                    user.LockoutEnd = null;
                    var result = await _userManager.UpdateSecurityStampAsync(user);

                    if (result.Succeeded)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = true,
                            Status = "Success"
                        });
                        successCount++;

                        _logger.LogInformation(
                            "User {UserId} ({UserName}) reactivated via bulk operation. Reason: {Reason}",
                            userId, user.UserName, request.Reason ?? "No reason provided");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = false,
                            ErrorMessage = errors,
                            Status = "Failed"
                        });
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reactivating user {UserId} in bulk operation", userId);
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        Success = false,
                        ErrorMessage = ex.Message,
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
            // 2. Áp dụng bộ lọc (Tái sử dụng code)
            query = ApplyUserFilters(query, filter);
            // 3. Lấy dữ liệu User 
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync(ct);
            // 4. 🚀 TỐI ƯU HIỆU NĂNG: Lấy Roles (Batch Query)
            var userIds = users.Select(u => u.Id).ToList();
            var userRolesMap = await _db.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
                .ToListAsync(ct);

            // Gom nhóm roles theo UserId để dễ tra cứu
            var rolesLookup = userRolesMap
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            // 5. Tạo CSV
            var csv = new StringBuilder();
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

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains("\"")) value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
}