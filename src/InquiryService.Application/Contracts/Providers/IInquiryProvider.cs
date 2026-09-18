using InquiryService.Domain.Common;

namespace InquiryService.Application.Contracts.Providers;

public interface IInquiryProvider
{
    /// <summary>
    /// Provider name (e.g., "ProviderA", "ProviderB", etc.)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Provider priority (lower number indicates higher priority, e.g., 1 before 2)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Execute inquiry with CancellationToken support for timeout management
    /// </summary>
    Task<ProviderExecutionResult> ExecuteInquiryAsync(
        string identityIdentifier,
        string inquiryType,
        CancellationToken cancellationToken = default);
}