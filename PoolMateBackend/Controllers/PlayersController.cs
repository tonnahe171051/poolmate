using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Player;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize] // Authenticated users only (không cần Admin)
public class PlayersController : ControllerBase
{
    private readonly IAdminPlayerService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlayersController(IAdminPlayerService service, UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }
    
    /// Get danh sách Players mà user có thể claim (based on email)
    [HttpGet("claimable")]
    public async Task<ActionResult<List<ClaimablePlayerDto>>> GetClaimablePlayers(CancellationToken ct)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "User not authenticated." });
            
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return BadRequest(new { message = "User email not found." });
            
            userEmail = user.Email;
        }

        var players = await _service.GetClaimablePlayersAsync(userEmail, ct);
        return Ok(players);
    }


    /// User tự claim Player profile (validate email match)
    [HttpPost("{playerId}/claim")]
    public async Task<ActionResult<ClaimPlayerResponse>> ClaimPlayer(
        int playerId,
        [FromBody] ClaimPlayerRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "User not authenticated." });

        var result = await _service.ClaimPlayerAsync(
            playerId, 
            userId, 
            request.UpdateUserProfile, 
            ct);

        if (result == null)
            return BadRequest(new 
            { 
                message = "Failed to claim player. Player not found, already claimed by another user, or email does not match." 
            });

        return Ok(result);
    }
    
    /// Get danh sách Players mà user đã claim
    [HttpGet("my-players")]
    public async Task<ActionResult<List<Dtos.Admin.Player.PlayerListDto>>> GetMyPlayers(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "User not authenticated." });

        var players = await _service.GetMyPlayersAsync(userId, ct);
        return Ok(players);
    }


    /// Unclaim Player (remove link)
    [HttpPost("{playerId}/unclaim")]
    public async Task<IActionResult> UnclaimPlayer(int playerId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "User not authenticated." });

        // Check if player belongs to this user
        var player = await _service.GetLinkedUserAsync(playerId, ct);
        if (player == null || player.UserId != userId)
            return Forbid(); 

        var success = await _service.UnlinkPlayerFromUserAsync(playerId, ct);
        if (!success)
            return BadRequest(new { message = "Failed to unclaim player." });

        return Ok(new { message = "Player unclaimed successfully." });
    }
}

