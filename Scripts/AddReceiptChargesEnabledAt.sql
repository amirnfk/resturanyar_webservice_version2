/* ============================================================
   Forward-only charge estimates: track when charges were enabled.
   Run manually on live DB.
   ============================================================ */

IF COL_LENGTH('dbo.Restaurants', 'ReceiptChargesEnabledAt') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD ReceiptChargesEnabledAt DATETIME2 NULL;
END;
GO

-- Existing charge-enabled restaurants: estimates apply only to orders from now on.
UPDATE dbo.Restaurants
SET ReceiptChargesEnabledAt = GETDATE()
WHERE ReceiptChargesEnabled = 1
  AND ReceiptChargesEnabledAt IS NULL;
GO
