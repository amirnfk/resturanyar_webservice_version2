-- Fix FoodItems DiscountPrice and CostPrice NULL values
-- Run on production AFTER deploying the FoodItemPricing code changes.
-- Phase A: inspect only. Phase B: backfill. Phase C: optional constraint.

-- ========== Phase A: Inspect (zero risk) ==========

SELECT
    COUNT(*) AS TotalFoodItems,
    SUM(CASE WHEN CostPrice IS NULL THEN 1 ELSE 0 END) AS NullCostPrice,
    SUM(CASE WHEN DiscountPrice IS NULL THEN 1 ELSE 0 END) AS NullDiscountPrice,
    SUM(CASE WHEN DiscountPrice = 0 THEN 1 ELSE 0 END) AS ZeroDiscountPrice,
    SUM(CASE WHEN DiscountPrice IS NOT NULL AND DiscountPrice >= Price THEN 1 ELSE 0 END) AS InvalidDiscountPrice
FROM FoodItems;

SELECT FoodItemId, Name, Price, DiscountPrice, CostPrice
FROM FoodItems
WHERE CostPrice IS NULL;

SELECT FoodItemId, Name, Price, DiscountPrice, CostPrice
FROM FoodItems
WHERE DiscountPrice IS NOT NULL AND (DiscountPrice <= 0 OR DiscountPrice >= Price);

SELECT oi.OrderItemId, oi.OrderId, oi.FoodItemId, oi.UnitPrice, oi.UnitPriceWithDiscount, oi.Quantity
FROM OrderItems oi
WHERE oi.UnitPriceWithDiscount = 0;

-- ========== Phase B: Backfill (run during low traffic) ==========

BEGIN TRANSACTION;

UPDATE FoodItems
SET CostPrice = 0
WHERE CostPrice IS NULL;

UPDATE FoodItems
SET DiscountPrice = NULL
WHERE DiscountPrice IS NOT NULL
  AND (DiscountPrice <= 0 OR DiscountPrice >= Price);

-- Only if Phase A found OrderItems with UnitPriceWithDiscount = 0:
UPDATE oi
SET oi.UnitPriceWithDiscount = oi.UnitPrice
FROM OrderItems oi
WHERE oi.UnitPriceWithDiscount = 0
  AND oi.UnitPrice > 0;

COMMIT TRANSACTION;

-- ========== Phase C: Optional constraint (defer if preferred) ==========

-- ALTER TABLE FoodItems
-- ADD CONSTRAINT DF_FoodItems_CostPrice DEFAULT (0) FOR CostPrice;
--
-- ALTER TABLE FoodItems
-- ALTER COLUMN CostPrice decimal(18,2) NOT NULL;
