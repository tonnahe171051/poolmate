using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _svc;
    private readonly IBracketService _bracket;
    public TournamentsController(ITournamentService svc, IBracketService bracket)
    {
        _svc = svc;
        _bracket = bracket;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTournamentModel m, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var id = await _svc.CreateAsync(userId, m, ct);
            if (id is null) return BadRequest(new { message = "Create failed" });
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my-tournaments")]
    public async Task<ActionResult<PagingList<UserTournamentListDto>>> GetTournamentsByUser(
    [FromQuery] string? searchName = null,
    [FromQuery] TournamentStatus? status = null,
    [FromQuery] int pageIndex = 1,
    [FromQuery] int pageSize = 10,
    CancellationToken ct = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _svc.GetTournamentsByUserAsync(userId, searchName, status, pageIndex, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDetailDto>> GetTournamentDetail(int id, CancellationToken ct)
    {
        var tournament = await _svc.GetTournamentDetailAsync(id, ct);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTournamentModel m, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ok = await _svc.UpdateAsync(id, userId, m, ct);
            return ok ? Ok(new { id }) : Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/flyer")]
    public async Task<IActionResult> UpdateFlyer(int id, [FromBody] UpdateFlyerModel m, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.UpdateFlyerAsync(id, userId, m, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.StartAsync(id, userId, ct);
        return ok ? Ok(new { id }) : Forbid();
    }

    [HttpGet("payout-templates")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PayoutTemplateDto>>> GetPayoutTemplates(CancellationToken ct)
    {
        try
        {
            var data = await _svc.GetPayoutTemplatesAsync(ct);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagingList<TournamentListDto>>> GetTournaments(
        [FromQuery] string? searchName = null,
        [FromQuery] TournamentStatus? status = null,
        [FromQuery] GameType? gameType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _svc.GetTournamentsAsync(searchName, status, gameType, pageIndex, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("{id}/players")]
    public async Task<IActionResult> AddPlayer(
    int id,
    [FromBody] AddTournamentPlayerModel model,
    CancellationToken ct)
    {
        try
        {
            if (model.Phone != null && model.Phone != "")
            {
                if (model.Phone.Trim().Length == 0)
                    return BadRequest(new { message = "Phone number cannot be only whitespace." });

                var phone = model.Phone.Trim();
                if (!Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
                    return BadRequest(new { message = "Invalid phone number. Must be 10-15 digits, optional leading '+'." });

                model.Phone = phone;
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var tp = await _svc.AddTournamentPlayerAsync(id, userId, model, ct);
            if (tp is null) return NotFound(new { message = "Tournament not found or not owned by you." });

            return Ok(new { id = tp.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/players/bulk-lines")]
    public async Task<IActionResult> BulkAddPlayersPerLine(
    int id,
    [FromBody] AddTournamentPlayersPerLineModel model,
    CancellationToken ct)
    {
        try
        {
            var ownerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var resp = await _svc.BulkAddPlayersPerLineAsync(id, ownerUserId, model, ct);
            return Ok(resp);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("players/search")]
    public async Task<IActionResult> SearchPlayers([FromQuery] string q, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _svc.SearchPlayersAsync(q, limit, ct);
        return Ok(items);
    }

    [HttpPost("{tournamentId}/players/{tpId}/link")]
    public async Task<IActionResult> LinkPlayer(int tournamentId, int tpId, [FromBody] LinkPlayerRequest m, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.LinkTournamentPlayerAsync(tournamentId, tpId, uid, m, ct);
        if (!ok) return BadRequest(new { message = "Link failed." });
        return Ok(new { message = "Linked." });
    }

    [HttpPost("{tournamentId}/players/{tpId}/unlink")]
    public async Task<IActionResult> UnlinkPlayer(int tournamentId, int tpId, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await _svc.UnlinkTournamentPlayerAsync(tournamentId, tpId, uid, ct);
        if (!ok) return BadRequest(new { message = "Unlink failed." }); 
        return Ok(new { message = "Unlinked." });
    }

    [HttpPost("{tournamentId}/players/{tpId}/create-profile")]
    public async Task<IActionResult> CreateProfileFromSnapshot(int tournamentId, int tpId,
        [FromBody] CreateProfileFromSnapshotRequest m, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var playerId = await _svc.CreateProfileFromSnapshotAndLinkAsync(tournamentId, tpId, uid, m, ct);
        if (playerId is null) return BadRequest(new { message = "Create profile failed." });
        return Ok(new { playerId });
    }

    [HttpGet("{id}/players")]
    public async Task<ActionResult<List<TournamentPlayerListDto>>> GetTournamentPlayers(
    int id,
    [FromQuery] string? searchName = null,
    CancellationToken ct = default)
    {
        var players = await _svc.GetTournamentPlayersAsync(id, searchName, ct);
        return Ok(players);
    }

    [HttpPut("{tournamentId}/players/{tpId}")]
    public async Task<IActionResult> UpdateTournamentPlayer(
    int tournamentId,
    int tpId,
    [FromBody] UpdateTournamentPlayerModel model,
    CancellationToken ct)
    {
        try
        {
            if (model.Phone != null && model.Phone != "")
            {
                if (model.Phone.Trim().Length == 0)
                    return BadRequest(new { message = "Phone number cannot be only whitespace." });

                var phone = model.Phone.Trim();
                if (!Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
                    return BadRequest(new { message = "Invalid phone number. Must be 10-15 digits, optional leading '+'." });

                model.Phone = phone;
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var success = await _svc.UpdateTournamentPlayerAsync(tournamentId, tpId, userId, model, ct);

            if (!success)
                return NotFound(new { message = "Tournament player not found or you don't have permission." });

            return Ok(new { message = "Tournament player updated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/tables")]
    public async Task<IActionResult> AddTable(
        int id,
        [FromBody] AddTournamentTableModel model,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var table = await _svc.AddTournamentTableAsync(id, userId, model, ct);
        if (table is null)
            return NotFound(new { message = "Tournament not found or not owned by you." });

        return Ok(new
        {
            id = table.Id,
            label = table.Label,
            message = "Table added successfully."
        });
    }

    [HttpPost("{id}/tables/bulk")]
    public async Task<IActionResult> AddMultipleTables(
    int id,
    [FromBody] AddMultipleTournamentTablesModel model,
    CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (model.EndNumber < model.StartNumber)
            return BadRequest(new { message = "End number must be greater than or equal to start number." });

        var tableCount = model.EndNumber - model.StartNumber + 1;
        if (tableCount > 50)
            return BadRequest(new { message = "Cannot add more than 50 tables at once." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await _svc.AddMultipleTournamentTablesAsync(id, userId, model, ct);
        if (result is null)
            return NotFound(new { message = "Tournament not found or not owned by you." });

        return Ok(new
        {
            addedCount = result.AddedCount,
            tables = result.Added,
            message = $"Successfully added {result.AddedCount} tables."
        });
    }

    [HttpPut("{tournamentId}/tables/{tableId}")]
    public async Task<IActionResult> UpdateTable(
        int tournamentId,
        int tableId,
        [FromBody] UpdateTournamentTableModel model,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var success = await _svc.UpdateTournamentTableAsync(tournamentId, tableId, userId, model, ct);
        if (!success)
            return NotFound(new { message = "Table not found or tournament not owned by you." });

        return Ok(new
        {
            tableId,
            message = "Table updated successfully."
        });
    }

    [HttpDelete("{tournamentId}/tables")]
    public async Task<IActionResult> DeleteTables(
        int tournamentId,
        [FromBody] DeleteTablesModel model,
        CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _svc.DeleteTournamentTablesAsync(tournamentId, userId, model, ct);
            if (result is null)
                return NotFound(new { message = "Tournament not found or not owned by you." });

            return Ok(new
            {
                deletedCount = result.DeletedCount,
                deletedIds = result.DeletedIds,
                failed = result.Failed,
                message = $"Successfully deleted {result.DeletedCount} table(s)."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/tables")]
    public async Task<ActionResult<List<TournamentTableDto>>> GetTournamentTables(
        int id,
        CancellationToken ct = default)
    {
        var tables = await _svc.GetTournamentTablesAsync(id, ct);
        return Ok(tables);
    }

    [HttpDelete("{tournamentId}/players")]
    public async Task<IActionResult> DeletePlayers(
    int tournamentId,
    [FromBody] DeletePlayersModel model,
    CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _svc.DeleteTournamentPlayersAsync(tournamentId, userId, model, ct);
            if (result is null)
                return NotFound(new { message = "Tournament not found or not owned by you." });

            return Ok(new
            {
                deletedCount = result.DeletedCount,
                deletedIds = result.DeletedIds,
                failed = result.Failed,
                message = $"Successfully deleted {result.DeletedCount} player(s)."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTournament(int id, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var success = await _svc.DeleteTournamentAsync(id, userId, ct);

            if (!success)
                return NotFound(new { message = "Tournament not found or you don't have permission to delete it." });

            return Ok(new { message = "Tournament deleted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Controllers/TournamentsController.cs
    [HttpGet("{id}/bracket/preview")]
    public async Task<ActionResult<BracketPreviewDto>> PreviewBracket(int id, CancellationToken ct)
    {
        var dto = await _bracket.PreviewAsync(id, ct);
        return Ok(dto);
    }

    [HttpPost("{id}/bracket/create")]
    public async Task<IActionResult> CreateBracket(
        int id,
        [FromBody] CreateBracketRequest? request,
        CancellationToken ct)
    {
        try
        {
            await _bracket.CreateAsync(id, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/bracket/reset")]
    public async Task<IActionResult> ResetBracket(int id, CancellationToken ct)
    {
        try
        {
            await _bracket.ResetAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tournament not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("{id}/bracket")]
    public async Task<ActionResult<BracketDto>> GetBracket(
        int id,
        [FromQuery] BracketFilterType? filterType = null,
        CancellationToken ct = default)
    {
        try
        {
            if (filterType.HasValue)
            {
                var filter = new BracketFilterRequest { FilterType = filterType.Value };
                var filtered = await _bracket.GetFilteredAsync(id, filter, ct);
                return Ok(filtered);
            }

            var bracket = await _bracket.GetAsync(id, ct);
            return Ok(bracket);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tournament not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/status-summary")]
    public async Task<ActionResult<TournamentStatusSummaryDto>> GetTournamentStatusSummary(int id, CancellationToken ct)
    {
        try
        {
            var status = await _bracket.GetTournamentStatusAsync(id, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tournament not found." });
        }
    }

    [HttpPost("{tournamentId}/stages/{stageNo}/complete")]
    public async Task<IActionResult> CompleteStage(
        int tournamentId,
        int stageNo,
        [FromBody] CompleteStageRequest? request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _bracket.CompleteStageAsync(tournamentId, stageNo, request ?? new CompleteStageRequest(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tournament or stage not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/bracket/debug")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetBracketDebug(int id, CancellationToken ct = default)
    {
        var lines = await _bracket.GetBracketDebugViewAsync(id, ct);
        return Ok(lines);
    }

    [HttpGet("{tournamentId}/players/stats")]
    public async Task<ActionResult<IReadOnlyList<TournamentPlayerStatsDto>>> GetPlayerStats(
        int tournamentId,
        CancellationToken ct = default)
    {
        var stats = await _bracket.GetPlayerStatsAsync(tournamentId, ct);
        return Ok(stats);
    }
}
