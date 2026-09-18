using Dapper;
using InquiryService.Application.Contracts.Infrastructure;
using InquiryService.Domain.Entities;
using Microsoft.Data.SqlClient;

namespace InquiryService.Infrastructure.Data.Repositories;

public class InquiryRepository : IInquiryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InquiryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Inquiry?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                Id,
                TrackingNumber,
                IdempotencyKey,
                IdentityIdentifier,
                InquiryType, 
                Status,
                SuccessfulProvider,
                ResultPayload,
                ErrorMessage,
                CreatedAt,
                CompletedAt
            FROM 
                dbo.Inquiries WITH (NOLOCK)
            WHERE 
                IdempotencyKey = @Key";

        var command = new CommandDefinition(sql, new { Key = idempotencyKey }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Inquiry>(command);
    }

    public async Task<Inquiry> CreatePendingInquiryAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string insertSql = @"
            INSERT INTO 
                dbo.Inquiries 
                (
                    TrackingNumber,
                    IdempotencyKey,
                    IdentityIdentifier, 
                    InquiryType,
                    Status,
                    CreatedAt
                )
            OUTPUT 
                INSERTED.Id,
                INSERTED.TrackingNumber,
                INSERTED.IdempotencyKey, 
                INSERTED.IdentityIdentifier,
                INSERTED.InquiryType,
                INSERTED.Status, 
                INSERTED.SuccessfulProvider,
                INSERTED.ResultPayload,
                INSERTED.ErrorMessage, 
                INSERTED.CreatedAt,
                INSERTED.CompletedAt
            VALUES 
            (
                @TrackingNumber,
                @IdempotencyKey,
                @IdentityIdentifier, 
                @InquiryType,
                @Status,
                SYSUTCDATETIME()
            )";

        try
        {
            var command = new CommandDefinition(insertSql, new
            {
                inquiry.TrackingNumber,
                inquiry.IdempotencyKey,
                inquiry.IdentityIdentifier,
                inquiry.InquiryType,
                Status = (byte)inquiry.Status
            }, cancellationToken: cancellationToken);

            return await connection.QuerySingleAsync<Inquiry>(command);
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            // race condition: another request with the same idempotency key was processed concurrently
            var existing = await GetByIdempotencyKeyAsync(inquiry.IdempotencyKey, cancellationToken);
            if (existing != null)
            {
                return existing;
            }

            throw;
        }
    }

    public async Task UpdateInquiryStatusAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string updateSql = @"
            UPDATE 
                dbo.Inquiries
            SET 
                Status = @Status,
                SuccessfulProvider = @SuccessfulProvider,
                ResultPayload = @ResultPayload,
                ErrorMessage = @ErrorMessage,
                CompletedAt = SYSUTCDATETIME()
            WHERE
                Id = @Id";

        var command = new CommandDefinition(updateSql, new
        {
            inquiry.Id,
            Status = (byte)inquiry.Status,
            inquiry.SuccessfulProvider,
            inquiry.ResultPayload,
            inquiry.ErrorMessage
        }, cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task AddProviderAttemptAsync(InquiryProviderAttempt attempt, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string insertSql = @"
            INSERT INTO 
                dbo.InquiryProviderAttempts
                (
                    InquiryId,
                    ProviderName,
                    ExecutionOrder,
                    IsSuccess,
                    ErrorType,
                    HttpStatusCode,
                    RequestPayload,
                    ResponsePayload, 
                    DurationMs,
                    AttemptedAt
                )
                VALUES
                (
                    @InquiryId,
                    @ProviderName,
                    @ExecutionOrder, 
                    @IsSuccess, 
                    @ErrorType, 
                    @HttpStatusCode, 
                    @RequestPayload, 
                    @ResponsePayload, 
                    @DurationMs, 
                    SYSUTCDATETIME()
                )";

        var command = new CommandDefinition(insertSql, new
        {
            attempt.InquiryId,
            attempt.ProviderName,
            attempt.ExecutionOrder,
            attempt.IsSuccess,
            ErrorType = (byte)attempt.ErrorType,
            attempt.HttpStatusCode,
            attempt.RequestPayload,
            attempt.ResponsePayload,
            attempt.DurationMs
        }, cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<InquiryProviderAttempt>> GetAttemptsByInquiryIdAsync(long inquiryId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                Id,
                InquiryId, 
                ProviderName, 
                ExecutionOrder, 
                IsSuccess, 
                ErrorType,
                HttpStatusCode, 
                RequestPayload, ResponsePayload, 
                DurationMs, 
                AttemptedAt
            FROM
                dbo.InquiryProviderAttempts
            WHERE
                InquiryId = @InquiryId
            ORDER BY
                ExecutionOrder ASC;";

        var command = new CommandDefinition(sql, new { InquiryId = inquiryId }, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<InquiryProviderAttempt>(command);
        return [.. result];
    }
}