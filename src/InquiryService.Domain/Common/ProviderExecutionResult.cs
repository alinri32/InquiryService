using InquiryService.Domain.Enums;

namespace InquiryService.Domain.Common;

public class ProviderExecutionResult
{
    public bool IsSuccess { get; set; }
    public ProviderErrorType ErrorType { get; set; }
    public short? HttpStatusCode { get; set; }
    public string? RawResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }

    public static ProviderExecutionResult Success(string response, int durationMs, short statusCode = 200) => new()
    {
        IsSuccess = true,
        ErrorType = ProviderErrorType.None,
        HttpStatusCode = statusCode,
        RawResponse = response,
        DurationMs = durationMs
    };

    public static ProviderExecutionResult BusinessError(string errorMessage, string? response, int durationMs, short statusCode = 400) => new()
    {
        IsSuccess = false,
        ErrorType = ProviderErrorType.BusinessError,
        HttpStatusCode = statusCode,
        ErrorMessage = errorMessage,
        RawResponse = response,
        DurationMs = durationMs
    };

    public static ProviderExecutionResult TechnicalError(string errorMessage, int durationMs, short? statusCode = 500) => new()
    {
        IsSuccess = false,
        ErrorType = ProviderErrorType.TechnicalError,
        HttpStatusCode = statusCode,
        ErrorMessage = errorMessage,
        DurationMs = durationMs
    };

    public static ProviderExecutionResult Timeout(int durationMs) => new()
    {
        IsSuccess = false,
        ErrorType = ProviderErrorType.Timeout,
        HttpStatusCode = 504,
        ErrorMessage = "Request timed out",
        DurationMs = durationMs
    };
}