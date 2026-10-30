/* ============================================================
   One-time web SSO tickets (Android → web auto-login).
   Additive / production-safe / idempotent.
   Review and run manually in SSMS — do not auto-deploy / no EF migration.
   ============================================================ */

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.WebLoginTickets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebLoginTickets
    (
        Id              INT            IDENTITY(1,1) NOT NULL,
        TokenHash       NVARCHAR(88)   NOT NULL,
        OwnerId         INT            NOT NULL,
        RestaurantId    INT            NOT NULL,
        Target          NVARCHAR(32)   NOT NULL,
        ExpiresAtUtc    DATETIME2      NOT NULL,
        UsedAtUtc       DATETIME2      NULL,
        CreatedAtUtc    DATETIME2      NOT NULL
            CONSTRAINT DF_WebLoginTickets_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedIp       NVARCHAR(64)   NULL,
        CONSTRAINT PK_WebLoginTickets PRIMARY KEY (Id),
        CONSTRAINT UQ_WebLoginTickets_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_WebLoginTickets_Owners_OwnerId
            FOREIGN KEY (OwnerId) REFERENCES dbo.Owners (Id),
        CONSTRAINT FK_WebLoginTickets_Restaurants_RestaurantId
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants (restaurant_id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WebLoginTickets_OwnerId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.WebLoginTickets')
)
BEGIN
    CREATE INDEX IX_WebLoginTickets_OwnerId_CreatedAtUtc
        ON dbo.WebLoginTickets (OwnerId, CreatedAtUtc DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WebLoginTickets_ExpiresAtUtc'
      AND object_id = OBJECT_ID(N'dbo.WebLoginTickets')
)
BEGIN
    CREATE INDEX IX_WebLoginTickets_ExpiresAtUtc
        ON dbo.WebLoginTickets (ExpiresAtUtc)
        WHERE UsedAtUtc IS NULL;
END;
GO
