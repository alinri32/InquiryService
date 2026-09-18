using InquiryService.Application.Common;
using InquiryService.Application.Contracts.Caching;
using InquiryService.Application.Contracts.Infrastructure;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Application.DTOs;
using InquiryService.Domain.Entities;
using InquiryService.Domain.Enums;

namespace InquiryService.Application.Services;

public class InquiryOrchestrator : IInquiryOrchestrator
{
    private readonly IInquiryRepository _repository;
    private readonly IInquiryCacheService _cacheService;
    private readonly IEnumerable<IInquiryProvider> _providers;
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(10);

    public InquiryOrchestrator(
        IInquiryRepository repository,
        IInquiryCacheService cacheService,
        IEnumerable<IInquiryProvider> providers)
    {
        _repository = repository;
        _cacheService = cacheService;
        _providers = providers;
    }

    public async Task<ApiResponse<InquiryResponseDto>> ProcessInquiryAsync(
        string idempotencyKey,
        InquiryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // ۱. اعتبارسنجی کلید ارسال‌شده در هدر
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ApiResponse<InquiryResponseDto>.Fail("هدر 'Idempotency-Key' الزامی است.");
        }

        if (idempotencyKey.Length > 64)
        {
            return ApiResponse<InquiryResponseDto>.Fail("طول 'Idempotency-Key' نمی‌تواند بیشتر از ۶۴ کاراکتر باشد.");
        }

        // ۲. اعتبارسنجی بدنه درخواست
        var (isValid, validationError) = request.Validate();
        if (!isValid)
        {
            return ApiResponse<InquiryResponseDto>.Fail(validationError!);
        }

        // ۳. بررسی کلید کش بیزینسی بر اساس مشخصه استعلام
        string cacheKey = $"inquiry:{request.InquiryType}:{request.IdentityIdentifier}";
        var cachedPayload = await _cacheService.GetAsync(cacheKey, request.BypassCache);
        if (cachedPayload != null)
        {
            var cachedResponse = new InquiryResponseDto(
                TrackingNumber: "FROM_CACHE",
                IdempotencyKey: idempotencyKey,
                Status: InquiryStatus.Completed,
                SuccessfulProvider: "Cache",
                ResultPayload: cachedPayload,
                ErrorMessage: null,
                IsFromCache: true,
                CreatedAt: DateTime.UtcNow,
                CompletedAt: DateTime.UtcNow);

            return ApiResponse<InquiryResponseDto>.Ok(cachedResponse, "پاسخ از طریق کش دریافت شد.");
        }

