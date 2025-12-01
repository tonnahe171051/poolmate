using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Admin.Users;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Services;
using PoolMate.Api.Dtos.Response;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = UserRoles.ADMIN)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _userService;
    private readonly ILogger<AdminUsersController> _logger;
    private readonly IAdminPlayerService _playerService;

    public AdminUsersController(
        IAdminUserService userService,
        IAdminPlayerService playerService,
        ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _playerService = playerService;
        _logger = logger;
    }


    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagingList<AdminUserListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagingList<AdminUserListDto>>>> GetUsers(
        [FromQuery] AdminUserFilterDto filter,
        CancellationToken ct)
    {
        try
        {
            var result = await _userService.GetUsersAsync(filter, ct);

            if (!result.Success)
            {
                // Trả về lỗi bọc trong ApiResponse
                return BadRequest(ApiResponse<object>.Fail(400, result.Message));
            }

            // Ép kiểu dữ liệu trả về từ Service (vì Service của bạn đang trả về object chung chung)
            if (result.Data is not PagingList<AdminUserListDto> data)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, "Data type mismatch"));
            }

            // Trả về thành công bọc trong ApiResponse
            return Ok(ApiResponse<PagingList<AdminUserListDto>>.Ok(data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AdminUserDetailDto>>> GetUserDetail(
        string id,
        CancellationToken ct)
    {
        try
        {
            var response = await _userService.GetUserDetailAsync(id, ct);

            if (!response.Success)
            {
                if (response.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse<object>.Fail(404, response.Message));
                }

                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            if (response.Data is not AdminUserDetailDto data)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, "Data type mismatch"));
            }

            return Ok(ApiResponse<AdminUserDetailDto>.Ok(data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<UserStatisticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<UserStatisticsDto>>> GetUserStatistics(
        CancellationToken ct)
    {
        try
        {
            var response = await _userService.GetUserStatisticsAsync(ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            if (response.Data is not UserStatisticsDto data)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, "Data type mismatch"));
            }

            return Ok(ApiResponse<UserStatisticsDto>.Ok(data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("{id}/activity-log")]
    [ProducesResponseType(typeof(ApiResponse<UserActivityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<UserActivityLogDto>>> GetUserActivityLog(string id, CancellationToken ct)
    {
        try
        {
            var response = await _userService.GetUserActivityLogAsync(id, ct);
            if (!response.Success)
            {
                return NotFound(ApiResponse<object>.Fail(404, response.Message));
            }

            if (response.Data is not UserActivityLogDto data)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, "Data type mismatch"));
            }

            return Ok(ApiResponse<UserActivityLogDto>.Ok(data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPut("{id}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeactivateUser(string id, CancellationToken ct)
    {
        try
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var response = await _userService.DeactivateUserAsync(id, adminId!, ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            return Ok(ApiResponse<object>.Ok(response.Data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPut("{id}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> ReactivateUser(string id, CancellationToken ct)
    {
        try
        {
            var response = await _userService.ReactivateUserAsync(id, ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            return Ok(ApiResponse<object>.Ok(response.Data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPost("bulk-deactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> BulkDeactivateUsers(
        [FromBody] BulkDeactivateUsersDto request,
        CancellationToken ct)
    {
        try
        {
            if (request.UserIds == null || !request.UserIds.Any())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "UserIds list cannot be empty"));
            }

            // 👇 Lấy ID của Admin đang đăng nhập
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var response = await _userService.BulkDeactivateUsersAsync(request, adminId!, ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            return Ok(ApiResponse<object>.Ok(response.Data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPost("bulk-reactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> BulkReactivateUsers(
        [FromBody] BulkReactivateUsersDto request,
        CancellationToken ct)
    {
        try
        {
            if (request.UserIds == null || !request.UserIds.Any())
            {
                return BadRequest(ApiResponse<object>.Fail(400, "UserIds list cannot be empty"));
            }

            var response = await _userService.BulkReactivateUsersAsync(request, ct);

            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            return Ok(ApiResponse<object>.Ok(response.Data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }

    
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportUsers(
        [FromQuery] AdminUserFilterDto filter,
        CancellationToken ct)
    {
        var response = await _userService.ExportUsersAsync(filter, ct);
        if (!response.Success)
        {
            return BadRequest(new { message = response.Message });
        }
        
        if (response.Data is not FileExportDto exportData)
        {
            return StatusCode(500, new { message = "Export data format mismatch" });
        }
        var content = System.Text.Encoding.UTF8.GetBytes(exportData.Content);
        return File(content, "text/csv", exportData.FileName);
    }
}