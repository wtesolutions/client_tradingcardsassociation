/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent. Marks Hobby Tyme Sports Cards & Toys (Mebane NC) as
   editorial Featured — not paid / Sponsored. Leaves Cards And Stuff
   DATA_REVIEW notes unchanged.

   Optional owner account: set @OwnerEmail below if Eric wants a login
   for /owner (Development 888888 bypass). Leave empty to skip.
   Do not invent Jay Dudley's email or phone.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @Slug nvarchar(160) = N'hobby-tyme-sports-cards-toys-mebane-nc';
DECLARE @Note nvarchar(240) = N'Featured by Eric 2026-10-23 (editorial)';
DECLARE @OwnerEmail nvarchar(320) = N''; -- Eric: set this to create/link a non-staff owner login. Example: N'eric+hobbytyme@wte.net'
DECLARE @ShopId bigint;
DECLARE @Now datetime2(3) = SYSUTCDATETIME();

SELECT @ShopId = ShopId
FROM dir.Shop
WHERE Slug = @Slug;

IF @ShopId IS NULL
BEGIN
    RAISERROR(N'SEED_21: shop slug hobby-tyme-sports-cards-toys-mebane-nc not found. Run SEED_10 first.', 16, 1);
    RETURN;
END;

UPDATE dir.Shop
SET
    IsEditorialFeatured = 1,
    EditorialFeaturedNote = @Note,
    EditorialFeaturedAt = COALESCE(EditorialFeaturedAt, @Now),
    UpdatedAt = @Now
WHERE ShopId = @ShopId
  AND (
        IsEditorialFeatured = 0
        OR ISNULL(EditorialFeaturedNote, N'') <> @Note
      );

IF NOT EXISTS (
    SELECT 1
    FROM core.AuditLog
    WHERE EntityType = N'Shop'
      AND EntityId = @ShopId
      AND Action = N'FEATURE_EDITORIAL'
      AND ChangesJson LIKE N'%"note":"Featured by Eric 2026-10-23 (editorial)"%')
INSERT INTO core.AuditLog (EntityType, EntityId, Action, ChangesJson, CreatedAt)
VALUES (
    N'Shop',
    @ShopId,
    N'FEATURE_EDITORIAL',
    N'{"note":"Featured by Eric 2026-10-23 (editorial)","promotionType":"editorial","slug":"hobby-tyme-sports-cards-toys-mebane-nc"}',
    @Now
);

IF NULLIF(LTRIM(RTRIM(@OwnerEmail)), N'') IS NOT NULL
BEGIN
    DECLARE @Email nvarchar(320) = LOWER(LTRIM(RTRIM(@OwnerEmail)));
    DECLARE @User nvarchar(40) = LEFT(REPLACE(@Email, N'@', N'.'), 40);

    MERGE core.AppUser AS t
    USING (SELECT @Email AS Email) AS s
    ON t.Email = s.Email
    WHEN MATCHED THEN UPDATE SET
        AccountStatus = 'ACTIVE',
        EmailVerifiedAt = COALESCE(t.EmailVerifiedAt, @Now)
    WHEN NOT MATCHED THEN INSERT
        (Email, Username, DisplayName, AccountStatus, IsStaff, EmailVerifiedAt, MembershipLevel)
    VALUES
        (@Email, @User, N'Hobby Tyme owner (dev)', 'ACTIVE', 0, @Now, 'SUPPORTER');
END;

PRINT 'SEED_21_hobby_tyme_featured: editorial Featured set on Hobby Tyme. Cards And Stuff DATA_REVIEW unchanged.';
GO
