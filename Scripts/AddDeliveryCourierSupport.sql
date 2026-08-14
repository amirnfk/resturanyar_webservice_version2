/* ============================================================
   Delivery courier (پیک) support — Phase: staff role 5
   Safe for production — additive only; no status ID changes.
   Run in SSMS / Azure Data Studio against your live DB.
   ============================================================ */

-- 1) Roles: پیک (role_id = 5)
-- Roles.role_id is NOT an IDENTITY column — insert the key directly.
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE role_id = 5)
BEGIN
    INSERT INTO dbo.Roles (role_id, role_name)
    VALUES (5, N'پیک');
END;
GO

-- 2) Users.delivery_management_permission
IF COL_LENGTH('dbo.Users', 'delivery_management_permission') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD delivery_management_permission BIT NOT NULL
        CONSTRAINT DF_Users_delivery_management_permission DEFAULT 0;
END;
GO

-- 3) OrderFulfillments assignment + failure columns
IF COL_LENGTH('dbo.OrderFulfillments', 'AssignedDriverUserId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments
    ADD AssignedDriverUserId INT NULL;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'AssignedAt') IS NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments
    ADD AssignedAt DATETIME2 NULL;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'DeliveryFailureReason') IS NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments
    ADD DeliveryFailureReason NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'DeliveryFailedAt') IS NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments
    ADD DeliveryFailedAt DATETIME2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_OrderFulfillments_AssignedDriver'
)
BEGIN
    ALTER TABLE dbo.OrderFulfillments
        ADD CONSTRAINT FK_OrderFulfillments_AssignedDriver
        FOREIGN KEY (AssignedDriverUserId) REFERENCES dbo.Users (user_id)
        ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderFulfillments_AssignedDriverUserId'
      AND object_id = OBJECT_ID(N'dbo.OrderFulfillments')
)
BEGIN
    CREATE INDEX IX_OrderFulfillments_AssignedDriverUserId
        ON dbo.OrderFulfillments (AssignedDriverUserId);
END;
GO
