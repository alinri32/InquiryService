namespace InquiryService.Application.Contracts.Caching;

public interface IInquiryCacheService
{
    Task<string?> GetAsync(string key, bool bypassCache = false);
    Task SetAsync(string key, string value, TimeSpan expiration);
}