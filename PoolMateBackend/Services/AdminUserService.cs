using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;
using System.Text;

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

    /// <summary>
    /// GET ALL USERS với phân trang, filter, search, sort
    /// </summary>
    public async Task<Response> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct)
    {
        try
        {
            // BƯỚC 1: Base query
            var query = _userManager.Users.AsQueryable();

            // BƯỚC 2: SEARCH - Tìm theo username, email, name
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower().Trim();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                    (u.Nickname != null && u.Nickname.ToLower().Contains(searchLower))
                );
            }

            // BƯỚC 3: FILTERS (dựa trên các trường CÓ SẴN trong AspNetUsers)
            
            // Filter theo EmailConfirmed
            if (filter.EmailConfirmed.HasValue)
            {
                query = query.Where(u => u.EmailConfirmed == filter.EmailConfirmed.Value);
            }


            if (filter.PhoneNumberConfirmed.HasValue)
            {
                query = query.Where(u => u.PhoneNumberConfirmed == filter.PhoneNumberConfirmed.Value);
            }


            if (filter.TwoFactorEnabled.HasValue)
            {
                query = query.Where(u => u.TwoFactorEnabled == filter.TwoFactorEnabled.Value);
            }


            if (filter.LockoutEnabled.HasValue)
            {
                query = query.Where(u => u.LockoutEnabled == filter.LockoutEnabled.Value);
            }


            if (filter.IsLockedOut.HasValue)
            {
                if (filter.IsLockedOut.Value)
                {
                    // Users đang bị locked (LockoutEnd > now)
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                }
                else
                {
                    // Users không bị locked
                    query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
                }
            }


            if (!string.IsNullOrWhiteSpace(filter.Country))
            {
                query = query.Where(u => u.Country == filter.Country);
            }


            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                query = query.Where(u => u.City == filter.City);
            }

            // Filter by Role (requires join with UserRoles table)
            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                // Get role ID
                var role = await _roleManager.FindByNameAsync(filter.Role);
                if (role != null)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(filter.Role);
                    var userIdsInRole = usersInRole.Select(u => u.Id).ToList();
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
                else
                {
                    // Role không tồn tại, trả về empty
                    query = query.Where(u => false);
                }
            }


            if (filter.CreatedFrom.HasValue)
            {
                query = query.Where(u => u.CreatedAt >= filter.CreatedFrom.Value);
            }

            if (filter.CreatedTo.HasValue)
            {
                // Add 1 day để include cả ngày CreatedTo
                var toDate = filter.CreatedTo.Value.AddDays(1);
                query = query.Where(u => u.CreatedAt < toDate);
            }


            query = ApplySorting(query, filter.SortBy, filter.IsDescending);


            var totalRecords = await query.CountAsync(ct);


            var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
            var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

            var users = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);


            var userDtos = new List<AdminUserListDto>();
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                
                userDtos.Add(new AdminUserListDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Nickname = user.Nickname,
                    Country = user.Country,
                    City = user.City,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnd = user.LockoutEnd?.DateTime,
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    LockoutEnabled = user.LockoutEnabled,
                    CreatedAt = user.CreatedAt,
                    Roles = roles.ToList(),
                    AvatarUrl = user.ProfilePicture
                });
            }


            // Tạo PagingList response
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
            return Response.Error("Error fetching users list");
        }
    }

    /// <summary>
    /// Helper method để apply sorting logic
    /// </summary>
    private IQueryable<ApplicationUser> ApplySorting(
        IQueryable<ApplicationUser> query, 
        string? sortBy, 
        bool isDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            // Default: sort by CreatedAt descending
            return query.OrderByDescending(u => u.CreatedAt);
        }

        return sortBy.ToLower() switch
        {
            "username" => isDescending
                ? query.OrderByDescending(u => u.UserName)
                : query.OrderBy(u => u.UserName),

            "email" => isDescending
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),

            "createdat" => isDescending
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),

            "firstname" => isDescending
                ? query.OrderByDescending(u => u.FirstName)
                : query.OrderBy(u => u.FirstName),

            "lastname" => isDescending
                ? query.OrderByDescending(u => u.LastName)
                : query.OrderBy(u => u.LastName),

            "country" => isDescending
                ? query.OrderByDescending(u => u.Country)
                : query.OrderBy(u => u.Country),

            "city" => isDescending
                ? query.OrderByDescending(u => u.City)
                : query.OrderBy(u => u.City),

            "lockoutstatus" => isDescending
                ? query.OrderByDescending(u => u.LockoutEnd)
                : query.OrderBy(u => u.LockoutEnd),

            _ => query.OrderByDescending(u => u.CreatedAt) // Default
        };
    }

    /// <summary>
    /// GET USER DETAIL - Lấy thông tin chi tiết của 1 user với full context
    /// </summary>
    public async Task<Response> GetUserDetailAsync(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return Response.Error("User not found");
            }

            // Lấy roles
            var roles = await _userManager.GetRolesAsync(user);

            // Lấy related data counts
            var claimedPlayersCount = await _db.Players
                .Where(p => p.UserId == userId)
                .CountAsync(ct);

            var tournamentsJoinedCount = await _db.TournamentPlayers
                .Where(tp => tp.Player.UserId == userId)
                .Select(tp => tp.TournamentId)
                .Distinct()
                .CountAsync(ct);

            var postsCreatedCount = await _db.Posts
                .Where(p => p.UserId == userId)
                .CountAsync(ct);

            var venuesCreatedCount = await _db.Venues
                .Where(v => v.CreatedByUserId == userId)
                .CountAsync(ct);

            var tournamentsOrganizedCount = await _db.Tournaments
                .Where(t => t.OwnerUserId == userId)
                .CountAsync(ct);

            // Calculate activity stats
            var lastTournamentDate = await _db.TournamentPlayers
                .Where(tp => tp.Player != null && tp.Player.UserId == userId)
                .OrderByDescending(tp => tp.Tournament.StartUtc)
                .Select(tp => (DateTime?)tp.Tournament.StartUtc)
                .FirstOrDefaultAsync(ct);

            var lastPostDate = await _db.Posts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => (DateTime?)p.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var lastActivityDate = new[] { lastTournamentDate, lastPostDate, user.CreatedAt }
                .Where(d => d.HasValue)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            var daysSinceLastActivity = lastActivityDate.HasValue
                ? (int)(DateTime.UtcNow - lastActivityDate.Value).TotalDays
                : 0;

            var isActive = daysSinceLastActivity <= 30;

            // Map sang DTO
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
                AvatarPublicId = user.AvatarPublicId,
                CreatedAt = user.CreatedAt,
                Roles = roles.ToList(),
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                AccessFailedCount = user.AccessFailedCount,
                TwoFactorEnabled = user.TwoFactorEnabled,
                
                ActivityStats = new UserActivityStatsDto
                {
                    TotalLogins = 0,  // Không track login history trong DB hiện tại
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

    /// <summary>
    /// DEACTIVATE USER - Vô hiệu hóa tài khoản user (lock vĩnh viễn)
    /// Sử dụng LockoutEnd = DateTimeOffset.MaxValue để lock vĩnh viễn
    /// User không thể login nhưng dữ liệu vẫn được giữ lại trong hệ thống
    /// </summary>
    public async Task<Response> DeactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }
            // Kiểm tra user đã bị deactivate chưa
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Response.Error("User is already deactivated");
            }
            // Nếu LockoutEnabled = false → User được bảo vệ (VIP/Admin account)
            // KHÔNG CHO PHÉP deactivate
            if (!user.LockoutEnabled)
            {
                _logger.LogWarning(
                    "Attempt to deactivate protected account: User {UserId} ({UserName}) has LockoutEnabled = false. Request denied.",
                    userId, user.UserName);
                
                return Response.Error(
                    "Cannot deactivate this user. " +
                    "This is a protected account (VIP/Admin) with lockout protection enabled. " +
                    "Please contact system administrator if you need to deactivate this account.");
            }

            // Lấy roles để log
            var roles = await _userManager.GetRolesAsync(user);

            // Set LockoutEnd = MaxValue (lock vĩnh viễn)
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to deactivate user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to deactivate user: {errors}");
            }
            _logger.LogInformation(
                "User {UserId} ({UserName}, roles: {Roles}) has been deactivated successfully",
                userId, user.UserName, string.Join(", ", roles));

            return Response.Ok(new
            {
                message = "User deactivated successfully",
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

    /// <summary>
    /// REACTIVATE USER - Kích hoạt lại tài khoản đã bị deactivate
    /// Xóa lockout để user có thể login lại
    /// </summary>
    public async Task<Response> ReactivateUserAsync(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return Response.Error("User not found");
            }

            // Kiểm tra user có đang bị deactivate không
            if (!user.LockoutEnd.HasValue || user.LockoutEnd.Value <= DateTimeOffset.UtcNow)
            {
                return Response.Error("User is not currently deactivated");
            }

            // Lấy roles để log
            var roles = await _userManager.GetRolesAsync(user);

            // Unlock user: set LockoutEnd = null
            user.LockoutEnd = null;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to reactivate user {UserId}: {Errors}", userId, errors);
                return Response.Error($"Failed to reactivate user: {errors}");
            }

            _logger.LogInformation(
                "User {UserId} ({UserName}, roles: {Roles}) has been reactivated successfully",
                userId, user.UserName, string.Join(", ", roles));

            return Response.Ok(new
            {
                message = "User reactivated successfully",
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

    /// <summary>
    /// GET USER STATISTICS - Lấy thống kê tổng quan về users
    /// </summary>
    public async Task<Response> GetUserStatisticsAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            // Get all users
            var allUsers = await _userManager.Users.ToListAsync(ct);
            var totalUsers = allUsers.Count;

            // Overview Statistics
            var activeUsers = allUsers.Count(u => !u.LockoutEnd.HasValue || u.LockoutEnd.Value <= DateTimeOffset.UtcNow);
            var lockedUsers = allUsers.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow);
            var deactivatedUsers = allUsers.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value == DateTimeOffset.MaxValue);

            // Email & Phone Verification
            var emailConfirmedUsers = allUsers.Count(u => u.EmailConfirmed);
            var emailUnconfirmedUsers = totalUsers - emailConfirmedUsers;
            var emailConfirmationRate = totalUsers > 0 
                ? Math.Round((double)emailConfirmedUsers / totalUsers * 100, 2) 
                : 0;

            var phoneConfirmedUsers = allUsers.Count(u => u.PhoneNumberConfirmed);
            var phoneUnconfirmedUsers = totalUsers - phoneConfirmedUsers;
            var phoneConfirmationRate = totalUsers > 0 
                ? Math.Round((double)phoneConfirmedUsers / totalUsers * 100, 2) 
                : 0;

            // Security
            var twoFactorEnabledUsers = allUsers.Count(u => u.TwoFactorEnabled);
            var twoFactorAdoptionRate = totalUsers > 0 
                ? Math.Round((double)twoFactorEnabledUsers / totalUsers * 100, 2) 
                : 0;

            // Recent Activity
            var usersCreatedToday = allUsers.Count(u => u.CreatedAt.Date == today);
            var usersCreatedLast7Days = allUsers.Count(u => u.CreatedAt >= last7Days);
            var usersCreatedLast30Days = allUsers.Count(u => u.CreatedAt >= last30Days);

            // Role Distribution
            var roleDistribution = new List<RoleDistributionDto>();
            var allRoles = await _roleManager.Roles.ToListAsync(ct);
            
            foreach (var role in allRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
                var count = usersInRole.Count;
                var percentage = totalUsers > 0 
                    ? Math.Round((double)count / totalUsers * 100, 2) 
                    : 0;

                roleDistribution.Add(new RoleDistributionDto
                {
                    RoleName = role.Name!,
                    Count = count,
                    Percentage = percentage
                });
            }

            // Geographic Distribution - Top 10 countries
            var topCountries = allUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Country))
                .GroupBy(u => u.Country)
                .Select(g => new GeographicDistributionDto
                {
                    Location = g.Key!,
                    Count = g.Count(),
                    Percentage = totalUsers > 0 
                        ? Math.Round((double)g.Count() / totalUsers * 100, 2) 
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            // Geographic Distribution - Top 10 cities
            var topCities = allUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.City))
                .GroupBy(u => u.City)
                .Select(g => new GeographicDistributionDto
                {
                    Location = g.Key!,
                    Count = g.Count(),
                    Percentage = totalUsers > 0 
                        ? Math.Round((double)g.Count() / totalUsers * 100, 2) 
                        : 0
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            // Monthly Growth (Last 6 months)
            var monthlyGrowth = new List<UserGrowthTrendDto>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = now.AddMonths(-i).Date;
                var monthStartFirstDay = new DateTime(monthStart.Year, monthStart.Month, 1);
                var monthEnd = monthStartFirstDay.AddMonths(1);

                var newUsersInMonth = allUsers.Count(u => 
                    u.CreatedAt >= monthStartFirstDay && 
                    u.CreatedAt < monthEnd);

                var totalUsersUpToMonth = allUsers.Count(u => u.CreatedAt < monthEnd);

                monthlyGrowth.Add(new UserGrowthTrendDto
                {
                    Year = monthStartFirstDay.Year,
                    Month = monthStartFirstDay.Month,
                    MonthName = monthStartFirstDay.ToString("MMM yyyy"),
                    NewUsers = newUsersInMonth,
                    TotalUsers = totalUsersUpToMonth
                });
            }

            var statistics = new UserStatisticsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                LockedUsers = lockedUsers,
                DeactivatedUsers = deactivatedUsers,
                EmailConfirmedUsers = emailConfirmedUsers,
                EmailUnconfirmedUsers = emailUnconfirmedUsers,
                EmailConfirmationRate = emailConfirmationRate,
                PhoneConfirmedUsers = phoneConfirmedUsers,
                PhoneUnconfirmedUsers = phoneUnconfirmedUsers,
                PhoneConfirmationRate = phoneConfirmationRate,
                TwoFactorEnabledUsers = twoFactorEnabledUsers,
                TwoFactorAdoptionRate = twoFactorAdoptionRate,
                UsersCreatedLast7Days = usersCreatedLast7Days,
                UsersCreatedLast30Days = usersCreatedLast30Days,
                UsersCreatedToday = usersCreatedToday,
                RoleDistribution = roleDistribution,
                TopCountries = topCountries,
                TopCities = topCities,
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

    /// <summary>
    /// GET USER ACTIVITY LOG - Lấy activity log của 1 user
    /// </summary>
    public async Task<Response> GetUserActivityLogAsync(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Response.Error("User not found");
            }

            // Get related data counts
            var claimedPlayers = await _db.Players
                .Where(p => p.UserId == userId)
                .CountAsync(ct);

            var tournaments = await _db.TournamentPlayers
                .Where(tp => tp.Player != null && tp.Player.UserId == userId)
                .Include(tp => tp.Tournament)
                .Select(tp => tp.Tournament)
                .Distinct()
                .ToListAsync(ct);

            var posts = await _db.Posts
                .Where(p => p.UserId == userId)
                .CountAsync(ct);

            var venues = await _db.Venues
                .Where(v => v.CreatedByUserId == userId)
                .CountAsync(ct);

            // Build activity summary
            var activitySummary = new UserActivitySummaryDto
            {
                TotalPlayers = claimedPlayers,
                TotalTournaments = tournaments.Count,
                TotalPosts = posts,
                TotalVenues = venues,
                TotalLoginAttempts = 0,  // Không track trong DB
                FailedLoginAttempts = user.AccessFailedCount,
                LastLoginAt = null,  // Không track trong DB
                TimesLocked = user.LockoutEnd.HasValue ? 1 : 0,
                LastLockedAt = user.LockoutEnd?.DateTime,
                LastUnlockedAt = null,  // Không track trong DB
                AccountCreatedAt = user.CreatedAt,
                LastActivityAt = tournaments.Any() 
                    ? tournaments.Max(t => t.StartUtc) 
                    : user.CreatedAt
            };

            // Build recent activities (top 20)
            var recentActivities = new List<ActivityEntryDto>();

            // Add tournament activities
            foreach (var tournament in tournaments.OrderByDescending(t => t.StartUtc).Take(10))
            {
                recentActivities.Add(new ActivityEntryDto
                {
                    ActivityType = "Tournament",
                    Description = $"Joined tournament: {tournament.Name}",
                    ActivityDate = tournament.StartUtc,
                    RelatedEntityId = tournament.Id.ToString(),
                    RelatedEntityName = tournament.Name
                });
            }

            // Add recent posts
            var recentPosts = await _db.Posts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync(ct);

            foreach (var post in recentPosts)
            {
                var contentPreview = post.Content != null && post.Content.Length > 50
                    ? post.Content.Substring(0, 50) + "..."
                    : post.Content ?? "No content";

                recentActivities.Add(new ActivityEntryDto
                {
                    ActivityType = "Post",
                    Description = $"Created post: {contentPreview}",
                    ActivityDate = post.CreatedAt,
                    RelatedEntityId = post.Id.ToString(),
                    RelatedEntityName = post.Content
                });
            }

            // Sort by date and take top 20
            recentActivities = recentActivities
                .OrderByDescending(a => a.ActivityDate)
                .Take(20)
                .ToList();

            var activityLog = new UserActivityLogDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                ActivitySummary = activitySummary,
                RecentActivities = recentActivities
            };

            return Response.Ok(activityLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user activity log for userId={UserId}", userId);
            return Response.Error("Error fetching user activity log");
        }
    }

    /// <summary>
    /// BULK DEACTIVATE USERS - Deactivate nhiều users cùng lúc
    /// </summary>
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
                            UserName = null,
                            Success = false,
                            ErrorMessage = "User not found",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // Check if already deactivated
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

                    // Check if protected account (LockoutEnabled = false)
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

                    // Deactivate user
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = true,
                            ErrorMessage = null,
                            Status = "Success"
                        });
                        successCount++;

                        _logger.LogInformation(
                            "User {UserId} ({UserName}) deactivated via bulk operation. Reason: {Reason}",
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
                    _logger.LogError(ex, "Error deactivating user {UserId} in bulk operation", userId);
                    results.Add(new BulkOperationItemResultDto
                    {
                        UserId = userId,
                        UserName = null,
                        Success = false,
                        ErrorMessage = ex.Message,
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

    /// <summary>
    /// BULK REACTIVATE USERS - Reactivate nhiều users cùng lúc
    /// </summary>
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
                            UserName = null,
                            Success = false,
                            ErrorMessage = "User not found",
                            Status = "Failed"
                        });
                        failedCount++;
                        continue;
                    }

                    // Check if user is not currently deactivated
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

                    // Reactivate user
                    user.LockoutEnd = null;
                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                    {
                        results.Add(new BulkOperationItemResultDto
                        {
                            UserId = userId,
                            UserName = user.UserName,
                            Success = true,
                            ErrorMessage = null,
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
                        UserName = null,
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

    /// <summary>
    /// EXPORT USERS - Export danh sách users ra CSV
    /// </summary>
    public async Task<Response> ExportUsersAsync(AdminUserFilterDto filter, CancellationToken ct)
    {
        try
        {
            // Get users with same filter logic
            var query = _userManager.Users.AsQueryable();

            // Apply all filters (copy from GetUsersAsync)
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower().Trim();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                    (u.Nickname != null && u.Nickname.ToLower().Contains(searchLower))
                );
            }

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
                if (filter.IsLockedOut.Value)
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                else
                    query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
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

            // Get all users (no pagination for export)
            var users = await query.ToListAsync(ct);

            // Build CSV content
            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("UserId,UserName,Email,EmailConfirmed,PhoneNumber,PhoneConfirmed,FirstName,LastName,Nickname,Country,City,Roles,CreatedAt,IsLockedOut,LockoutEnd");

            // Data rows
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var rolesStr = string.Join(";", roles);
                var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                
                csv.AppendLine($"\"{user.Id}\"," +
                    $"\"{user.UserName}\"," +
                    $"\"{user.Email}\"," +
                    $"{user.EmailConfirmed}," +
                    $"\"{user.PhoneNumber}\"," +
                    $"{user.PhoneNumberConfirmed}," +
                    $"\"{user.FirstName}\"," +
                    $"\"{user.LastName}\"," +
                    $"\"{user.Nickname}\"," +
                    $"\"{user.Country}\"," +
                    $"\"{user.City}\"," +
                    $"\"{rolesStr}\"," +
                    $"{user.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                    $"{isLockedOut}," +
                    $"\"{user.LockoutEnd?.DateTime:yyyy-MM-dd HH:mm:ss}\"");
            }

            var exportResult = new
            {
                fileName = $"users_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                contentType = "text/csv",
                content = csv.ToString(),
                totalRecords = users.Count,
                exportedAt = DateTime.UtcNow
            };

            return Response.Ok(exportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting users");
            return Response.Error("Error exporting users");
        }
    }
}
