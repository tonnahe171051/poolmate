using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        ILogger<AdminUserService> logger)
    {
        _userManager = userManager;
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

            _ => query.OrderByDescending(u => u.CreatedAt) // Default
        };
    }

    /// <summary>
    /// GET USER DETAIL - Lấy thông tin chi tiết của 1 user
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

            // Map sang DTO
            var detailDto = new AdminUserDetailDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
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
                AccessFailedCount = user.AccessFailedCount
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
}
