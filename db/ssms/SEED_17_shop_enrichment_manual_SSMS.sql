/* Plain T-SQL for SSMS — SQLCMD Mode is NOT required.
   Database: wte_NtcaDatabase (hardcoded).
   Requires db/ssms/06_shop_social_SSMS.sql first.
   Idempotent. Real data only — does not invent handles, hours, or coordinates.
   Verified 2026-10-23. See docs/DATA_REVIEW.md.
*/

USE [wte_NtcaDatabase];
GO

IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NULL
BEGIN
    RAISERROR('dir.ShopSocial is missing. Run db/06_shop_social.sql (or db/ssms/06_shop_social_SSMS.sql) first.', 16, 1);
    RETURN;
END
GO

/* GamesandCards.com — official site named in the 2026-10-23 brief. */
UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://gacsuperstore.com/'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'gamesandcards-com-eagle-rock-ca';
GO

MERGE dir.ShopSocial AS t
USING (
    SELECT s.ShopId,
           N'WEBSITE' AS PlatformCode,
           N'https://gacsuperstore.com/' AS Url,
           10 AS SortOrder
    FROM dir.Shop s
    WHERE s.Slug = N'gamesandcards-com-eagle-rock-ca'
) AS x
ON t.ShopId = x.ShopId AND t.PlatformCode = x.PlatformCode
WHEN MATCHED THEN
    UPDATE SET
        t.Url = x.Url,
        t.Handle = NULL,
        t.Verified = 1,
        t.Source = 'manual',
        t.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ShopId, PlatformCode, Url, Handle, SortOrder, Verified, Source)
    VALUES (x.ShopId, x.PlatformCode, x.Url, NULL, x.SortOrder, 1, 'manual');
GO

/* SADMIN review flags only. Do not rewrite name, phone, or address. */
UPDATE dir.Shop
SET ImportNotes = CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Google Maps may list Sports Nuts Collectibles (205-406-5080). Keep directory phone until confirmed.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Google Maps may list Sports Nuts Collectibles (205-406-5080). Keep directory phone until confirmed.'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'birmingham-sports-cards-alabaster-al';

UPDATE dir.Shop
SET ImportNotes = CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Review whether this Gilbert listing is still a live shop.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Review whether this Gilbert listing is still a live shop.'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'cards-and-stuff-gilbert-az';

UPDATE dir.Shop
SET ImportNotes = CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: No Google listing; site down; possibly closed.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: No Google listing; site down; possibly closed.'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug IN (N'chicagocards-com-pittsburg-ca', N'mojo-sportscards-thousand-oaks-ca');

UPDATE dir.Shop
SET ImportNotes = CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: PO Box listing; may be Simi Sportscards at a different address/phone. Do not merge until confirmed.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: PO Box listing; may be Simi Sportscards at a different address/phone. Do not merge until confirmed.'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'simicards-simi-valley-ca';

UPDATE dir.Shop
SET ImportNotes = CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Possibly Great American Sports Cards. Do not apply GASC socials.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Possibly Great American Sports Cards. Do not apply GASC socials.'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'bakersfield-cards-bakersfield-ca';
GO
