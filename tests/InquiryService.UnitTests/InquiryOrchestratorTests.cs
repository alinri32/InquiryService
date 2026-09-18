using InquiryService.Application.Contracts.Caching;
using InquiryService.Application.Contracts.Infrastructure;
using InquiryService.Application.Contracts.Providers;
using InquiryService.Application.DTOs;
using InquiryService.Application.Services;
using InquiryService.Domain.Common;
using InquiryService.Domain.Entities;
using InquiryService.Domain.Enums;
using Moq;

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

        _provider1Mock.Setup(p => p.Name).Returns("Provider_1");
        _provider1Mock.Setup(p => p.Priority).Returns(1);

        _provider2Mock.Setup(p => p.Name).Returns("Provider_2");
        _provider2Mock.Setup(p => p.Priority).Returns(2);

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
        var sut = CreateSut();
        var request = new InquiryRequestDto("0011223344", "NationalCode");
        const string key = "KEY_TECH_FAIL";

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.TechnicalError("خطای فنی پرووایدر اول", 100));

        _provider2Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Success("{\"status\":\"ok\"}", 80));

        var result = await sut.ProcessInquiryAsync(key, request);

        Assert.True(result.Success);
        Assert.Equal("Provider_2", result.Data!.SuccessfulProvider);
        Assert.Equal(InquiryStatus.Completed, result.Data.Status);

        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.AddProviderAttemptAsync(It.IsAny<InquiryProviderAttempt>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessInquiry_WhenProvider1TimesOut_ShouldFailoverToProvider2()
    {
        var sut = CreateSut();
        var request = new InquiryRequestDto("0011223344", "NationalCode");
        const string key = "KEY_TIMEOUT";

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Timeout(5000));

        _provider2Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.Success("{\"status\":\"ok\"}", 100));

        var result = await sut.ProcessInquiryAsync(key, request);

        Assert.True(result.Success);
        Assert.Equal("Provider_2", result.Data!.SuccessfulProvider);

        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInquiry_WhenProvider1ReturnsBusinessError_ShouldNOTFailoverToProvider2()
    {
        var sut = CreateSut();
        var request = new InquiryRequestDto("0011223344", "NationalCode");
        const string key = "KEY_BUSINESS_ERR";

        _provider1Mock
            .Setup(p => p.ExecuteInquiryAsync(request.IdentityIdentifier, request.InquiryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderExecutionResult.BusinessError("کد ملی نامعتبر است.", null, 50));

        var result = await sut.ProcessInquiryAsync(key, request);

        Assert.False(result.Success);
        Assert.Equal("Provider_1", result.Data!.SuccessfulProvider);
        Assert.Equal(InquiryStatus.Failed, result.Data.Status);

        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.AddProviderAttemptAsync(It.IsAny<InquiryProviderAttempt>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInquiry_WhenDuplicateIdempotencyKeyExists_ShouldReturnPreviousResultWithoutCallingProviders()
    {
        var sut = CreateSut();
        var request = new InquiryRequestDto("0011223344", "NationalCode");
        const string key = "DUPLICATE_KEY";

        var existingInquiry = new Inquiry
        {
            Id = 55,
            TrackingNumber = "TRACK_EXISTING",
            IdempotencyKey = key,
            Status = InquiryStatus.Completed,
            SuccessfulProvider = "Provider_1",
            ResultPayload = "{\"data\":\"existing_result\"}"
        };

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInquiry);

        var result = await sut.ProcessInquiryAsync(key, request);

        Assert.True(result.Success);
        Assert.Equal("TRACK_EXISTING", result.Data!.TrackingNumber);

        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider2Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInquiry_WhenCacheExists_ShouldReturnCachedDataDirectly()
    {
        var sut = CreateSut();
        var request = new InquiryRequestDto("0011223344", "NationalCode", BypassCache: false);
        const string key = "KEY_CACHE";

        _cacheServiceMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), false))
            .ReturnsAsync("{\"cached\":true}");

        var result = await sut.ProcessInquiryAsync(key, request);

        Assert.True(result.Success);
        Assert.True(result.Data!.IsFromCache);
        Assert.Equal("Cache", result.Data.SuccessfulProvider);

        _repositoryMock.Verify(r => r.CreatePendingInquiryAsync(It.IsAny<Inquiry>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider1Mock.Verify(p => p.ExecuteInquiryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}