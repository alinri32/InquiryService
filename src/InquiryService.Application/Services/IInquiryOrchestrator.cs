using InquiryService.Application.Common;
using InquiryService.Application.DTOs;

namespace InquiryService.Application.Services;

public interface IInquiryOrchestrator
{
    Task<ApiResponse<InquiryResponseDto>> ProcessInquiryAsync(
        InquiryRequestDto request,
        CancellationToken cancellationToken = default);
}