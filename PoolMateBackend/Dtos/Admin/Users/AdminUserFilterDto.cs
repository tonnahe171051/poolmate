namespace PoolMate.Api.Dtos.Admin.Users;


public class AdminUserFilterDto
{
    // Pagination
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    
    // Search
    public string? SearchTerm { get; set; } // Tìm theo username, email, name, phone
    
    // Filters (dựa trên các trường trong AspNetUsers)
    public bool? EmailConfirmed { get; set; } // true = đã confirm, false = chưa confirm
    public bool? PhoneNumberConfirmed { get; set; } // true = đã confirm phone, false = chưa
    public bool? TwoFactorEnabled { get; set; } // true = bật 2FA, false = tắt
    public bool? LockoutEnabled { get; set; } // true = có thể bị lock, false = không
    public bool? IsLockedOut { get; set; } // true = đang bị lock, false = không bị lock
    public string? Country { get; set; } // Filter theo country
    public string? City { get; set; } // Filter theo city
    
    // Date range filters
    public DateTime? CreatedFrom { get; set; } // Từ ngày (user created)
    public DateTime? CreatedTo { get; set; } // Đến ngày (user created)
    
    // Sorting
    public string? SortBy { get; set; } // "username", "email", "createdAt", etc.
    public bool IsDescending { get; set; } = false;
}

