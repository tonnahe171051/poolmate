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
