using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Services;
using PoolMate.Api.Dtos.Response;
using PoolMate.Api.Dtos.Admin.Player;

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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagingList<PlayerListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagingList<PlayerListDto>>>> GetPlayers(
        [FromQuery] PlayerFilterDto filter,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetPlayersAsync(filter, ct);
            return Ok(ApiResponse<PagingList<PlayerListDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<PagingList<PlayerListDto>>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStatisticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PlayerStatisticsDto>>> GetPlayerStatistics(
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetPlayerStatisticsAsync(ct);
            return Ok(ApiResponse<PlayerStatisticsDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<PlayerStatisticsDto>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PlayerDetailDto>>> GetPlayerDetail(
        int playerId,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetPlayerDetailAsync(playerId, ct);
            if (result == null)
            {
                return NotFound(ApiResponse<PlayerDetailDto>.Fail(404, "Player not found."));
            }

            return Ok(ApiResponse<PlayerDetailDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<PlayerDetailDto>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("data-quality")]
    [ProducesResponseType(typeof(ApiResponse<DataQualityReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DataQualityReportDto>>> GetDataQualityReport(
        CancellationToken ct)
    {
        try
        {
            var report = await _service.GetDataQualityReportAsync(ct);
            return Ok(ApiResponse<DataQualityReportDto>.Ok(report));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<DataQualityReportDto>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("issues/{issueType}")]
    [ProducesResponseType(typeof(ApiResponse<PlayersWithIssuesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PlayersWithIssuesDto>>> GetPlayersWithIssues(
        string issueType,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;
            var result = await _service.GetPlayersWithIssuesAsync(issueType, pageIndex, pageSize, ct);
            return Ok(ApiResponse<PlayersWithIssuesDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<PlayersWithIssuesDto>.Fail(500, "Internal server error"));
        }
    }

    
    
    [HttpGet("export")]
    [Produces("text/csv")] 
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportPlayers( 
        [FromQuery] PlayerFilterDto filter,
        [FromQuery] bool includeTournamentHistory = false,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        try
        {
            var response = await _service.ExportPlayersAsync(filter, includeTournamentHistory, format, ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }
            if (response.Data is not FileExportDto data)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, "Export data format mismatch"));
            }
            var fileBytes = System.Text.Encoding.UTF8.GetBytes(data.Content);
            return File(fileBytes, data.ContentType, data.FileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error during export"));
        }
    }
    
    
    [HttpPost("merge")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MergePlayers([FromBody] MergePlayerRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _service.MergePlayersAsync(request, ct);
            if (!result.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, result.Message));
            }
            return Ok(ApiResponse<object>.Ok(result.Data));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
}