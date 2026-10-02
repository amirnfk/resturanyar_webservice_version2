/* ============================================================
   Invoice print template preference per restaurant.
   Run manually on live DB.
   ============================================================ */

IF COL_LENGTH('dbo.Restaurants', 'InvoicePrintTemplateId') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD InvoicePrintTemplateId NVARCHAR(32) NOT NULL
        CONSTRAINT DF_Restaurants_InvoicePrintTemplateId DEFAULT (N'classic');
END;
GO
