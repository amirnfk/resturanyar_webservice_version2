-- Enable receipt/charges feature for all restaurants (default ON going forward).
-- Safe to re-run: already-enabled restaurants keep their original ReceiptChargesEnabledAt.

-- 1) Preview
SELECT
    COUNT(*) AS TotalRestaurants,
    SUM(CASE WHEN ReceiptChargesEnabled = 1 THEN 1 ELSE 0 END) AS AlreadyEnabled,
    SUM(CASE WHEN ReceiptChargesEnabled = 0 THEN 1 ELSE 0 END) AS WillEnable
FROM dbo.Restaurants;

-- 2) Enable all currently-disabled restaurants
BEGIN TRAN;

UPDATE dbo.Restaurants
SET
    ReceiptChargesEnabled = 1,
    ReceiptChargesEnabledAt = GETDATE()
WHERE ReceiptChargesEnabled = 0;

SELECT COUNT(*) AS StillDisabled
FROM dbo.Restaurants
WHERE ReceiptChargesEnabled = 0;

SELECT TOP 20
    restaurant_id,
    name,
    ReceiptChargesEnabled,
    ReceiptChargesEnabledAt
FROM dbo.Restaurants
ORDER BY restaurant_id DESC;

COMMIT TRAN;
-- If anything looks wrong: ROLLBACK TRAN;

-- 3) Change DB default so future inserts are ON
IF OBJECT_ID('dbo.DF_Restaurants_ReceiptChargesEnabled', 'D') IS NOT NULL
    ALTER TABLE dbo.Restaurants DROP CONSTRAINT DF_Restaurants_ReceiptChargesEnabled;

ALTER TABLE dbo.Restaurants
ADD CONSTRAINT DF_Restaurants_ReceiptChargesEnabled
DEFAULT (1) FOR ReceiptChargesEnabled;

-- If the constraint name differs, find it with:
-- SELECT name
-- FROM sys.default_constraints
-- WHERE parent_object_id = OBJECT_ID('dbo.Restaurants')
--   AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Restaurants'), 'ReceiptChargesEnabled', 'ColumnId');
