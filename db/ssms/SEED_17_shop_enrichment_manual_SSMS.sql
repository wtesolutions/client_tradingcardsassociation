/* Plain T-SQL for SSMS — SQLCMD Mode is NOT required.
   Database: wte_NtcaDatabase (hardcoded).
   Requires db/ssms/06_shop_social_SSMS.sql first.
   Idempotent. Verified 2026-10-23 from official public pages + published maps
   coordinates. Does not invent handles. Does not change name, phone, or address.
   Does not store Google ratings. Leave Boxseat hours empty.
   See docs/DATA_REVIEW.md.
*/

USE [wte_NtcaDatabase];
GO

IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NULL
BEGIN
    RAISERROR('dir.ShopSocial is missing. Run db/06_shop_social.sql (or db/ssms/06_shop_social_SSMS.sql) first.', 16, 1);
    RETURN;
END
GO

/* ---------------------------------------------------------------------
   Legacy Website / Facebook / Instagram columns — fill empty only.
   --------------------------------------------------------------------- */
UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://boxseatcollectibles.com'),
    FacebookUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.FacebookUrl)), N''), N'https://www.facebook.com/BoxseatCollectibles'),
    InstagramHandle = COALESCE(NULLIF(LTRIM(RTRIM(s.InstagramHandle)), N''), N'boxseataz'),
    Description = COALESCE(NULLIF(LTRIM(RTRIM(s.Description)), N''), N'Your one-stop shop for sport and non-sport cards, memorabilia, supplies, & more.'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'boxseat-collectibles-phoenix-az';

UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://www.burbankcards.com'),
    FacebookUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.FacebookUrl)), N''), N'https://www.facebook.com/BurbankSportscards'),
    InstagramHandle = COALESCE(NULLIF(LTRIM(RTRIM(s.InstagramHandle)), N''), N'burbankcards'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'burbank-sportscards-burbank-ca';

UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://gacsuperstore.com'),
    FacebookUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.FacebookUrl)), N''), N'https://www.facebook.com/gacsuperstore'),
    InstagramHandle = COALESCE(NULLIF(LTRIM(RTRIM(s.InstagramHandle)), N''), N'gacsuperstore'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'gamesandcards-com-eagle-rock-ca';

/* Beckett marketplace is the only verified site for these two rows. */
UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://marketplace.beckett.com/Bakersfield_636'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'bakersfield-cards-bakersfield-ca';

UPDATE s
SET WebsiteUrl = COALESCE(NULLIF(LTRIM(RTRIM(s.WebsiteUrl)), N''), N'https://marketplace.beckett.com/wonderwater_173'),
    Description = COALESCE(NULLIF(LTRIM(RTRIM(s.Description)), N''), N'Strange Name...Cool collectibles!'),
    UpdatedAt = SYSUTCDATETIME()
FROM dir.Shop s
WHERE s.Slug = N'wonder-water-sports-cards-comics-gaming-clearwater-fl';
GO

/* ---------------------------------------------------------------------
   Shop social rows. Source=manual, Verified=1.
   Skip: Bakersfield GASC socials, Simi Sportscards, Birmingham Sports Nuts URL.
   --------------------------------------------------------------------- */
MERGE dir.ShopSocial AS t
USING (
    SELECT s.ShopId, v.PlatformCode, v.Url, v.Handle, v.SortOrder
    FROM (VALUES
        /* Boxseat Collectibles — official site + contact-page socials */
        (N'boxseat-collectibles-phoenix-az', N'WEBSITE',   N'https://boxseatcollectibles.com',                              NULL,                    10),
        (N'boxseat-collectibles-phoenix-az', N'FACEBOOK',  N'https://www.facebook.com/BoxseatCollectibles',                 N'BoxseatCollectibles',   20),
        (N'boxseat-collectibles-phoenix-az', N'INSTAGRAM', N'https://www.instagram.com/boxseataz/',                        N'boxseataz',             30),
        (N'boxseat-collectibles-phoenix-az', N'YOUTUBE',   N'https://www.youtube.com/channel/UCXOLhkPKiShhCG40UvMUoRA',    N'UCXOLhkPKiShhCG40UvMUoRA', 50),
        (N'boxseat-collectibles-phoenix-az', N'X',         N'https://x.com/boxseataz',                                     N'boxseataz',             70),
        (N'boxseat-collectibles-phoenix-az', N'PINTEREST', N'https://www.pinterest.com/boxseatcollectibles/',              N'boxseatcollectibles',  150),

        /* Burbank Sportscards — official site + chamber-listed socials */
        (N'burbank-sportscards-burbank-ca', N'WEBSITE',   N'https://www.burbankcards.com',                                 NULL,                    10),
        (N'burbank-sportscards-burbank-ca', N'FACEBOOK',  N'https://www.facebook.com/BurbankSportscards',                  N'BurbankSportscards',    20),
        (N'burbank-sportscards-burbank-ca', N'INSTAGRAM', N'https://www.instagram.com/burbankcards/',                      N'burbankcards',          30),
        (N'burbank-sportscards-burbank-ca', N'TIKTOK',    N'https://www.tiktok.com/@burbanksportscards',                   N'burbanksportscards',    40),
        (N'burbank-sportscards-burbank-ca', N'YOUTUBE',   N'https://www.youtube.com/c/BurbankSportscards',                 N'BurbankSportscards',    50),
        (N'burbank-sportscards-burbank-ca', N'X',         N'https://x.com/BurbankCards',                                   N'BurbankCards',          70),
        (N'burbank-sportscards-burbank-ca', N'EMAIL',     N'mailto:info@burbankcards.com',                                 N'info@burbankcards.com',180),

        /* GamesandCards.com — gacsuperstore.com + official IG/FB/email */
        (N'gamesandcards-com-eagle-rock-ca', N'WEBSITE',   N'https://gacsuperstore.com',                                   NULL,                    10),
        (N'gamesandcards-com-eagle-rock-ca', N'FACEBOOK',  N'https://www.facebook.com/gacsuperstore',                      N'gacsuperstore',         20),
        (N'gamesandcards-com-eagle-rock-ca', N'INSTAGRAM', N'https://www.instagram.com/gacsuperstore/',                    N'gacsuperstore',         30),
        (N'gamesandcards-com-eagle-rock-ca', N'EMAIL',     N'mailto:info@gamesandcards.com',                               N'info@gamesandcards.com',180),

        /* Beckett marketplace as the only-site WEBSITE (not eBay host) */
        (N'bakersfield-cards-bakersfield-ca', N'WEBSITE', N'https://marketplace.beckett.com/Bakersfield_636',              NULL,                    10),
        (N'wonder-water-sports-cards-comics-gaming-clearwater-fl', N'WEBSITE', N'https://marketplace.beckett.com/wonderwater_173', NULL,              10)
    ) AS v (Slug, PlatformCode, Url, Handle, SortOrder)
    INNER JOIN dir.Shop s ON s.Slug = v.Slug
) AS x
ON t.ShopId = x.ShopId AND t.PlatformCode = x.PlatformCode
WHEN MATCHED THEN
    UPDATE SET
        t.Url = x.Url,
        t.Handle = x.Handle,
        t.SortOrder = x.SortOrder,
        t.Verified = 1,
        t.Source = 'manual',
        t.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ShopId, PlatformCode, Url, Handle, SortOrder, Verified, Source)
    VALUES (x.ShopId, x.PlatformCode, x.Url, x.Handle, x.SortOrder, 1, 'manual');
