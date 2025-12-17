using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Dtos.Organizer;
using PoolMate.Api.Services;
using System.Security.Claims;

namespace PoolMate.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrganizerController : ControllerBase
    {
        private readonly IOrganizerService _organizerService;

        public OrganizerController(IOrganizerService organizerService)
        {
            _organizerService = organizerService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<OrganizerDto>> Register([FromBody] RegisterOrganizerRequest request, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in token.");

            try
            {
                var organizer = await _organizerService.RegisterAsync(userId, request, ct);
                return Ok(organizer);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("me")]
        public async Task<ActionResult<OrganizerDto>> GetMyOrganizerProfile(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in token.");

            var organizer = await _organizerService.GetByUserIdAsync(userId, ct);
            if (organizer == null)
                return NotFound("Organizer profile not found.");

            return Ok(organizer);
        }

        [HttpGet("check-email/{email}")]
        public async Task<ActionResult<bool>> CheckEmailAvailability(string email, CancellationToken ct)
        {
            var isRegistered = await _organizerService.IsEmailRegisteredAsync(email, ct);
            return Ok(new { isAvailable = !isRegistered });
        }
    }
}
