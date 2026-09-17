using InquiryService.Application.Contracts.Caching;
using InquiryService.Application.Contracts.Infrastructure;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Application.DTOs;
using InquiryService.Application.Services;
using InquiryService.Domain.Common;
using InquiryService.Domain.Entities;
using InquiryService.Domain.Enums;
using Moq;
using Xunit;

namespace InquiryService.UnitTests;

public class InquiryOrchestratorTests
{
    private readonly Mock<IInquiryRepository> _repositoryMock;
    private readonly Mock<IInquiryCacheService> _cacheServiceMock;
    private readonly Mock<IInquiryProvider> _provider1Mock;
    private readonly Mock<IInquiryProvider> _provider2Mock;

    public InquiryOrchestratorTests()
    {
        _repositoryMock = new Mock<IInquiryRepository>();
        _cacheServiceMock = new Mock<IInquiryCacheService>();
        _provider1Mock = new Mock<IInquiryProvider>();
        _provider2Mock = new Mock<IInquiryProvider>();

        // تنظیم پیش‌فرض اولویت پرووایدرها
        _provider1Mock.Setup(p => p.Name).Returns("Provider_1");
        _provider1Mock.Setup(p => p.Priority).Returns(1);

        _provider2Mock.Setup(p => p.Name).Returns("Provider_2");
        _provider2Mock.Setup(p => p.Priority).Returns(2);

        // پیش‌فرض متد درج اولیه در دیتابیس
        _repositoryMock.Setup(r => r.CreatePendingInquiryAsync(It.IsAny<Inquiry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Inquiry inq, CancellationToken _) =>
            {
                inq.Id = 100;
                return inq;
            });
    }

    private InquiryOrchestrator CreateSut()
    {
        var providers = new List<IInquiryProvider> { _provider1Mock.Object, _provider2Mock.Object };
        return new InquiryOrchestrator(_repositoryMock.Object, _cacheServiceMock.Object, providers);
    }

    [Fact]
    public async Task ProcessInquiry_WhenProvider1FailsTechnically_ShouldFailoverToProvider2()
    {
        // Arrange (پرووایدر اول خطای فنی ۵۰۰ می‌دهد، پرووایدر دوم موفق می‌شود)
        var sut = CreateSut();
        var request = new InquiryRequestDto("KEY_TECH_FAIL", "0011223344", "NationalCode");

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.TechnicalError("خطای فنی پرووایدر اول", 100));

        _provider2Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Success("{\"status\":\"ok\"}", 80));

        // Act
        var result = await sut.ProcessInquiryAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Provider_2", result.Data!.SuccessfulProvider);
        Assert.Equal(InquiryStatus.Completed, result.Data.Status);

        // بررسی اینکه هر دو پرووایدر فراخوانی شدند (Failover رخ داده است)
        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);

        // بررسی ثبت تلاش‌ها در دیتابیس (۲ بار ثبت لاگ تلاش)
        _repositoryMock.Verify(r => r.AddProviderAttemptAsync(It.IsAny<InquiryProviderAttempt>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessInquiry_WhenProvider1TimesOut_ShouldFailoverToProvider2()
    {
        // Arrange (پرووایدر اول Timeout می‌دهد، پرووایدر دوم موفق می‌شود)
        var sut = CreateSut();
        var request = new InquiryRequestDto("KEY_TIMEOUT", "0011223344", "NationalCode");

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Timeout(5000));

        _provider2Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Success("{\"status\":\"ok\"}", 100));

        // Act
        var result = await sut.ProcessInquiryAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Provider_2", result.Data!.SuccessfulProvider);

        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInquiry_WhenProvider1ReturnsBusinessError_ShouldNOTFailoverToProvider2()
    {
        // Arrange (پرووایدر اول خطای Business می‌دهد؛ طبق تسک نباید به پرووایدر ۲ برود)
        var sut = CreateSut();
        var request = new InquiryRequestDto("KEY_BUSINESS_ERR", "0011223344", "NationalCode");

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.BusinessError("کد ملی نامعتبر است.", null, 50));

        // Act
        var result = await sut.ProcessInquiryAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Provider_1", result.Data!.SuccessfulProvider);
        Assert.Equal(InquiryStatus.Failed, result.Data.Status);

        // بررسی صریح: پرووایدر دوم هرگز نباید صدا زده شود
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // فقط ۱ تلاش در دیتابیس ثبت می‌شود
        _repositoryMock.Verify(r => r.AddProviderAttemptAsync(It.IsAny<InquiryProviderAttempt>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInquiry_WhenDuplicateIdempotencyKeyExists_ShouldReturnPreviousResultWithoutCallingProviders()
    {
        // Arrange (استعلام از قبل با همین کلید در وضعیت Completed وجود دارد)
        var sut = CreateSut();
        var request = new InquiryRequestDto("DUPLICATE_KEY", "0011223344", "NationalCode");

        var existingInquiry = new Inquiry
        {
            Id = 55,
            TrackingNumber = "TRACK_EXISTING",
            IdempotencyKey = "DUPLICATE_KEY",
            Status = InquiryStatus.Completed,
            SuccessfulProvider = "Provider_1",
            ResultPayload = "{\"data\":\"existing_result\"}"
        };

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(request.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInquiry);

        // Act
        var result = await sut.ProcessInquiryAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("TRACK_EXISTING", result.Data!.TrackingNumber);

        // هیچ پروایدری نباید فراخوانی شده باشد
        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInquiry_WhenCacheExists_ShouldReturnCachedDataDirectly()
    {
        // Arrange (نتیجه استعلام داخل کش هست و BypassCache=false)
        var sut = CreateSut();
        var request = new InquiryRequestDto("KEY_CACHE", "0011223344", "NationalCode", BypassCache: false);

        _cacheServiceMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), false))
            .ReturnsAsync("{\"cached\":true}");

        // Act
        var result = await sut.ProcessInquiryAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data!.IsFromCache);
        Assert.Equal("Cache", result.Data.SuccessfulProvider);

        // حتی دیتابیس هم نباید درج شود
        _repositoryMock.Verify(r => r.CreatePendingInquiryAsync(It.IsAny<Inquiry>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}