/* ============================================================
   OPTIONAL: Seed default charge templates for ONE test restaurant.
   Replace @RestaurantId before running.
   Does NOT enable ReceiptChargesEnabled — toggle that in admin.
   ============================================================ */

DECLARE @RestaurantId INT = 1; -- <-- set your test restaurant_id

IF NOT EXISTS (SELECT 1 FROM dbo.Restaurants WHERE restaurant_id = @RestaurantId)
BEGIN
    RAISERROR(N'Restaurant not found.', 16, 1);
    RETURN;
END;

MERGE dbo.RestaurantChargeDefinitions AS target
USING (VALUES
    (@RestaurantId, N'service',   N'حق سرویس',       1, 0, 10,   0, 1, 0, 10, 7),
    (@RestaurantId, N'vat',       N'مالیات بر ارزش افزوده', 2, 0, 9,    0, 0, 0, 20, 7),
    (@RestaurantId, N'packaging', N'هزینه بسته‌بندی', 1, 1, 20000, 0, 0, 0, 30, 6),
    (@RestaurantId, N'delivery',  N'هزینه ارسال',    1, 1, 30000, 0, 0, 0, 40, 4)
) AS source (RestaurantId, Code, Title, ChargeCategory, CalculationType, Value, IsEnabled, IsTaxable, PercentageBase, DisplayOrder, AppliesToOrderTypes)
ON target.RestaurantId = source.RestaurantId AND target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (RestaurantId, Code, Title, ChargeCategory, CalculationType, Value, IsEnabled, IsTaxable, PercentageBase, DisplayOrder, AppliesToOrderTypes)
    VALUES (source.RestaurantId, source.Code, source.Title, source.ChargeCategory, source.CalculationType, source.Value, source.IsEnabled, source.IsTaxable, source.PercentageBase, source.DisplayOrder, source.AppliesToOrderTypes);
GO
