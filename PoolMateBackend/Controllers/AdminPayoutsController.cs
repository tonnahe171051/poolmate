using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PoolMate.Api.Dtos.Admin.Payout;
using PoolMate.Api.Dtos.Response;
using PoolMate.Api.Services;
using PoolMate.Api.Common;

namespace PoolMate.Api.Controllers;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Roles = "Admin")]
public class AdminPayoutsController : ControllerBase
{
    private readonly IAdminPayoutService _service;
    private readonly ILogger<AdminPayoutsController> _logger;

    public AdminPayoutsController(
        IAdminPayoutService service,
        ILogger<AdminPayoutsController> logger)
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

            var result = await _service.CreateTemplateAsync(dto, ct);
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
            var result = await _service.GetTemplateByIdAsync(id, ct);
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
    public async Task<ActionResult<ApiResponse<List<PayoutTemplateDto>>>> GetTemplates(CancellationToken ct)
    {
        try
        {
            var results = await _service.GetTemplatesAsync(ct);
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

            var result = await _service.UpdateTemplateAsync(id, dto, ct);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail(404, "Payout template not found"));
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
            var response = await _service.DeleteTemplateAsync(id, ct);

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