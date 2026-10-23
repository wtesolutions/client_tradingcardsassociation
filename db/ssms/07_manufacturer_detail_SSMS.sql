/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice on an existing database.
   Adds staff-authored long-form copy and a logo URL on dir.Manufacturer.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dir.Manufacturer', N'DescriptionHtml') IS NULL
    ALTER TABLE dir.Manufacturer ADD DescriptionHtml NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dir.Manufacturer', N'LogoUrl') IS NULL
    ALTER TABLE dir.Manufacturer ADD LogoUrl NVARCHAR(500) NULL;
GO
