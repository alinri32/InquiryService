using InquiryService.Domain.Enums;

namespace InquiryService.Domain.Entities;

public class Inquiry
{
    public long Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string IdentityIdentifier { get; set; } = string.Empty;
    public string InquiryType { get; set; } = string.Empty;
    public InquiryStatus Status { get; set; }
    public string? SuccessfulProvider { get; set; }
    public string? ResultPayload { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}