GO

/* ---------------------------------------------------------------------
   Lat/lng from published maps listings that match the directory address.
   Source recorded in ImportNotes as manual-maps-2026-10-23 so the Google
   batch geocode can refresh these pins. Does not change the street address.
   --------------------------------------------------------------------- */
UPDATE dir.Shop
SET Latitude = 33.331530,
    Longitude = -111.978202,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'boxseat-collectibles-phoenix-az';

UPDATE dir.Shop
SET Latitude = 34.180800,
    Longitude = -118.309000,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'burbank-sportscards-burbank-ca';

UPDATE dir.Shop
SET Latitude = 34.139093,
    Longitude = -118.204263,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'gamesandcards-com-eagle-rock-ca';

UPDATE dir.Shop
SET Latitude = 35.377086,
    Longitude = -119.015543,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'bakersfield-cards-bakersfield-ca';

UPDATE dir.Shop
SET Latitude = 28.036522,
    Longitude = -82.737538,
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'wonder-water-sports-cards-comics-gaming-clearwater-fl';
GO

/* ---------------------------------------------------------------------
   Hours. Source=manual (noted on ImportNotes). Needs owner confirmation.
   Leave Boxseat empty. Skip GamesandCards / Wonder Water (listings disagree).
   DayOfWeek: 0=Sunday … 6=Saturday.
   --------------------------------------------------------------------- */
