/* Phase 0 — safe, additive. Run on production after backup.
   Does NOT alter RefreshTokens, Owners, or Users columns.
   Confirm Users/Restaurants table + PK names before COMMIT if yours differ. */
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID(N'dbo.StaffRefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffRefreshTokens
    (
        Id             INT            NOT NULL IDENTITY(1,1),
        Token          NVARCHAR(512)  NOT NULL,
        ExpiryTime     DATETIME2(7)   NOT NULL,
        UserId         INT            NOT NULL,
        RestaurantId   INT            NOT NULL,
        CreatedAtUtc   DATETIME2(7)   NOT NULL
            CONSTRAINT DF_StaffRefreshTokens_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_StaffRefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_StaffRefreshTokens_Token UNIQUE (Token),

        CONSTRAINT FK_StaffRefreshTokens_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(user_id)
            ON DELETE CASCADE,

        CONSTRAINT FK_StaffRefreshTokens_Restaurants
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants(restaurant_id)
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX IX_StaffRefreshTokens_UserId
        ON dbo.StaffRefreshTokens(UserId);

    CREATE NONCLUSTERED INDEX IX_StaffRefreshTokens_ExpiryTime
        ON dbo.StaffRefreshTokens(ExpiryTime);
END
ELSE
BEGIN
    PRINT 'StaffRefreshTokens already exists — no change.';
END

COMMIT;

/* Rollback if needed (only before app uses the table):
   DROP TABLE dbo.StaffRefreshTokens;
*/
