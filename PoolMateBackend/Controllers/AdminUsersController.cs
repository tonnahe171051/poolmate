using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.ADMIN)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IAdminUserService userService,
        ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// GET: api/admin/users
    /// Lấy danh sách users với phân trang, filter, search, sort
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] AdminUserFilterDto filter,
        CancellationToken ct)
    {
        var response = await _userService.GetUsersAsync(filter, ct);
        
        if (!response.Success)
        {
            return BadRequest(new { message = response.Message });
        }

        return Ok(response.Data);
    }

    /// <summary>
    /// GET: api/admin/users/{id}
    /// Lấy thông tin chi tiết của 1 user
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(string id, CancellationToken ct)
    {
        var response = await _userService.GetUserDetailAsync(id, ct);
        
        if (!response.Success)
        {
            return NotFound(new { message = response.Message });
        }

        return Ok(response.Data);
    }

    /// <summary>
    /// PUT: api/admin/users/{id}/deactivate
    /// Vô hiệu hóa tài khoản user (lock vĩnh viễn)
    /// User sẽ không thể đăng nhập nhưng dữ liệu vẫn được giữ lại
    /// </summary>
    [HttpPut("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(string id, CancellationToken ct)
    {
        var response = await _userService.DeactivateUserAsync(id, ct);
        
        if (!response.Success)
        {
            return BadRequest(new { message = response.Message });
        }

        return Ok(response.Data);
    }

    /// <summary>
    /// PUT: api/admin/users/{id}/reactivate
    /// Kích hoạt lại tài khoản đã bị deactivate
    /// User sẽ có thể đăng nhập lại
    /// </summary>
    [HttpPut("{id}/reactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateUser(string id, CancellationToken ct)
    {
        var response = await _userService.ReactivateUserAsync(id, ct);
        
        if (!response.Success)
        {
            return BadRequest(new { message = response.Message });
        }

        return Ok(response.Data);
    }
}
