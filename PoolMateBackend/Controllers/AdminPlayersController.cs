using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/admin/players")]
[Authorize(Roles = "Admin")]
public class AdminPlayersController : ControllerBase
{
    private readonly IAdminPlayerService _service;

    public AdminPlayersController(IAdminPlayerService service)
    {
        _service = service;
    }
    
    /// Get danh sách Players với filter, search, sort và pagination
    [HttpGet]
    public async Task<ActionResult<PagingList<PlayerListDto>>> GetPlayers(
        [FromQuery] PlayerFilterDto filter,
        CancellationToken ct)
    {
        var result = await _service.GetPlayersAsync(filter, ct);
        return Ok(result);
    }


    /// Get thống kê tổng quan về Players
    [HttpGet("statistics")]
    public async Task<ActionResult<PlayerStatisticsDto>> GetPlayerStatistics(
        CancellationToken ct)
    {
        var result = await _service.GetPlayerStatisticsAsync(ct);
        return Ok(result);
    }
    
    /// Get danh sách Players chưa claim (chưa link với User) với filter, search, sort và pagination
    [HttpGet("unclaimed")]
    public async Task<ActionResult<PagingList<UnclaimedPlayerDto>>> GetUnclaimedPlayers(
        [FromQuery] PlayerFilterDto filter,
        CancellationToken ct)
    {
        var result = await _service.GetUnclaimedPlayersAsync(filter, ct);
        return Ok(result);
    }


    /// Get chi tiết Player theo ID
    [HttpGet("{playerId}")]
    public async Task<ActionResult<PlayerDetailDto>> GetPlayerDetail(
        int playerId,
        CancellationToken ct)
    {
        var result = await _service.GetPlayerDetailAsync(playerId, ct);
        if (result == null)
            return NotFound(new { message = "Player not found." });

        return Ok(result);
    }



    /// Link Player với User
    [HttpPost("{playerId}/link-user")]
    public async Task<IActionResult> LinkPlayerToUser(
        int playerId,
        [FromBody] LinkPlayerToUserDto dto,
        CancellationToken ct)
    {
        var success = await _service.LinkPlayerToUserAsync(playerId, dto.UserId, ct);
        if (!success)
            return BadRequest(new { message = "Failed to link player to user. Player or user not found, or player already linked to another user." });

        return Ok(new { message = "Player linked to user successfully." });
    }


    /// Unlink Player khỏi User
    [HttpPost("{playerId}/unlink-user")]
    public async Task<IActionResult> UnlinkPlayerFromUser(
        int playerId,
        CancellationToken ct)
    {
        var success = await _service.UnlinkPlayerFromUserAsync(playerId, ct);
        if (!success)
            return NotFound(new { message = "Player not found." });

        return Ok(new { message = "Player unlinked from user successfully." });
    }


    /// Get tất cả Players của 1 User
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<PlayerListDto>>> GetPlayersByUser(
        string userId,
        CancellationToken ct)
    {
        var players = await _service.GetPlayersByUserIdAsync(userId, ct);
        return Ok(players);
    }


    /// Get User đã link với Player
    [HttpGet("{playerId}/linked-user")]
    public async Task<ActionResult<UserInfoDto>> GetLinkedUser(
        int playerId,
        CancellationToken ct)
    {
        var user = await _service.GetLinkedUserAsync(playerId, ct);
        if (user == null)
            return NotFound(new { message = "Player not linked to any user." });

        return Ok(user);
    }


    /// POST: api/admin/players/bulk-link
    /// Bulk link multiple players to users at once

    [HttpPost("bulk-link")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkLinkPlayers(
        [FromBody] BulkLinkPlayersDto request,
        CancellationToken ct)
    {
        if (request.Links == null || !request.Links.Any())
        {
            return BadRequest(new { message = "Links list cannot be empty" });
        }

        var result = await _service.BulkLinkPlayersAsync(request, ct);
        return Ok(result);
    }


    /// POST: api/admin/players/bulk-unlink
    /// Bulk unlink multiple players from users at once

    [HttpPost("bulk-unlink")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkUnlinkPlayers(
        [FromBody] BulkUnlinkPlayersDto request,
        CancellationToken ct)
    {
        if (request.PlayerIds == null || !request.PlayerIds.Any())
        {
            return BadRequest(new { message = "PlayerIds list cannot be empty" });
        }

        var result = await _service.BulkUnlinkPlayersAsync(request, ct);
        return Ok(result);
    }


    /// GET: api/admin/players/data-quality
    /// Tổng hợp báo cáo chất lượng dữ liệu Players

    [HttpGet("data-quality")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataQualityReport(CancellationToken ct)
    {
        var report = await _service.GetDataQualityReportAsync(ct);
        return Ok(report);
    }


    /// GET: api/admin/players/issues/{issueType}
    /// Lấy danh sách players theo loại issue

    [HttpGet("issues/{issueType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayersWithIssues(string issueType, CancellationToken ct)
    {
        var result = await _service.GetPlayersWithIssuesAsync(issueType, ct);
        return Ok(result);
    }


    /// POST: api/admin/players/validate
    /// Validate dữ liệu của 1 player (email/phone/skillLevel)
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ValidationResultDto> ValidatePlayer([FromBody] ValidatePlayerDto request)
    {
        return _service.ValidatePlayerDataAsync(request);
    }


    /// GET: api/admin/players/export
    /// Export danh sách players ra CSV/Excel (CSV supported). Hỗ trợ filter như API list players
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPlayers(
        [FromQuery] PlayerFilterDto filter,
        [FromQuery] bool includeTournamentHistory = false,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var response = await _service.ExportPlayersAsync(filter, includeTournamentHistory, format, ct);
        if (!response.Success)
        {
            return BadRequest(new { message = response.Message });
        }

        var data = response.Data as dynamic;
        if (data == null)
        {
            return BadRequest(new { message = "Export failed" });
        }

        var content = System.Text.Encoding.UTF8.GetBytes(data.content.ToString());
        var contentType = data.contentType?.ToString() ?? "text/csv";
        var fileName = data.fileName?.ToString() ?? $"players_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(content, contentType, fileName);
    }
}
