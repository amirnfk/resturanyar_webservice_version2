-- Idempotent: add audit payload columns to ReceiptPrintHistory for print-time traceability.
IF COL_LENGTH(N'dbo.ReceiptPrintHistory', N'ItemsSubtotal') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptPrintHistory
        ADD ItemsSubtotal DECIMAL(18, 2) NULL;
END;
GO

IF COL_LENGTH(N'dbo.ReceiptPrintHistory', N'GrandTotal') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptPrintHistory
        ADD GrandTotal DECIMAL(18, 2) NULL;
END;
GO

IF COL_LENGTH(N'dbo.ReceiptPrintHistory', N'ReceiptPayloadJson') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptPrintHistory
        ADD ReceiptPayloadJson NVARCHAR(MAX) NULL;
END;
GO
