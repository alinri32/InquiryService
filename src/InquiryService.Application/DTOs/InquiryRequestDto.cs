namespace InquiryService.Application.DTOs;

public record InquiryRequestDto(
    string IdempotencyKey,
    string IdentityIdentifier,
    string InquiryType,
    bool BypassCache = false)
{
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            return (false, "فیلد IdempotencyKey الزامی است.");

        if (IdempotencyKey.Length > 64)
            return (false, "طول IdempotencyKey نمی‌تواند بیشتر از ۶۴ کاراکتر باشد.");

        if (string.IsNullOrWhiteSpace(IdentityIdentifier))
            return (false, "شناسه استعلام (مثلاً کد ملی / شناسه فرد) الزامی است.");

        if (IdentityIdentifier.Length > 20)
            return (false, "طول شناسه استعلام نمی‌تواند بیشتر از ۲۰ کاراکتر باشد.");

        if (string.IsNullOrWhiteSpace(InquiryType))
            return (false, "فیلد InquiryType الزامی است.");

        return (true, null);
    }
}