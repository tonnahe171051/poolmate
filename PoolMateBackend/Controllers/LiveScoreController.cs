using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMate.Api.Services;

namespace PoolMate.Api.Controllers
{
    [ApiController]
    [Route("api/livescore")]
    public class LiveScoreController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITableTokenService _tableTokenService;
        private readonly IMatchService _matchService;

        public LiveScoreController(
            ApplicationDbContext db,
            ITableTokenService tableTokenService,
            IMatchService matchService)
        {
            _db = db;
            _tableTokenService = tableTokenService;
            _matchService = matchService;
        }

        [HttpPost("tournaments/{tournamentId:int}/tables/{tableId:int}/token")]
        [Authorize]
        public async Task<ActionResult<TableTokenResponse>> GenerateTableToken(
            int tournamentId,
            int tableId,
            [FromBody] TableTokenRequest? request,
            CancellationToken ct)
        {
            var table = await _db.TournamentTables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId, ct);

            if (table is null || table.TournamentId != tournamentId)
                return NotFound(new { message = "Table not found for the specified tournament." });

            var lifetime = request?.LifetimeMinutes is > 0
                ? TimeSpan.FromMinutes(request!.LifetimeMinutes.Value)
                : (TimeSpan?)null;

            var token = _tableTokenService.GenerateToken(tableId, tournamentId, lifetime);
            return Ok(token);
        }

        [HttpGet("tournaments/{tournamentId:int}/tables/{tableId:int}/match")]
        [AllowAnonymous]
        public async Task<ActionResult<MatchDto>> GetActiveMatchForTable(
            int tournamentId,
            int tableId,
            CancellationToken ct)
        {
            try
            {
                await EnsureTableAccessAsync(tournamentId, tableId, ct);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            var matchId = await _db.Matches
                .Where(m => m.TournamentId == tournamentId && m.TableId == tableId && m.Status != MatchStatus.Completed)
                .OrderBy(m => m.Id)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(ct);

            if (matchId is null)
                return NoContent();

            var matchDto = await _matchService.GetMatchAsync(matchId.Value, ct);
            return Ok(matchDto);
        }

        [HttpPost("matches/{matchId:int}/score")]
        [AllowAnonymous]
        public async Task<ActionResult<MatchScoreUpdateResponse>> UpdateLiveScore(
            int matchId,
            [FromBody] UpdateLiveScoreRequest request,
            CancellationToken ct)
        {
            if (request is null)
                return BadRequest(new { message = "Request body is required." });

            try
            {
                var actor = await ResolveScoringContextAsync(matchId, ct);
                var response = await _matchService.UpdateLiveScoreAsync(matchId, request, actor, ct);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (MatchLockedException ex)
            {
                return Conflict(new
                {
                    message = "Match is currently locked by another client.",
                    lockId = ex.LockId,
                    expiresAt = ex.ExpiresAt
                });
            }
            catch (ConcurrencyConflictException ex)
            {
                return Conflict(new
                {
                    message = ex.Message,
                    match = ex.LatestMatch
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("matches/{matchId:int}/complete")]
        [AllowAnonymous]
        public async Task<ActionResult<MatchScoreUpdateResponse>> CompleteMatch(
            int matchId,
            [FromBody] CompleteMatchRequest request,
            CancellationToken ct)
        {
            if (request is null)
                return BadRequest(new { message = "Request body is required." });

            try
            {
                var actor = await ResolveScoringContextAsync(matchId, ct);
                var response = await _matchService.CompleteMatchAsync(matchId, request, actor, ct);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (MatchLockedException ex)
            {
                return Conflict(new
                {
                    message = "Match is currently locked by another client.",
                    lockId = ex.LockId,
                    expiresAt = ex.ExpiresAt
                });
            }
            catch (ConcurrencyConflictException ex)
            {
                return Conflict(new
                {
                    message = ex.Message,
                    match = ex.LatestMatch
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<TournamentTable> EnsureTableAccessAsync(int tournamentId, int tableId, CancellationToken ct)
        {
            var table = await _db.TournamentTables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tableId, ct)
                ?? throw new KeyNotFoundException();

            if (table.TournamentId != tournamentId)
                throw new KeyNotFoundException();

            // Kiểm tra xem user đã đăng nhập (và không phải là table token)
            var scope = User?.FindFirstValue("scope");
            var isTableToken = !string.IsNullOrEmpty(scope) && scope == "table-scoring";
            
            if (User?.Identity?.IsAuthenticated == true && !isTableToken)
                return table;

            var token = ExtractBearerToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException("Table token is required.");

            TableTokenValidationResult validation;
            try
            {
                validation = _tableTokenService.ValidateToken(token);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(ex.Message);
            }

            if (validation.TournamentId != tournamentId || validation.TableId != tableId)
                throw new UnauthorizedAccessException("Table token does not grant access to this table.");

            return table;
        }

        private async Task<ScoringContext> ResolveScoringContextAsync(int matchId, CancellationToken ct)
        {
            // Kiểm tra xem có phải là user token không (không phải table token)
            var scope = User?.FindFirstValue("scope");
            var isTableToken = !string.IsNullOrEmpty(scope) && scope == "table-scoring";
            
            if (User?.Identity?.IsAuthenticated == true && !isTableToken)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId))
                    throw new UnauthorizedAccessException("User identity is missing.");

                return ScoringContext.ForUser(userId);
            }

            var token = ExtractBearerToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException("Table token is required.");

            TableTokenValidationResult validation;
            try
            {
                validation = _tableTokenService.ValidateToken(token);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(ex.Message);
            }

            var metadata = await GetMatchMetadataAsync(matchId, ct);

            if (!metadata.TableId.HasValue)
                throw new InvalidOperationException("Match is not assigned to a table.");

            if (metadata.TableId.Value != validation.TableId || metadata.TournamentId != validation.TournamentId)
                throw new UnauthorizedAccessException("Table token does not match the requested match.");

            return ScoringContext.ForTable(validation.TableId, validation.TournamentId, validation.TokenId);
        }

        private async Task<MatchMetadata> GetMatchMetadataAsync(int matchId, CancellationToken ct)
        {
            var metadata = await _db.Matches
                .Where(m => m.Id == matchId)
                .Select(m => new MatchMetadata(m.Id, m.TournamentId, m.TableId))
                .FirstOrDefaultAsync(ct);

            if (metadata is null)
                throw new KeyNotFoundException();

            return metadata;
        }

        private string? ExtractBearerToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var values))
                return null;

            var header = values.ToString();
            return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header.Substring("Bearer ".Length).Trim()
                : null;
        }

        private sealed record MatchMetadata(int MatchId, int TournamentId, int? TableId);
    }
}
