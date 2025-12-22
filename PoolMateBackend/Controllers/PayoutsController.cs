using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PoolMate.Api.Dtos.Payout;
using PoolMate.Api.Dtos.Response;
using PoolMate.Api.Services;
using PoolMate.Api.Common;
using PoolMate.Api.Dtos.Auth;
using System.Security.Claims;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/payouts")]
[Authorize(Roles = UserRoles.ORGANIZER)]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _service;
    private readonly ILogger<PayoutsController> _logger;

    public PayoutsController(
        IPayoutService service,
        ILogger<PayoutsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("templates")]
    [ProducesResponseType(typeof(ApiResponse<PayoutTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PayoutTemplateDto>>> CreateTemplate(
        [FromBody] CreatePayoutTemplateDto dto,
        CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.Fail(400, "Validation failed", errors));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _service.CreateTemplateAsync(userId, dto, ct);
            return CreatedAtAction(
                nameof(GetTemplateById),
                new { id = result.Id },
                ApiResponse<PayoutTemplateDto>.Created(result, "Payout template created successfully")
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payout template");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpGet("templates/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PayoutTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayoutTemplateDto>>> GetTemplateById(
        int id,
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _service.GetTemplateByIdAsync(id, userId, ct);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail(404, "Payout template not found"));
            }

            return Ok(ApiResponse<PayoutTemplateDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payout template {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<List<PayoutTemplateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)] 
    public async Task<ActionResult<ApiResponse<List<PayoutTemplateDto>>>> GetTemplates(CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var results = await _service.GetTemplatesAsync(userId, ct);
            if (results.Count == 0)
            {
                return Ok(ApiResponse<List<PayoutTemplateDto>>.Ok(results,
                    "You don't have any payout templates yet. Please create one."));
            }

            return Ok(ApiResponse<List<PayoutTemplateDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payout templates");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPut("templates/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTemplate(
        int id,
        [FromBody] CreatePayoutTemplateDto dto,
        CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.Fail(400, "Validation failed", errors));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _service.UpdateTemplateAsync(id, userId, dto, ct);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail(404,
                    "Payout template not found or you don't have permission"));
            }

            return Ok(ApiResponse<PayoutTemplateDto>.Ok(result, "Payout template updated successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payout template {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpDelete("templates/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(int id, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var response = await _service.DeleteTemplateAsync(id, userId, ct);
            if (!response.Success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, response.Message));
            }

            return Ok(ApiResponse<object>.Ok(new { deletedId = id }, "Payout template deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting payout template {Id}", id);
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }


    [HttpPost("simulate")]
    [ProducesResponseType(typeof(ApiResponse<PayoutSimulationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PayoutSimulationResultDto>>> SimulatePayout(
        [FromBody] PayoutSimulationRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.SimulatePayoutAsync(request, ct);
            return Ok(ApiResponse<PayoutSimulationResultDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating payout");
            return StatusCode(500, ApiResponse<object>.Fail(500, "Internal server error"));
        }
    }
}