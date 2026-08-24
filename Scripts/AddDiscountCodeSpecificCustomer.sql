/* ============================================================
   Add optional SpecificCustomerId to RestaurantDiscountCodes.
   Additive / production-safe / idempotent.
   Review and run manually — do not auto-deploy.
   ============================================================ */

SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.RestaurantDiscountCodes', 'SpecificCustomerId') IS NULL
BEGIN
    ALTER TABLE dbo.RestaurantDiscountCodes
    ADD SpecificCustomerId INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_RestaurantDiscountCodes_Customers_SpecificCustomerId'
)
BEGIN
    ALTER TABLE dbo.RestaurantDiscountCodes
    ADD CONSTRAINT FK_RestaurantDiscountCodes_Customers_SpecificCustomerId
        FOREIGN KEY (SpecificCustomerId) REFERENCES dbo.Customers (CustomerId)
        ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RestaurantDiscountCodes_SpecificCustomerId'
      AND object_id = OBJECT_ID(N'dbo.RestaurantDiscountCodes')
)
BEGIN
    CREATE INDEX IX_RestaurantDiscountCodes_SpecificCustomerId
        ON dbo.RestaurantDiscountCodes (SpecificCustomerId)
        WHERE SpecificCustomerId IS NOT NULL;
END;
GO

PRINT N'AddDiscountCodeSpecificCustomer.sql completed.';
GO
