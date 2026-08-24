/* ============================================================
   Remove legacy invoice_discount charge templates (data only).
   Order discounts are handled exclusively via discount codes.

   Review and run manually in SSMS / Azure Data Studio.
   Do not run from app deploy automatically.
   ============================================================ */

SET NOCOUNT ON;

DELETE FROM dbo.RestaurantChargeDefinitions
WHERE Code = N'invoice_discount';

PRINT CONCAT(N'invoice_discount rows removed: ', @@ROWCOUNT);
GO
