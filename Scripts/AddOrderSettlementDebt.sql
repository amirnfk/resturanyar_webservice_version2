/* ============================================================
   Order settlement debt (نسیه / partial pay) — links ledger to orders.
   Additive / production-safe / idempotent.
   Review and run manually in SSMS — do not auto-deploy / no EF migration.
   Run BEFORE deploying app code that maps the new columns.
   ============================================================ */

SET NOCOUNT ON;
GO

/* ---- Orders: settlement snapshot fields ---- */
IF COL_LENGTH(N'dbo.Orders', N'SettlementPaidAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Orders
    ADD SettlementPaidAmount DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH(N'dbo.Orders', N'SettlementDebtAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Orders
    ADD SettlementDebtAmount DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH(N'dbo.Orders', N'SettlementType') IS NULL
BEGIN
    ALTER TABLE dbo.Orders
    ADD SettlementType NVARCHAR(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_Orders_SettlementType'
      AND parent_object_id = OBJECT_ID(N'dbo.Orders')
)
BEGIN
    ALTER TABLE dbo.Orders
    ADD CONSTRAINT CK_Orders_SettlementType
        CHECK (SettlementType IS NULL OR SettlementType IN (N'Cash', N'Credit', N'Partial'));
END;
GO

/* ---- CustomerAccountTransactions: order link ---- */
IF COL_LENGTH(N'dbo.CustomerAccountTransactions', N'OrderId') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerAccountTransactions
    ADD OrderId INT NULL;
END;
GO

IF COL_LENGTH(N'dbo.CustomerAccountTransactions', N'PaidAmount') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerAccountTransactions
    ADD PaidAmount DECIMAL(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_CustomerAccountTransactions_Orders_OrderId'
)
BEGIN
    ALTER TABLE dbo.CustomerAccountTransactions
    ADD CONSTRAINT FK_CustomerAccountTransactions_Orders_OrderId
        FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId)
        ON DELETE SET NULL;
END;
GO

/* One Debt ledger row per order (manual debts stay OrderId-null). */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CustomerAccountTransactions_OrderDebt'
      AND object_id = OBJECT_ID(N'dbo.CustomerAccountTransactions')
)
BEGIN
    CREATE UNIQUE INDEX UX_CustomerAccountTransactions_OrderDebt
        ON dbo.CustomerAccountTransactions (OrderId)
        WHERE OrderId IS NOT NULL AND Type = N'Debt';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerAccountTransactions_OrderId'
      AND object_id = OBJECT_ID(N'dbo.CustomerAccountTransactions')
)
BEGIN
    CREATE INDEX IX_CustomerAccountTransactions_OrderId
        ON dbo.CustomerAccountTransactions (OrderId)
        WHERE OrderId IS NOT NULL;
END;
GO

PRINT N'AddOrderSettlementDebt.sql completed.';
GO
