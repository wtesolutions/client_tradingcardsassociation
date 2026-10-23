/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice on an existing database.
   Places enrich (local CLI only): rich Place Details cache, Google reviews,
   per-field source timestamps, resume tracker. Visitor pages never call Google.
   Requires db/05_google_places.sql and db/06_shop_social.sql.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* Rich Place Details columns on the existing 30-day cache. */
IF COL_LENGTH(N'dir.GooglePlaceCache', N'WebsiteUri') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD WebsiteUri NVARCHAR(500) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'NationalPhone') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD NationalPhone NVARCHAR(40) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'InternationalPhone') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD InternationalPhone NVARCHAR(40) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'BusinessStatus') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD BusinessStatus VARCHAR(40) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'EditorialSummary') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD EditorialSummary NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'TypesJson') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD TypesJson NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'PrimaryType') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD PrimaryType NVARCHAR(80) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'PriceLevel') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD PriceLevel VARCHAR(40) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'PlusCode') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD PlusCode NVARCHAR(40) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'AttributesJson') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD AttributesJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dir.GooglePlaceCache', N'CurrentHoursJson') IS NULL
    ALTER TABLE dir.GooglePlaceCache ADD CurrentHoursJson NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dir.GooglePlaceReview', N'U') IS NULL
BEGIN
    CREATE TABLE dir.GooglePlaceReview
    (
        ReviewId            BIGINT          IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_GooglePlaceReview PRIMARY KEY,
        ShopId              BIGINT          NOT NULL
            CONSTRAINT FK_GooglePlaceReview_Shop REFERENCES dir.Shop (ShopId),
        GooglePlaceId       NVARCHAR(255)   NOT NULL,
        AuthorName          NVARCHAR(200)   NULL,
        AuthorUri           NVARCHAR(400)   NULL,
        Rating              DECIMAL(2, 1)   NULL,
        ReviewText          NVARCHAR(2000)  NULL,
        RelativeTime        NVARCHAR(80)    NULL,
        PublishTime         DATETIME2(3)    NULL,
        AttributionUri      NVARCHAR(500)   NULL,
        SortOrder           TINYINT         NOT NULL
            CONSTRAINT DF_GooglePlaceReview_Order DEFAULT 1,
        FetchedUtc          DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceReview_Fetched DEFAULT SYSUTCDATETIME(),
        ExpiresUtc          DATETIME2(3)    NOT NULL,
        CreatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceReview_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_GooglePlaceReview_ShopSlot UNIQUE (ShopId, SortOrder)
    );
    CREATE INDEX IX_GooglePlaceReview_Shop ON dir.GooglePlaceReview (ShopId, ExpiresUtc);
END
GO

IF OBJECT_ID(N'dir.ShopFieldSource', N'U') IS NULL
BEGIN
    CREATE TABLE dir.ShopFieldSource
    (
        ShopId              BIGINT          NOT NULL
            CONSTRAINT FK_ShopFieldSource_Shop REFERENCES dir.Shop (ShopId),
        FieldName           VARCHAR(40)     NOT NULL,
        Source              VARCHAR(20)     NOT NULL,
        FetchedUtc          DATETIME2(3)    NOT NULL
            CONSTRAINT DF_ShopFieldSource_Fetched DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ShopFieldSource PRIMARY KEY (ShopId, FieldName),
        CONSTRAINT CK_ShopFieldSource_Source CHECK
            (Source IN ('google','website','owner','seed','sadmin','manual'))
    );
END
GO

IF OBJECT_ID(N'dir.ShopPlaceEnrich', N'U') IS NULL
BEGIN
    CREATE TABLE dir.ShopPlaceEnrich
    (
        ShopId              BIGINT          NOT NULL
            CONSTRAINT PK_ShopPlaceEnrich PRIMARY KEY
            CONSTRAINT FK_ShopPlaceEnrich_Shop REFERENCES dir.Shop (ShopId),
        LastMatchUtc        DATETIME2(3)    NULL,
        LastDetailsUtc      DATETIME2(3)    NULL,
        LastWebsiteUtc      DATETIME2(3)    NULL,
        LastError           NVARCHAR(400)   NULL,
        WebsiteEmails       NVARCHAR(500)   NULL,
        CreatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_ShopPlaceEnrich_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_ShopPlaceEnrich_Updated DEFAULT SYSUTCDATETIME()
    );
END
GO

/* Allow website / google fills on social rows without touching owner/sadmin values. */
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_ShopSocial_Source' AND parent_object_id = OBJECT_ID(N'dir.ShopSocial'))
    ALTER TABLE dir.ShopSocial DROP CONSTRAINT CK_ShopSocial_Source;
ALTER TABLE dir.ShopSocial ADD CONSTRAINT CK_ShopSocial_Source
    CHECK (Source IN ('seed','owner','sadmin','manual','google','website'));
GO
