/* Quick check: does CustomerAddresses exist and look healthy? */
SELECT OBJECT_ID(N'dbo.CustomerAddresses') AS CustomerAddressesObjectId;

IF OBJECT_ID(N'dbo.CustomerAddresses') IS NOT NULL
BEGIN
    SELECT TOP 5 * FROM dbo.CustomerAddresses;
    SELECT c.name, t.name AS type_name, c.max_length, c.is_nullable
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.CustomerAddresses')
    ORDER BY c.column_id;
END
ELSE
BEGIN
    PRINT N'Table dbo.CustomerAddresses is MISSING. Run Phase 1 customer/address migration first.';
END
GO
