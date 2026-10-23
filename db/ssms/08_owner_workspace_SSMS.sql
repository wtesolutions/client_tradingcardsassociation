/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice on an existing database.
   Editorial Featured flag + owner field locks for Google Places skip.
   dir.ShopClaimRequest already exists in 01_schema.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dir.Shop', N'IsEditorialFeatured') IS NULL
    ALTER TABLE dir.Shop ADD IsEditorialFeatured BIT NOT NULL CONSTRAINT DF_Shop_EditorialFeatured DEFAULT 0;
IF COL_LENGTH(N'dir.Shop', N'EditorialFeaturedNote') IS NULL
    ALTER TABLE dir.Shop ADD EditorialFeaturedNote NVARCHAR(240) NULL;
IF COL_LENGTH(N'dir.Shop', N'EditorialFeaturedAt') IS NULL
    ALTER TABLE dir.Shop ADD EditorialFeaturedAt DATETIME2(3) NULL;
IF COL_LENGTH(N'dir.Shop', N'OwnerFieldLocks') IS NULL
    ALTER TABLE dir.Shop ADD OwnerFieldLocks NVARCHAR(400) NULL;
IF COL_LENGTH(N'dir.Shop', N'LogoObjectKey') IS NULL
    ALTER TABLE dir.Shop ADD LogoObjectKey NVARCHAR(400) NULL;
IF COL_LENGTH(N'dir.ShopEditRequest', N'RequestKind') IS NULL
    ALTER TABLE dir.ShopEditRequest ADD RequestKind VARCHAR(20) NOT NULL CONSTRAINT DF_ShopEdit_Kind DEFAULT 'REVIEW';
GO

IF COL_LENGTH(N'ugc.ShopPhoto', N'ObjectKey') IS NULL
    ALTER TABLE ugc.ShopPhoto ADD ObjectKey NVARCHAR(400) NULL;
IF COL_LENGTH(N'ugc.ShopPhoto', N'SortOrder') IS NULL
    ALTER TABLE ugc.ShopPhoto ADD SortOrder INT NOT NULL CONSTRAINT DF_ShopPhoto_Sort DEFAULT 0;
IF COL_LENGTH(N'ugc.ShopPhoto', N'IsCover') IS NULL
    ALTER TABLE ugc.ShopPhoto ADD IsCover BIT NOT NULL CONSTRAINT DF_ShopPhoto_Cover DEFAULT 0;
IF COL_LENGTH(N'ugc.ShopPhoto', N'AltText') IS NULL
    ALTER TABLE ugc.ShopPhoto ADD AltText NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shop_EditorialFeatured' AND object_id = OBJECT_ID(N'dir.Shop'))
    CREATE INDEX IX_Shop_EditorialFeatured ON dir.Shop (IsEditorialFeatured, ListingStatus) WHERE IsEditorialFeatured = 1;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ShopEdit_Status' AND parent_object_id = OBJECT_ID(N'dir.ShopEditRequest'))
    ALTER TABLE dir.ShopEditRequest DROP CONSTRAINT CK_ShopEdit_Status;
ALTER TABLE dir.ShopEditRequest ADD CONSTRAINT CK_ShopEdit_Status
    CHECK (Status IN ('PENDING_REVIEW','APPROVED','REJECTED','WITHDRAWN','FYI','UNDONE'));
GO