MERGE dir.ShopHours AS t
USING (
    SELECT s.ShopId, v.DayOfWeek, v.OpensAt, v.ClosesAt, v.IsClosed
    FROM (VALUES
        /* Burbank official help page: Mon–Sat 10–6, Sun 11–4 */
        (N'burbank-sportscards-burbank-ca', 0, CAST(N'11:00' AS TIME(0)), CAST(N'16:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 1, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 2, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 3, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 4, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 5, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'burbank-sportscards-burbank-ca', 6, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),

        /* Bakersfield — captured listings at 1201 20th St: M–F 10–6, Sat 10–3, Sun closed */
        (N'bakersfield-cards-bakersfield-ca', 0, CAST(NULL AS TIME(0)), CAST(NULL AS TIME(0)), CONVERT(BIT, 1)),
        (N'bakersfield-cards-bakersfield-ca', 1, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'bakersfield-cards-bakersfield-ca', 2, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'bakersfield-cards-bakersfield-ca', 3, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'bakersfield-cards-bakersfield-ca', 4, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'bakersfield-cards-bakersfield-ca', 5, CAST(N'10:00' AS TIME(0)), CAST(N'18:00' AS TIME(0)), CONVERT(BIT, 0)),
        (N'bakersfield-cards-bakersfield-ca', 6, CAST(N'10:00' AS TIME(0)), CAST(N'15:00' AS TIME(0)), CONVERT(BIT, 0))
    ) AS v (Slug, DayOfWeek, OpensAt, ClosesAt, IsClosed)
    INNER JOIN dir.Shop s ON s.Slug = v.Slug
) AS x
ON t.ShopId = x.ShopId AND t.DayOfWeek = x.DayOfWeek
WHEN MATCHED THEN
    UPDATE SET
        t.OpensAt = x.OpensAt,
        t.ClosesAt = x.ClosesAt,
        t.IsClosed = x.IsClosed
WHEN NOT MATCHED THEN
    INSERT (ShopId, DayOfWeek, OpensAt, ClosesAt, IsClosed)
    VALUES (x.ShopId, x.DayOfWeek, x.OpensAt, x.ClosesAt, x.IsClosed);
GO

/* ---------------------------------------------------------------------
   ImportNotes flags. Do not rewrite name, phone, or address.
   Tokens are appended once. NVARCHAR(500) — keep each add-on short.
   --------------------------------------------------------------------- */
UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Maps may list Sports Nuts Collectibles (205-406-5080). Keep directory phone. Do not apply Sports Nuts Beckett URL.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Maps may list Sports Nuts Collectibles (205-406-5080). Keep directory phone. Do not apply Sports Nuts Beckett URL.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'birmingham-sports-cards-alabaster-al';

UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Review whether this Gilbert listing is still a live shop.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Review whether this Gilbert listing is still a live shop.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'cards-and-stuff-gilbert-az';

UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: No Google listing; site down; possibly closed.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: No Google listing; site down; possibly closed.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug IN (N'chicagocards-com-pittsburg-ca', N'mojo-sportscards-thousand-oaks-ca');

UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: PO Box listing; may be Simi Sportscards at a different address/phone. Do not merge until confirmed.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: PO Box listing; may be Simi Sportscards at a different address/phone. Do not merge until confirmed.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'simicards-simi-valley-ca';

UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'DATA_REVIEW 2026-10-23: Possibly Great American Sports Cards. Do not apply GASC socials.'
        ELSE ImportNotes + N'; DATA_REVIEW 2026-10-23: Possibly Great American Sports Cards. Do not apply GASC socials.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug = N'bakersfield-cards-bakersfield-ca';

/* Hours source + owner-confirm (ShopHours has no Source column). */
UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%HOURS 2026-10-23%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'HOURS 2026-10-23: Source=manual; needs owner confirmation.'
        ELSE ImportNotes + N'; HOURS 2026-10-23: Source=manual; needs owner confirmation.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug IN (N'burbank-sportscards-burbank-ca', N'bakersfield-cards-bakersfield-ca');

/* Lat/lng source so the Google batch geocode can refresh these pins. */
UPDATE dir.Shop
SET ImportNotes = LEFT(CASE
        WHEN ImportNotes LIKE N'%GEO 2026-10-23%' THEN ImportNotes
        WHEN ImportNotes IS NULL OR LTRIM(RTRIM(ImportNotes)) = N'' THEN N'GEO 2026-10-23: Source=manual-maps-2026-10-23.'
        ELSE ImportNotes + N'; GEO 2026-10-23: Source=manual-maps-2026-10-23.'
    END, 500),
    UpdatedAt = SYSUTCDATETIME()
WHERE Slug IN (
    N'boxseat-collectibles-phoenix-az',
    N'burbank-sportscards-burbank-ca',
    N'gamesandcards-com-eagle-rock-ca',
    N'bakersfield-cards-bakersfield-ca',
    N'wonder-water-sports-cards-comics-gaming-clearwater-fl');
GO

PRINT 'SEED_17_shop_enrichment_manual: applied verified 2026-10-23 cells. See docs/DATA_REVIEW.md.';
GO
