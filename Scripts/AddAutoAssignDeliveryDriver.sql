/* ============================================================
   Auto-assign default delivery courier setting
   Safe for production — additive only.
   Run in SSMS / Azure Data Studio.
   ============================================================ */

-- 1) Master toggle (default ON — existing rows receive 1 automatically)
IF COL_LENGTH('dbo.Restaurants', 'AutoAssignDeliveryDriver') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD AutoAssignDeliveryDriver BIT NOT NULL
        CONSTRAINT DF_Restaurants_AutoAssignDeliveryDriver DEFAULT 1;
END;
GO

-- 2) Selected default courier (nullable until owner picks one in settings)
IF COL_LENGTH('dbo.Restaurants', 'DefaultDeliveryDriverUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD DefaultDeliveryDriverUserId INT NULL;
END;
GO

-- 3) FK to Users (NO ACTION — avoids SQL Server Msg 1785 cascade-path error)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Restaurants_DefaultDeliveryDriver'
)
BEGIN
    ALTER TABLE dbo.Restaurants
        ADD CONSTRAINT FK_Restaurants_DefaultDeliveryDriver
        FOREIGN KEY (DefaultDeliveryDriverUserId) REFERENCES dbo.Users (user_id);
END;
GO

-- 4) Index for lookups
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Restaurants_DefaultDeliveryDriverUserId'
      AND object_id = OBJECT_ID(N'dbo.Restaurants')
)
BEGIN
    CREATE INDEX IX_Restaurants_DefaultDeliveryDriverUserId
        ON dbo.Restaurants (DefaultDeliveryDriverUserId);
END;
GO
