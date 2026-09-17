using InquiryService.Domain.Enums;

namespace InquiryService.Domain.Entities;

public class InquiryProviderAttempt
{
    public long Id { get; set; }
    public long InquiryId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public byte ExecutionOrder { get; set; }
    public bool IsSuccess { get; set; }
    public ProviderErrorType ErrorType { get; set; }
    public short? HttpStatusCode { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int DurationMs { get; set; }
    public DateTime AttemptedAt { get; set; }
}