using System.Diagnostics;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Domain.Common;

namespace InquiryService.Infrastructure.Providers;

public class PrimaryMockProvider : IInquiryProvider
{
    public string Name => "PrimaryProvider_A";
    public int Priority => 1; // اولویت اول

    public async Task<ProviderExecutionResult> ExecuteInquiryAsync(
        string identityIdentifier,
        string inquiryType,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ۱. سناریوی شبیه‌سازی Timeout
        if (identityIdentifier.StartsWith("999"))
        {
            await Task.Delay(500, cancellationToken); // شبیه‌سازی زمان انتظار
            stopwatch.Stop();
            return ProviderExecutionResult.Timeout((int)stopwatch.ElapsedMilliseconds);
        }

        // ۲. سناریوی خطای فنی (Technical Error - باعث Failover می‌شود)
        if (identityIdentifier.StartsWith("888"))
        {
            await Task.Delay(100, cancellationToken);
            stopwatch.Stop();
            return ProviderExecutionResult.TechnicalError("سرویس‌دهنده اصلی موقتاً از دسترس خارج است (500).", (int)stopwatch.ElapsedMilliseconds);
        }

        // ۳. سناریوی خطای Business (نباید Failover رخ دهد)
        if (identityIdentifier.StartsWith("777"))
        {
            await Task.Delay(50, cancellationToken);
            stopwatch.Stop();
            return ProviderExecutionResult.BusinessError("کد ملی استعلام‌شده در سامانه یافت نشد.", null, (int)stopwatch.ElapsedMilliseconds);
        }

        // ۴. پاسخ موفقیت‌آمیز
        await Task.Delay(80, cancellationToken);
        stopwatch.Stop();
        string mockData = $"{{\"provider\":\"{Name}\", \"identifier\":\"{identityIdentifier}\", \"isVerified\":true, \"timestamp\":\"{DateTime.UtcNow:O}\"}}";
        return ProviderExecutionResult.Success(mockData, (int)stopwatch.ElapsedMilliseconds);
    }
}