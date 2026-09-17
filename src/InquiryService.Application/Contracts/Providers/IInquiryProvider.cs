using InquiryService.Domain.Common;

namespace InquiryService.Application.Contracts.Providers;

public interface IInquiryProvider
{
    /// <summary>
    /// نام پرووایدر جهت لاگ و ثبت در دیتابیس
    /// </summary>
    string Name { get; }

    /// <summary>
    /// اولویت پرووایدر (عدد کمتر به معنی اولویت بالاتر است، مثلاً ۱ قبل از ۲ اجرا می‌شود)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// اجرای استعلام با پشتیبانی از CancellationToken جهت مدیریت Timeout
    /// </summary>
    Task<ProviderExecutionResult> ExecuteInquiryAsync(
        string identityIdentifier,
        string inquiryType,
        CancellationToken cancellationToken = default);
}