-- Quoted reply target on support chat messages.
-- Run in SSMS against the Resturanyar database.

IF OBJECT_ID(N'dbo.SupportMessages', N'U') IS NULL
BEGIN
    RAISERROR(N'dbo.SupportMessages was not found.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SupportMessages')
      AND name = N'ReplyToMessageId'
)
BEGIN
    ALTER TABLE dbo.SupportMessages
        ADD ReplyToMessageId BIGINT NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SupportMessages')
      AND name = N'ReplyToMessageId'
)
AND OBJECT_ID(N'dbo.FK_SupportMessages_ReplyTo', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.SupportMessages
        ADD CONSTRAINT FK_SupportMessages_ReplyTo
            FOREIGN KEY (ReplyToMessageId) REFERENCES dbo.SupportMessages(Id);
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SupportMessages')
      AND name = N'ReplyToMessageId'
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SupportMessages_ReplyTo'
      AND object_id = OBJECT_ID(N'dbo.SupportMessages')
)
BEGIN
    CREATE INDEX IX_SupportMessages_ReplyTo
        ON dbo.SupportMessages(ReplyToMessageId)
        WHERE ReplyToMessageId IS NOT NULL;
END
GO
