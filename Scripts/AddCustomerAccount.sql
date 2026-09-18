/* ============================================================
   Customer account ledger (AR-lite) — phase 1, independent of orders.
   Additive / production-safe / idempotent.
   Review and run manually in SSMS — do not auto-deploy / no EF migration.
   ============================================================ */

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.CustomerAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAccounts
    (
        AccountId       INT            IDENTITY(1,1) NOT NULL,
        RestaurantId    INT            NOT NULL,
        CustomerId      INT            NOT NULL,
        CurrentBalance  DECIMAL(18,2)  NOT NULL
            CONSTRAINT DF_CustomerAccounts_CurrentBalance DEFAULT (0),
        CreatedAt       DATETIME2      NOT NULL,
        UpdatedAt       DATETIME2      NOT NULL,
        CONSTRAINT PK_CustomerAccounts PRIMARY KEY (AccountId),
        CONSTRAINT UQ_CustomerAccounts_CustomerId UNIQUE (CustomerId),
        CONSTRAINT FK_CustomerAccounts_Customers_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerAccounts_RestaurantId'
      AND object_id = OBJECT_ID(N'dbo.CustomerAccounts')
)
BEGIN
    CREATE INDEX IX_CustomerAccounts_RestaurantId
        ON dbo.CustomerAccounts (RestaurantId);
END;
GO

IF OBJECT_ID(N'dbo.CustomerAccountTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAccountTransactions
    (
        TransactionId         INT            IDENTITY(1,1) NOT NULL,
        AccountId             INT            NOT NULL,
        RestaurantId          INT            NOT NULL,
        CustomerId            INT            NOT NULL,
        Type                  NVARCHAR(20)   NOT NULL,
        Amount                DECIMAL(18,2)  NOT NULL,
        SignedAmount          DECIMAL(18,2)  NOT NULL,
        BalanceAfter          DECIMAL(18,2)  NOT NULL,
        Note                  NVARCHAR(500)  NULL,
        CreatedAt             DATETIME2      NOT NULL,
        CreatedByOwnerId      INT            NULL,
        CreatedByStaffUserId  INT            NULL,
        CONSTRAINT PK_CustomerAccountTransactions PRIMARY KEY (TransactionId),
        CONSTRAINT FK_CustomerAccountTransactions_Account
            FOREIGN KEY (AccountId) REFERENCES dbo.CustomerAccounts (AccountId),
        CONSTRAINT CK_CustomerAccountTransactions_AmountPositive
            CHECK (Amount > 0),
        CONSTRAINT CK_CustomerAccountTransactions_Type
            CHECK (Type IN (N'Debt', N'Payment', N'Adjustment'))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerAccountTransactions_AccountId_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.CustomerAccountTransactions')
)
BEGIN
    CREATE INDEX IX_CustomerAccountTransactions_AccountId_CreatedAt
        ON dbo.CustomerAccountTransactions (AccountId, CreatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerAccountTransactions_Restaurant_Customer_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.CustomerAccountTransactions')
)
BEGIN
    CREATE INDEX IX_CustomerAccountTransactions_Restaurant_Customer_CreatedAt
        ON dbo.CustomerAccountTransactions (RestaurantId, CustomerId, CreatedAt DESC);
END;
GO

PRINT N'AddCustomerAccount.sql completed.';
GO
