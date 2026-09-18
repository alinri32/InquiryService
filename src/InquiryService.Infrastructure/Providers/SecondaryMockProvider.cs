using System.Diagnostics;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Domain.Common;

namespace InquiryService.Infrastructure.Providers;

public class SecondaryMockProvider : IInquiryProvider
{
    public string Name => "SecondaryProvider_B";
    public int Priority => 2;

    public async Task<ProviderExecutionResult> ExecuteInquiryAsync(
        string identityIdentifier,
        string inquiryType,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Error simulation for specific identifiers
        if (identityIdentifier == "8888888888")
        {
            await Task.Delay(100, cancellationToken);
            stopwatch.Stop();
            return ProviderExecutionResult.TechnicalError("سرویس‌دهنده پشتیبان نیز قادر به پاسخگویی نیست.", (int)stopwatch.ElapsedMilliseconds);
        }

        // Simulate a successful response for other identifiers
        await Task.Delay(120, cancellationToken);
        stopwatch.Stop();
        string mockData = $"{{\"provider\":\"{Name}\", \"identifier\":\"{identityIdentifier}\", \"isVerified\":true, \"note\":\"Failover Succeeded\", \"timestamp\":\"{DateTime.UtcNow:O}\"}}";
        return ProviderExecutionResult.Success(mockData, (int)stopwatch.ElapsedMilliseconds);
    }
}