        // ۴. بررسی Idempotency در دیتابیس برای جلوگیری از پردازش تکراری
        var existingInquiry = await _repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingInquiry != null)
        {
            if (existingInquiry.Status == InquiryStatus.Pending)
            {
                return ApiResponse<InquiryResponseDto>.Fail("درخواست با این شناسه در حال حاضر در حال پردازش است.");
            }

            var duplicateResponse = MapToDto(existingInquiry, isFromCache: false);
            return ApiResponse<InquiryResponseDto>.Ok(duplicateResponse, "نتیجه استعلام قبلاً ثبت شده است.");
        }

        // ۵. ایجاد رکورد استعلام با وضعیت Pending در دیتابیس
        var newInquiry = new Inquiry
        {
            TrackingNumber = Guid.NewGuid().ToString("N")[..16],
            IdempotencyKey = idempotencyKey,
            IdentityIdentifier = request.IdentityIdentifier,
            InquiryType = request.InquiryType,
            Status = InquiryStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var currentInquiry = await _repository.CreatePendingInquiryAsync(newInquiry, cancellationToken);

        if (currentInquiry.Status != InquiryStatus.Pending)
        {
            return ApiResponse<InquiryResponseDto>.Ok(MapToDto(currentInquiry, isFromCache: false));
        }

        // ۶. مرتب‌سازی پرووایدرها بر اساس اولویت
        var orderedProviders = _providers.OrderBy(p => p.Priority).ToList();
        if (!orderedProviders.Any())
        {
            currentInquiry.Status = InquiryStatus.Failed;
            currentInquiry.ErrorMessage = "هیچ پرووایدری برای ارائه خدمت پیکربندی نشده است.";
            await _repository.UpdateInquiryStatusAsync(currentInquiry, cancellationToken);
            return ApiResponse<InquiryResponseDto>.Fail(currentInquiry.ErrorMessage);
        }

        byte attemptOrder = 1;
        bool isResolved = false;

        // ۷. حلقه Failover روی پرووایدرها
        foreach (var provider in orderedProviders)
        {
            var executionResult = await provider.ExecuteInquiryAsync(
                request.IdentityIdentifier,
                request.InquiryType,
                cancellationToken);

            var attemptLog = new InquiryProviderAttempt
            {
                InquiryId = currentInquiry.Id,
                ProviderName = provider.Name,
                ExecutionOrder = attemptOrder++,
                IsSuccess = executionResult.IsSuccess,
                ErrorType = executionResult.ErrorType,
                HttpStatusCode = executionResult.HttpStatusCode,
                RequestPayload = $"{request.InquiryType}:{request.IdentityIdentifier}",
                ResponsePayload = executionResult.RawResponse ?? executionResult.ErrorMessage,
                DurationMs = executionResult.DurationMs,
                AttemptedAt = DateTime.UtcNow
            };
            await _repository.AddProviderAttemptAsync(attemptLog, cancellationToken);

            // سناریو الف: پاسخ موفق
            if (executionResult.IsSuccess)
            {
                currentInquiry.Status = InquiryStatus.Completed;
                currentInquiry.SuccessfulProvider = provider.Name;
                currentInquiry.ResultPayload = executionResult.RawResponse;
                currentInquiry.ErrorMessage = null;
                currentInquiry.CompletedAt = DateTime.UtcNow;

                await _repository.UpdateInquiryStatusAsync(currentInquiry, cancellationToken);
                await _cacheService.SetAsync(cacheKey, executionResult.RawResponse!, DefaultCacheDuration);

                isResolved = true;
                break;
            }

            // سناریو ب: خطای Business (توقف چرخه Failover)
            if (executionResult.ErrorType == ProviderErrorType.BusinessError)
            {
                currentInquiry.Status = InquiryStatus.Failed;
                currentInquiry.SuccessfulProvider = provider.Name;
                currentInquiry.ErrorMessage = executionResult.ErrorMessage ?? "خطای بیزینسی از سرویس‌دهنده دریافت شد.";
                currentInquiry.ResultPayload = executionResult.RawResponse;
                currentInquiry.CompletedAt = DateTime.UtcNow;

                await _repository.UpdateInquiryStatusAsync(currentInquiry, cancellationToken);

                isResolved = true;
                break;
            }

            // سناریو ج: خطای Technical یا Timeout -> انتقال به پرووایدر بعدی
        }

        if (!isResolved)
        {
            currentInquiry.Status = InquiryStatus.Failed;
            currentInquiry.ErrorMessage = "تمامی پرووایدرها به دلیل خطای فنی یا تایم‌اوت پاسخگو نبودند.";
            currentInquiry.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateInquiryStatusAsync(currentInquiry, cancellationToken);
        }

        var finalResponse = MapToDto(currentInquiry, isFromCache: false);
        return currentInquiry.Status == InquiryStatus.Completed
            ? ApiResponse<InquiryResponseDto>.Ok(finalResponse)
            : ApiResponse<InquiryResponseDto>.Fail(
                currentInquiry.ErrorMessage ?? "عملیات استعلام ناموفق بود.",
                new[] { currentInquiry.ErrorMessage ?? string.Empty },
                finalResponse);
    }

    private static InquiryResponseDto MapToDto(Inquiry entity, bool isFromCache) => new(
        TrackingNumber: entity.TrackingNumber,
        IdempotencyKey: entity.IdempotencyKey,
        Status: entity.Status,
        SuccessfulProvider: entity.SuccessfulProvider,
        ResultPayload: entity.ResultPayload,
        ErrorMessage: entity.ErrorMessage,
        IsFromCache: isFromCache,
        CreatedAt: entity.CreatedAt,
        CompletedAt: entity.CompletedAt);
}