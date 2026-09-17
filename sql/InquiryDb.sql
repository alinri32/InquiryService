IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'InquiryDb')
BEGIN
    CREATE DATABASE InquiryDb;
END
GO

USE InquiryDb;
GO

IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'Ayan')
BEGIN
    CREATE LOGIN Ayan WITH PASSWORD = 'StrongP@ssw0rd!';
END
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'Ayan')
BEGIN
    CREATE USER Ayan FOR LOGIN Ayan;
    ALTER ROLE db_datareader ADD MEMBER Ayan;
    ALTER ROLE db_datawriter ADD MEMBER Ayan;
END
GO

IF OBJECT_ID(N'dbo.Inquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Inquiries
    (
        Id                 BIGINT IDENTITY(1, 1) NOT NULL,
        TrackingNumber     VARCHAR(32)           NOT NULL, 
        IdempotencyKey     VARCHAR(64)           NOT NULL, 
        IdentityIdentifier VARCHAR(20)           NOT NULL, 
        InquiryType        VARCHAR(20)           NOT NULL, 
        [Status]           TINYINT               NOT NULL, 
        SuccessfulProvider VARCHAR(30)           NULL,     
        ResultPayload      NVARCHAR(MAX)         NULL,     
        ErrorMessage       NVARCHAR(500)         NULL,     
        CreatedAt          DATETIME2(3)          NOT NULL DEFAULT SYSUTCDATETIME(), 
        CompletedAt        DATETIME2(3)          NULL,

        CONSTRAINT PK_Inquiries PRIMARY KEY CLUSTERED (Id ASC)
    ) WITH (DATA_COMPRESSION = PAGE);
END
GO

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'UX_Inquiries_IdempotencyKey')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Inquiries_IdempotencyKey
    ON dbo.Inquiries (IdempotencyKey ASC)
    WITH (DATA_COMPRESSION = PAGE);
END
GO

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_Inquiries_TrackingNumber')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Inquiries_TrackingNumber
    ON dbo.Inquiries (TrackingNumber ASC)
    WITH (DATA_COMPRESSION = PAGE);
END
GO

IF OBJECT_ID(N'dbo.InquiryProviderAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InquiryProviderAttempts
    (
        Id              BIGINT IDENTITY(1, 1) NOT NULL,
        InquiryId       BIGINT                NOT NULL,
        ProviderName    VARCHAR(30)           NOT NULL, 
        ExecutionOrder  TINYINT               NOT NULL, 
        IsSuccess       BIT                   NOT NULL, 
        ErrorType       TINYINT               NOT NULL, 
        HttpStatusCode  SMALLINT              NULL,     
        RequestPayload  NVARCHAR(MAX)         NULL,     
        ResponsePayload NVARCHAR(MAX)         NULL,     
        DurationMs      INT                   NOT NULL, 
        AttemptedAt     DATETIME2(3)          NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_InquiryProviderAttempts PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT FK_InquiryProviderAttempts_Inquiries FOREIGN KEY (InquiryId)
            REFERENCES dbo.Inquiries (Id)
            ON DELETE CASCADE
    ) WITH (DATA_COMPRESSION = PAGE);
END
GO

IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_InquiryProviderAttempts_InquiryId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_InquiryProviderAttempts_InquiryId
    ON dbo.InquiryProviderAttempts (InquiryId ASC)
    WITH (DATA_COMPRESSION = PAGE);
END
GO