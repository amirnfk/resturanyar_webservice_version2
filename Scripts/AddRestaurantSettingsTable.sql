/* ============================================================
   Zero-downtime migration: RestaurantSettings
   Safe for production — only creates a NEW table.
   Run in SSMS / Azure Data Studio against your live DB.
   ============================================================ */

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = N'RestaurantSettings'
      AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.RestaurantSettings
    (
        RestaurantId          INT            NOT NULL,

        PrimaryColor          NVARCHAR(9)    NOT NULL
            CONSTRAINT DF_RestaurantSettings_PrimaryColor
            DEFAULT N'#f97316',

        SecondaryColor        NVARCHAR(9)    NOT NULL
            CONSTRAINT DF_RestaurantSettings_SecondaryColor
            DEFAULT N'#f97316',

        BackgroundImageUrl    NVARCHAR(500)  NULL,
        LogoUrl               NVARCHAR(500)  NULL,

        CONSTRAINT PK_RestaurantSettings
            PRIMARY KEY (RestaurantId),

        CONSTRAINT FK_RestaurantSettings_Restaurants_RestaurantId
            FOREIGN KEY (RestaurantId)
            REFERENCES dbo.Restaurants (restaurant_id)
            ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_RestaurantSettings_PrimaryColor'
)
BEGIN
    ALTER TABLE dbo.RestaurantSettings
    ADD CONSTRAINT CK_RestaurantSettings_PrimaryColor
        CHECK (PrimaryColor LIKE N'#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]');
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_RestaurantSettings_SecondaryColor'
)
BEGIN
    ALTER TABLE dbo.RestaurantSettings
    ADD CONSTRAINT CK_RestaurantSettings_SecondaryColor
        CHECK (SecondaryColor LIKE N'#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]');
END;
GO

INSERT INTO dbo.RestaurantSettings (RestaurantId, PrimaryColor, SecondaryColor)
SELECT r.restaurant_id, N'#f97316', N'#fff7ed'
FROM dbo.Restaurants AS r
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.RestaurantSettings AS rs
    WHERE rs.RestaurantId = r.restaurant_id
);
GO
