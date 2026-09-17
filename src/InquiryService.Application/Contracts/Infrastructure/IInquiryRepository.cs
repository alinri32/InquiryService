using InquiryService.Domain.Entities;

namespace InquiryService.Application.Contracts.Infrastructure;

public interface IInquiryRepository
{
    Task<Inquiry?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Inquiry> CreatePendingInquiryAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
    Task UpdateInquiryStatusAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
    Task AddProviderAttemptAsync(InquiryProviderAttempt attempt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InquiryProviderAttempt>> GetAttemptsByInquiryIdAsync(long inquiryId, CancellationToken cancellationToken = default);
}