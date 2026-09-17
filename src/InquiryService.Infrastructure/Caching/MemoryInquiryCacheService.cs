using InquiryService.Application.Contracts.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace InquiryService.Infrastructure.Caching;

public class MemoryInquiryCacheService : IInquiryCacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryInquiryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<string?> GetAsync(string key, bool bypassCache = false)
    {
        if (bypassCache)
        {
            return Task.FromResult<string?>(null);
        }

        _memoryCache.TryGetValue(key, out string? cachedResult);
        return Task.FromResult(cachedResult);
    }

    public Task SetAsync(string key, string value, TimeSpan expiration)
    {
        _memoryCache.Set(key, value, expiration);
        return Task.CompletedTask;
    }
}