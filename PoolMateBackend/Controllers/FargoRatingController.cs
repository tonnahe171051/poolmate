using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoolMate.Api.Integrations.FargoRate.Models;
using PoolMate.Api.Integrations.FargoRate;

namespace PoolMate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FargoRatingController : ControllerBase
    {
        private readonly IFargoRateService _fargoRateService;
        private readonly ILogger<FargoRatingController> _logger;

        public FargoRatingController(IFargoRateService fargoRateService, ILogger<FargoRatingController> logger)
        {
            _fargoRateService = fargoRateService;
            _logger = logger;
        }

        [HttpPost("batch-search-fargo")]
        public async Task<ActionResult<List<PlayerFargoSearchResult>>> BatchSearchFargoRatings(
            [FromBody] List<BatchSearchRequest> requests)
        {
            var results = await _fargoRateService.BatchSearchPlayersAsync(requests);
            return Ok(results);
        }

        [HttpPost("apply")]
        public async Task<ActionResult> ApplyFargoRatings(
        [FromBody] ApplyFargoRatingsDto dto)
        {
            if (dto.Requests == null || !dto.Requests.Any())
            {
                return BadRequest(new { message = "Request list cannot be empty" });
            }

            try
            {
                var updatedCount = await _fargoRateService.ApplyFargoRatingsAsync(
                    dto.TournamentId,
                    dto.Requests);

                return Ok(new
                {
                    message = "Fargo ratings applied and seeds recalculated",
                    updatedCount = updatedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Fargo ratings");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
