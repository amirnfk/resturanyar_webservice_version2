/* ============================================================
   Undo Phase 2 — Delivery driver support
   Reverses AddDeliveryDriverSupport.sql
   Safe to re-run (idempotent). Does NOT touch Phase 1
   (EnableTakeaway/EnableDelivery / OrderFulfillments text columns).
   ============================================================ */

-- 1) Drop driver FK + index on OrderFulfillments
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_OrderFulfillments_AssignedDriver'
)
BEGIN
    ALTER TABLE dbo.OrderFulfillments
        DROP CONSTRAINT FK_OrderFulfillments_AssignedDriver;
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderFulfillments_AssignedDriverUserId'
      AND object_id = OBJECT_ID(N'dbo.OrderFulfillments')
)
BEGIN
    DROP INDEX IX_OrderFulfillments_AssignedDriverUserId
        ON dbo.OrderFulfillments;
END;
GO

-- 2) Drop Phase 2 columns on OrderFulfillments
IF COL_LENGTH('dbo.OrderFulfillments', 'AssignedDriverUserId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments DROP COLUMN AssignedDriverUserId;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'AssignedAt') IS NOT NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments DROP COLUMN AssignedAt;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'LatitudeSnapshot') IS NOT NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments DROP COLUMN LatitudeSnapshot;
END;
GO

IF COL_LENGTH('dbo.OrderFulfillments', 'LongitudeSnapshot') IS NOT NULL
BEGIN
    ALTER TABLE dbo.OrderFulfillments DROP COLUMN LongitudeSnapshot;
END;
GO

-- 3) Drop Users.delivery_management_permission (+ default constraint if present)
IF COL_LENGTH('dbo.Users', 'delivery_management_permission') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Users')
      AND c.name = N'delivery_management_permission';

    IF @df IS NOT NULL
        EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT [' + @df + N']');

    ALTER TABLE dbo.Users DROP COLUMN delivery_management_permission;
END;
GO

-- 4) Remove پیک role (only if no users still reference it)
IF EXISTS (SELECT 1 FROM dbo.Roles WHERE role_id = 5)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE role_id = 5)
    BEGIN
        RAISERROR(N'Cannot delete role_id=5: Users still have role پیک. Reassign or delete those users first.', 16, 1);
    END
    ELSE
    BEGIN
        DELETE FROM dbo.Roles WHERE role_id = 5;
    END
END;
GO

-- Optional cleanup (run manually if desired):
-- DELETE FROM dbo.Users WHERE name = N'delivery1' AND role_id = 5;
-- UPDATE dbo.OrderUpdates SET TargetRoleId = 2 WHERE TargetRoleId = 5;  -- remap driver notifications to waiter
