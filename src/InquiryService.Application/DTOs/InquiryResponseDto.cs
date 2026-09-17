using InquiryService.Domain.Enums;

namespace InquiryService.Application.DTOs;

public record InquiryResponseDto
    (
    string TrackingNumber,
    string IdempotencyKey,
    InquiryStatus Status,
    string? SuccessfulProvider,
    string? ResultPayload,
    string? ErrorMessage,
    bool IsFromCache,
    DateTime CreatedAt,
    DateTime? CompletedAt
    );