/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice on an existing database.
   Sets SkyBox LogoUrl to the committed Wikimedia Commons JPEG.
   Does not invent a manufacturer description.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

UPDATE dir.Manufacturer
SET
    LogoUrl = N'/media/manufacturers/skybox.jpg',
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'skybox'
  AND (LogoUrl IS NULL OR LogoUrl <> N'/media/manufacturers/skybox.jpg');
GO
