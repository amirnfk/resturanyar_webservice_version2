/* ============================================================
   Zero-downtime migration: Receipt Charge System
   Safe for production — adds columns/tables only; all flags OFF.
   Run in SSMS / Azure Data Studio against your live DB.
   ============================================================ */

-- 1) Feature flag on Restaurants (default OFF)
IF COL_LENGTH('dbo.Restaurants', 'ReceiptChargesEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Restaurants
    ADD ReceiptChargesEnabled BIT NOT NULL
        CONSTRAINT DF_Restaurants_ReceiptChargesEnabled DEFAULT 0;
END;
GO

-- 2) OrderType on Orders (default DineIn = 0)
IF COL_LENGTH('dbo.Orders', 'OrderType') IS NULL
BEGIN
    ALTER TABLE dbo.Orders
    ADD OrderType TINYINT NOT NULL
        CONSTRAINT DF_Orders_OrderType DEFAULT 0;
END;
GO

-- 3) RestaurantChargeDefinitions
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'RestaurantChargeDefinitions' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.RestaurantChargeDefinitions
    (
        Id                   INT            IDENTITY(1,1) NOT NULL,
        RestaurantId         INT            NOT NULL,
        Code                 NVARCHAR(50)   NOT NULL,
        Title                NVARCHAR(100)  NOT NULL,
        ChargeCategory       TINYINT        NOT NULL,
        CalculationType      TINYINT        NOT NULL,
        Value                DECIMAL(18,4)  NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_Value DEFAULT 0,
        IsEnabled            BIT            NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_IsEnabled DEFAULT 0,
        IsTaxable            BIT            NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_IsTaxable DEFAULT 0,
        PercentageBase       TINYINT        NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_PercentageBase DEFAULT 0,
        DisplayOrder         INT            NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_DisplayOrder DEFAULT 0,
        AppliesToOrderTypes  TINYINT        NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_AppliesToOrderTypes DEFAULT 7,
        CreatedAt            DATETIME2      NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt            DATETIME2      NOT NULL
            CONSTRAINT DF_RestaurantChargeDefinitions_UpdatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_RestaurantChargeDefinitions PRIMARY KEY (Id),

        CONSTRAINT FK_RestaurantChargeDefinitions_Restaurants
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants (restaurant_id) ON DELETE CASCADE,

        CONSTRAINT UQ_RestaurantChargeDefinitions_RestaurantId_Code
            UNIQUE (RestaurantId, Code),

        CONSTRAINT CK_RestaurantChargeDefinitions_ChargeCategory
            CHECK (ChargeCategory IN (0, 1, 2)),

        CONSTRAINT CK_RestaurantChargeDefinitions_CalculationType
            CHECK (CalculationType IN (0, 1)),

        CONSTRAINT CK_RestaurantChargeDefinitions_PercentageBase
            CHECK (PercentageBase IN (0, 1))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RestaurantChargeDefinitions_RestaurantId'
      AND object_id = OBJECT_ID(N'dbo.RestaurantChargeDefinitions')
)
BEGIN
    CREATE INDEX IX_RestaurantChargeDefinitions_RestaurantId
        ON dbo.RestaurantChargeDefinitions (RestaurantId);
END;
GO

-- 4) OrderReceiptSnapshots (one per order)
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'OrderReceiptSnapshots' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.OrderReceiptSnapshots
    (
        Id                  INT            IDENTITY(1,1) NOT NULL,
        OrderId             INT            NOT NULL,
        RestaurantId        INT            NOT NULL,
        OrderType           TINYINT        NOT NULL,
        ItemsSubtotal       DECIMAL(18,2)  NOT NULL,
        GrandTotal          DECIMAL(18,2)  NOT NULL,
        ChargeLinesJson     NVARCHAR(MAX)  NOT NULL,
        ReceiptPayloadJson  NVARCHAR(MAX)  NOT NULL,
        OrderItemsVersion   DATETIME2      NOT NULL,
        IssuedAt            DATETIME2      NOT NULL
            CONSTRAINT DF_OrderReceiptSnapshots_IssuedAt DEFAULT SYSUTCDATETIME(),
        IssuedByUserId      INT            NULL,

        CONSTRAINT PK_OrderReceiptSnapshots PRIMARY KEY (Id),

        CONSTRAINT UQ_OrderReceiptSnapshots_OrderId UNIQUE (OrderId),

        CONSTRAINT FK_OrderReceiptSnapshots_Orders
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,

        CONSTRAINT FK_OrderReceiptSnapshots_Restaurants
            FOREIGN KEY (RestaurantId) REFERENCES dbo.Restaurants (restaurant_id) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_OrderReceiptSnapshots_RestaurantId'
      AND object_id = OBJECT_ID(N'dbo.OrderReceiptSnapshots')
)
BEGIN
    CREATE INDEX IX_OrderReceiptSnapshots_RestaurantId
        ON dbo.OrderReceiptSnapshots (RestaurantId);
END;
GO

-- 5) ReceiptPrintHistory
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = N'ReceiptPrintHistory' AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.ReceiptPrintHistory
    (
        Id                      INT            IDENTITY(1,1) NOT NULL,
        OrderId                 INT            NOT NULL,
        OrderReceiptSnapshotId  INT            NOT NULL,
        PrintedAt               DATETIME2      NOT NULL
            CONSTRAINT DF_ReceiptPrintHistory_PrintedAt DEFAULT SYSUTCDATETIME(),
        PrintedByUserId         INT            NULL,
        Channel                 NVARCHAR(20)   NOT NULL
            CONSTRAINT DF_ReceiptPrintHistory_Channel DEFAULT N'Web',

        CONSTRAINT PK_ReceiptPrintHistory PRIMARY KEY (Id),

        CONSTRAINT FK_ReceiptPrintHistory_Orders
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId) ON DELETE NO ACTION,

        CONSTRAINT FK_ReceiptPrintHistory_OrderReceiptSnapshots
            FOREIGN KEY (OrderReceiptSnapshotId) REFERENCES dbo.OrderReceiptSnapshots (Id) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ReceiptPrintHistory_OrderId_PrintedAt'
      AND object_id = OBJECT_ID(N'dbo.ReceiptPrintHistory')
)
BEGIN
    CREATE INDEX IX_ReceiptPrintHistory_OrderId_PrintedAt
        ON dbo.ReceiptPrintHistory (OrderId, PrintedAt DESC);
END;
GO
