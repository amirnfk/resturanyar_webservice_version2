/* ========== Support Chat DDL — run in SSMS before enabling built-in chat ========== */
/* Agent does not apply this. IsEnabled defaults to 0 so Goftino stays live. */

IF OBJECT_ID(N'dbo.SupportChatSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportChatSettings
    (
        Id                    INT            NOT NULL CONSTRAINT PK_SupportChatSettings PRIMARY KEY,
        IsEnabled             BIT            NOT NULL CONSTRAINT DF_SupportChatSettings_IsEnabled DEFAULT (0),
        SmsNotifyWhenOffline  BIT            NOT NULL CONSTRAINT DF_SupportChatSettings_Sms DEFAULT (1),
        SmsThrottleHours      INT            NOT NULL CONSTRAINT DF_SupportChatSettings_Throttle DEFAULT (3),
        UpdatedAtUtc          DATETIME2(0)   NOT NULL CONSTRAINT DF_SupportChatSettings_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_SupportChatSettings_SingleRow CHECK (Id = 1),
        CONSTRAINT CK_SupportChatSettings_Throttle CHECK (SmsThrottleHours BETWEEN 1 AND 72)
    );

    INSERT INTO dbo.SupportChatSettings (Id, IsEnabled, SmsNotifyWhenOffline, SmsThrottleHours)
    VALUES (1, 1, 1, 3);
END
GO

-- If you already ran the earlier script with IsEnabled=0, enable built-in chat:
IF OBJECT_ID(N'dbo.SupportChatSettings', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.SupportChatSettings
    SET IsEnabled = 1, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = 1 AND IsEnabled = 0;
END
GO

IF OBJECT_ID(N'dbo.SupportConversations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportConversations
    (
        Id                      BIGINT         NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_SupportConversations PRIMARY KEY,
        RestaurantId            INT            NULL,
        OwnerId                 INT            NULL,
        GuestKey                NVARCHAR(64)   NULL,
        RestaurantName          NVARCHAR(200)  NULL,
        OwnerName               NVARCHAR(200)  NULL,
        OwnerPhone              NVARCHAR(20)   NULL,
        LastPageUrl             NVARCHAR(500)  NULL,
        UserAgent               NVARCHAR(500)  NULL,
        LastMessageAtUtc        DATETIME2(0)   NOT NULL
            CONSTRAINT DF_SupportConversations_LastMsg DEFAULT (SYSUTCDATETIME()),
        LastCustomerMessageAtUtc DATETIME2(0)  NULL,
        LastSmsSentAtUtc        DATETIME2(0)   NULL,
        UnreadBySupport         INT            NOT NULL
            CONSTRAINT DF_SupportConversations_UnreadS DEFAULT (0),
        UnreadByCustomer        INT            NOT NULL
            CONSTRAINT DF_SupportConversations_UnreadC DEFAULT (0),
        CreatedAtUtc            DATETIME2(0)   NOT NULL
            CONSTRAINT DF_SupportConversations_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_SupportConversations_Identity CHECK (
            (RestaurantId IS NOT NULL AND GuestKey IS NULL)
            OR (RestaurantId IS NULL AND GuestKey IS NOT NULL)
        )
    );

    CREATE UNIQUE INDEX UX_SupportConversations_RestaurantId
        ON dbo.SupportConversations(RestaurantId)
        WHERE RestaurantId IS NOT NULL;

    CREATE UNIQUE INDEX UX_SupportConversations_GuestKey
        ON dbo.SupportConversations(GuestKey)
        WHERE GuestKey IS NOT NULL;

    CREATE INDEX IX_SupportConversations_LastMessageAt
        ON dbo.SupportConversations(LastMessageAtUtc DESC);
END
GO

IF OBJECT_ID(N'dbo.SupportMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportMessages
    (
        Id               BIGINT          NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_SupportMessages PRIMARY KEY,
        ConversationId   BIGINT          NOT NULL,
        SenderType       TINYINT         NOT NULL,
        Body             NVARCHAR(2000)  NULL,
        ImageUrl         NVARCHAR(500)   NULL,
        ClientMessageId  UNIQUEIDENTIFIER NULL,
        CreatedAtUtc     DATETIME2(0)    NOT NULL
            CONSTRAINT DF_SupportMessages_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SupportMessages_Conversation
            FOREIGN KEY (ConversationId) REFERENCES dbo.SupportConversations(Id),
        CONSTRAINT CK_SupportMessages_Sender CHECK (SenderType IN (0, 1)),
        CONSTRAINT CK_SupportMessages_Content CHECK (
            (Body IS NOT NULL AND LEN(LTRIM(RTRIM(Body))) > 0)
            OR (ImageUrl IS NOT NULL AND LEN(ImageUrl) > 0)
        )
    );

    CREATE INDEX IX_SupportMessages_Conversation_Created
        ON dbo.SupportMessages(ConversationId, CreatedAtUtc);

    CREATE UNIQUE INDEX UX_SupportMessages_ClientMessageId
        ON dbo.SupportMessages(ConversationId, ClientMessageId)
        WHERE ClientMessageId IS NOT NULL;
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
