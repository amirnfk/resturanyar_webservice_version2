/* ============================================================
   Order Discount Codes — additive, production-safe schema.
   Safe for live DB:
   - No ALTER of existing non-null columns without defaults
   - Nullable Orders.DiscountCodeId only (metadata-friendly)
   - New tables only; no backfill of Orders / OrderItems
   - Idempotent (IF NOT EXISTS / IF COL_LENGTH)

   Review and run manually in SSMS / Azure Data Studio.
   Do NOT run from app deploy automatically.
   ============================================================ */

SET NOCOUNT ON;
GO

-- 1) RestaurantDiscountCodes
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'RestaurantDiscountCodes' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.RestaurantDiscountCodes
    (
        Id                     INT             IDENTITY(1,1) NOT NULL,
        RestaurantId           INT             NOT NULL,
        Code                   NVARCHAR(50)    NOT NULL,
        Title                  NVARCHAR(100)   NOT NULL,
        DiscountType           NVARCHAR(20)    NOT NULL,
        DiscountValue          DECIMAL(18,2)   NOT NULL,
        MinOrderAmount         DECIMAL(18,2)   NULL,
        MaxDiscountAmount      DECIMAL(18,2)   NULL,
        StartDate              DATETIME2       NOT NULL,
        EndDate                DATETIME2       NOT NULL,
        UsageLimit             INT             NULL,
        UsedCount              INT             NOT NULL
            CONSTRAINT DF_RestaurantDiscountCodes_UsedCount DEFAULT 0,
        PerCustomerUsageLimit  INT             NULL,
        SpecificCustomerId     INT             NULL,
        IsActive               BIT             NOT NULL
            CONSTRAINT DF_RestaurantDiscountCodes_IsActive DEFAULT 1,
        CreatedAt              DATETIME2       NOT NULL
            CONSTRAINT DF_RestaurantDiscountCodes_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt              DATETIME2       NOT NULL
            CONSTRAINT DF_RestaurantDiscountCodes_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_RestaurantDiscountCodes PRIMARY KEY (Id),

        CONSTRAINT FK_RestaurantDiscountCodes_Restaurants
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants (restaurant_id) ON DELETE CASCADE,

        CONSTRAINT UQ_RestaurantDiscountCodes_RestaurantId_Code
            UNIQUE (RestaurantId, Code),

        CONSTRAINT CK_RestaurantDiscountCodes_DiscountType
            CHECK (DiscountType IN (N'Percentage', N'FixedAmount')),

        CONSTRAINT CK_RestaurantDiscountCodes_DiscountValue
            CHECK (DiscountValue >= 0),

        CONSTRAINT CK_RestaurantDiscountCodes_UsedCount
            CHECK (UsedCount >= 0),

        CONSTRAINT CK_RestaurantDiscountCodes_Dates
            CHECK (EndDate >= StartDate)
    );

    ALTER TABLE dbo.RestaurantDiscountCodes
    ADD CONSTRAINT FK_RestaurantDiscountCodes_Customers_SpecificCustomerId
        FOREIGN KEY (SpecificCustomerId) REFERENCES dbo.Customers (CustomerId)
        ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RestaurantDiscountCodes_SpecificCustomerId'
      AND object_id = OBJECT_ID(N'dbo.RestaurantDiscountCodes')
)
BEGIN
    CREATE INDEX IX_RestaurantDiscountCodes_SpecificCustomerId
        ON dbo.RestaurantDiscountCodes (SpecificCustomerId)
        WHERE SpecificCustomerId IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RestaurantDiscountCodes_RestaurantId'
      AND object_id = OBJECT_ID(N'dbo.RestaurantDiscountCodes')
)
BEGIN
    CREATE INDEX IX_RestaurantDiscountCodes_RestaurantId
        ON dbo.RestaurantDiscountCodes (RestaurantId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RestaurantDiscountCodes_RestaurantId_IsActive'
      AND object_id = OBJECT_ID(N'dbo.RestaurantDiscountCodes')
)
BEGIN
    CREATE INDEX IX_RestaurantDiscountCodes_RestaurantId_IsActive
        ON dbo.RestaurantDiscountCodes (RestaurantId, IsActive);
END;
GO

-- 2) OrderDiscountCodeUsages
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'OrderDiscountCodeUsages' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.OrderDiscountCodeUsages
    (
        Id                     INT             IDENTITY(1,1) NOT NULL,
        DiscountCodeId         INT             NOT NULL,
        OrderId                INT             NOT NULL,
        RestaurantId           INT             NOT NULL,
        CustomerId             INT             NULL,
        DiscountAmount         DECIMAL(18,2)   NOT NULL,
        ItemsSubtotalAtApply   DECIMAL(18,2)   NOT NULL,
        UsedAt                 DATETIME2       NOT NULL
            CONSTRAINT DF_OrderDiscountCodeUsages_UsedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_OrderDiscountCodeUsages PRIMARY KEY (Id),

        CONSTRAINT UQ_OrderDiscountCodeUsages_OrderId UNIQUE (OrderId),

        CONSTRAINT FK_OrderDiscountCodeUsages_DiscountCodes
            FOREIGN KEY (DiscountCodeId) REFERENCES dbo.RestaurantDiscountCodes (Id) ON DELETE NO ACTION,

        CONSTRAINT FK_OrderDiscountCodeUsages_Orders
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,

        CONSTRAINT FK_OrderDiscountCodeUsages_Restaurants
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants (restaurant_id) ON DELETE NO ACTION,

        CONSTRAINT CK_OrderDiscountCodeUsages_DiscountAmount
            CHECK (DiscountAmount >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderDiscountCodeUsages_DiscountCodeId'
      AND object_id = OBJECT_ID(N'dbo.OrderDiscountCodeUsages')
)
BEGIN
    CREATE INDEX IX_OrderDiscountCodeUsages_DiscountCodeId
        ON dbo.OrderDiscountCodeUsages (DiscountCodeId);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderDiscountCodeUsages_DiscountCodeId_CustomerId'
      AND object_id = OBJECT_ID(N'dbo.OrderDiscountCodeUsages')
)
BEGIN
    CREATE INDEX IX_OrderDiscountCodeUsages_DiscountCodeId_CustomerId
        ON dbo.OrderDiscountCodeUsages (DiscountCodeId, CustomerId);
END;
GO

-- 3) Orders.DiscountCodeId (nullable — existing rows unchanged)
IF COL_LENGTH('dbo.Orders', 'DiscountCodeId') IS NULL
BEGIN
    ALTER TABLE dbo.Orders
    ADD DiscountCodeId INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Orders_RestaurantDiscountCodes_DiscountCodeId'
)
BEGIN
    ALTER TABLE dbo.Orders
    ADD CONSTRAINT FK_Orders_RestaurantDiscountCodes_DiscountCodeId
        FOREIGN KEY (DiscountCodeId) REFERENCES dbo.RestaurantDiscountCodes (Id)
        ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Orders_DiscountCodeId'
      AND object_id = OBJECT_ID(N'dbo.Orders')
)
BEGIN
    CREATE INDEX IX_Orders_DiscountCodeId
        ON dbo.Orders (DiscountCodeId)
        WHERE DiscountCodeId IS NOT NULL;
END;
GO

PRINT N'AddOrderDiscountCodes.sql completed.';
GO
