using System.ComponentModel.DataAnnotations;
using InquiryService.Application.Common;
using InquiryService.Application.DTOs;
using InquiryService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InquiryService.Api.Controllers;

[ApiController]
[Route("api/V1/[controller]")]
public class InquiriesController : ControllerBase
{
    private readonly IInquiryOrchestrator _orchestrator;
    private readonly ILogger<InquiriesController> _logger;

    public InquiriesController(
        IInquiryOrchestrator orchestrator,
        ILogger<InquiriesController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// execute an inquiry request.
    /// </summary>
    [HttpPost("Get")]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteInquiry([FromHeader(Name = "Idempotency-Key")][Required] string idempotencyKey, [FromBody] InquiryRequestDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("درخواست استعلام جدید دریافت شد. IdempotencyKey: {Key}, Type: {Type}", idempotencyKey, request.InquiryType);

        var result = await _orchestrator.ProcessInquiryAsync(idempotencyKey, request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}