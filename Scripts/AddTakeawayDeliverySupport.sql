/* ============================================================
   Zero-downtime migration: Takeaway / Delivery support
   Safe for production — adds columns/tables only; takeaway/delivery ON by default.
   Run in SSMS / Azure Data Studio against your live DB.
   ============================================================ */

-- 1) Feature flags on Restaurants (default OFF)
IF COL_LENGTH('dbo.Restaurants', 'EnableTakeaway') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD EnableTakeaway BIT NOT NULL
        CONSTRAINT DF_Restaurants_EnableTakeaway DEFAULT 1;
END;
GO

IF COL_LENGTH('dbo.Restaurants', 'EnableDelivery') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD EnableDelivery BIT NOT NULL
        CONSTRAINT DF_Restaurants_EnableDelivery DEFAULT 1;
END;
GO

-- 2) OrderFulfillments (1:1 optional side table for Takeaway/Delivery snapshots)
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'OrderFulfillments' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.OrderFulfillments
    (
        OrderId                INT            NOT NULL,
        CustomerAddressId      INT            NULL,
        CustomerNameSnapshot   NVARCHAR(200)  NULL,
        PhoneSnapshot          NVARCHAR(20)   NULL,
        AddressSnapshot        NVARCHAR(1000) NULL,
        CreatedAt              DATETIME2      NOT NULL
            CONSTRAINT DF_OrderFulfillments_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt              DATETIME2      NOT NULL
            CONSTRAINT DF_OrderFulfillments_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_OrderFulfillments PRIMARY KEY (OrderId),

        CONSTRAINT FK_OrderFulfillments_Orders
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,

        CONSTRAINT FK_OrderFulfillments_CustomerAddresses
            FOREIGN KEY (CustomerAddressId) REFERENCES dbo.CustomerAddresses (AddressId) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderFulfillments_CustomerAddressId'
      AND object_id = OBJECT_ID(N'dbo.OrderFulfillments')
)
BEGIN
    CREATE INDEX IX_OrderFulfillments_CustomerAddressId
        ON dbo.OrderFulfillments (CustomerAddressId);
END;
GO
