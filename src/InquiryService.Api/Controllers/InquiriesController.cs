using System.ComponentModel.DataAnnotations;
using InquiryService.Application.Common;
using InquiryService.Application.DTOs;
using InquiryService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InquiryService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// ثبت و انجام استعلام
    /// </summary>
    /// <param name="idempotencyKey">کلید یکتای درخواست جهت تضمین Idempotency</param>
    /// <param name="request">اطلاعات بیزینسی استعلام</param>
    /// <param name="cancellationToken">توکن لغو عملیات</param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteInquiry(
        [FromHeader(Name = "Idempotency-Key")][Required] string idempotencyKey,
        [FromBody] InquiryRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "درخواست استعلام جدید دریافت شد. IdempotencyKey: {Key}, Type: {Type}",
            idempotencyKey,
            request.InquiryType);

        var result = await _orchestrator.ProcessInquiryAsync(idempotencyKey, request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}