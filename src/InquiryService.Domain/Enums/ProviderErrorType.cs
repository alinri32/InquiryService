namespace InquiryService.Domain.Enums;

public enum ProviderErrorType : byte
{
    None = 0,
    TechnicalError = 1,
    Timeout = 2,
    BusinessError = 3
}
