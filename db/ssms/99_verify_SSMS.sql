/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Read-only cross-check after 01–06 + seeds (including SEED_17 / SEED_18 / SEED_19).
   Open in SSMS connected to 10.100.1.87 and Execute. No writes.
   Edit @Expect at the top if a documented count changes.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Expect TABLE
(
    K VARCHAR(40) NOT NULL PRIMARY KEY,
    N INT         NOT NULL
);

INSERT INTO @Expect (K, N) VALUES
    (N'shops',                 186),
    (N'shows',                  28),
    (N'shows_national',          4),
    (N'shows_regional',         14),
    (N'shows_local',            10),
    (N'promoters',              23),
    (N'venues',                 27),
    (N'manufacturers',          68),
    (N'glossary',               42),
    (N'condition_grades',       10),
    (N'posts_published',         5),
    (N'bloggers',               12),
    (N'gallery',                22),
    (N'social_platforms',       19),
    (N'shopsocial_seed17',      19),
    (N'shops_latlng_seed17',     5),
    (N'shophours',              14),
    (N'data_review_flags',       6),
    (N'person_bios',             3);

DECLARE @Out TABLE
(
    SortOrder INT            IDENTITY(1,1) NOT NULL,
    Area      NVARCHAR(40)   NOT NULL,
    [Check]   NVARCHAR(160)  NOT NULL,
    Expected  NVARCHAR(200)  NULL,
    Actual    NVARCHAR(400)  NULL,
    Result    VARCHAR(8)     NOT NULL
);

DECLARE @exp INT, @act INT, @txt NVARCHAR(400);

/* ------------------------------------------------------------------ */
/* Schema objects from 01–06                                          */
/* ------------------------------------------------------------------ */
DECLARE @Need TABLE
(
    Area     NVARCHAR(40)  NOT NULL,
    Kind     VARCHAR(16)   NOT NULL,
    FullName NVARCHAR(128) NOT NULL
);

INSERT INTO @Need (Area, Kind, FullName) VALUES
    (N'schema', N'table', N'core.AppUser'),
    (N'schema', N'table', N'dir.Shop'),
    (N'schema', N'table', N'dir.ShopHours'),
    (N'schema', N'table', N'dir.ShopTier'),
    (N'schema', N'table', N'crm.SignupFunnel'),
    (N'schema', N'table', N'crm.SignupStepEvent'),
    (N'schema', N'table', N'cms.Event'),
    (N'schema', N'table', N'cms.Post'),
    (N'schema', N'table', N'cms.Blogger'),
    (N'schema', N'table', N'cms.PersonBio'),
    (N'schema', N'table', N'cms.GalleryItem'),
    (N'schema', N'table', N'cms.GlossaryTerm'),
    (N'schema', N'table', N'cms.ConditionGrade'),
    (N'schema', N'table', N'cms.StaticPage'),
    (N'schema', N'table', N'mkt.ShopEvent'),
    (N'schema', N'table', N'mkt.Promoter'),
    (N'schema', N'table', N'mkt.Venue'),
    (N'schema', N'table', N'dir.Manufacturer'),
    (N'schema', N'table', N'dir.Organization'),
    (N'schema', N'table', N'core.MediaAsset'),
    (N'schema', N'table', N'analytics.ShopImpression'),
    (N'04',     N'table', N'crm.OnboardingSubmission'),
    (N'05',     N'table', N'dir.GooglePlaceMatch'),
    (N'05',     N'table', N'dir.GooglePlaceCache'),
    (N'05',     N'table', N'dir.GooglePlacePhoto'),
    (N'05',     N'table', N'analytics.GoogleApiCallLog'),
    (N'06',     N'table', N'dir.SocialPlatform'),
    (N'06',     N'table', N'dir.ShopSocial'),
    (N'schema', N'view',  N'feed.vw_NewShopRotation'),
    (N'schema', N'view',  N'mkt.vw_CalendarUpcoming'),
    (N'schema', N'view',  N'dir.vw_ShopPublicListing'),
    (N'schema', N'view',  N'karma.vw_UserKarma'),
    (N'schema', N'view',  N'seo.vw_SitemapShows'),
    (N'schema', N'view',  N'seo.vw_SitemapShops'),
    (N'schema', N'view',  N'seo.vw_SitemapPromoters'),
    (N'schema', N'view',  N'seo.vw_SitemapManufacturers'),
    (N'schema', N'proc',  N'karma.usp_AwardPoints'),
    (N'schema', N'proc',  N'karma.usp_EvaluateBadges'),
    (N'schema', N'proc',  N'feed.usp_RefreshNewShopFeed'),
    (N'schema', N'proc',  N'analytics.usp_RollupShopDailyStats');

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    n.Area,
    n.Kind + N' ' + n.FullName,
    N'exists',
    CASE WHEN OBJECT_ID(n.FullName, CASE n.Kind WHEN N'table' THEN N'U' WHEN N'view' THEN N'V' WHEN N'proc' THEN N'P' END) IS NULL
         THEN N'missing' ELSE N'exists' END,
    CASE WHEN OBJECT_ID(n.FullName, CASE n.Kind WHEN N'table' THEN N'U' WHEN N'view' THEN N'V' WHEN N'proc' THEN N'P' END) IS NULL
         THEN N'FAIL' ELSE N'PASS' END
FROM @Need n;

DECLARE @Cols TABLE (Area NVARCHAR(40), Obj NVARCHAR(128), Col SYSNAME);
INSERT INTO @Cols VALUES
    (N'04', N'crm.SignupFunnel',     N'Stage'),
    (N'04', N'crm.SignupFunnel',     N'RolesJson'),
    (N'04', N'crm.SignupFunnel',     N'OnboardingStatus'),
    (N'04', N'crm.SignupFunnel',     N'RegisteredAt'),
    (N'04', N'crm.SignupFunnel',     N'ConfirmedAt'),
    (N'04', N'crm.SignupFunnel',     N'OnboardingStartedAt'),
    (N'04', N'crm.SignupFunnel',     N'OnboardingCompletedAt'),
    (N'04', N'crm.SignupStepEvent',  N'Stage'),
    (N'04', N'crm.SignupStepEvent',  N'RoleKey'),
    (N'07', N'dir.Manufacturer',     N'DescriptionHtml'),
    (N'07', N'dir.Manufacturer',     N'LogoUrl');

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    c.Area,
    N'column ' + c.Obj + N'.' + c.Col,
    N'exists',
    CASE WHEN COL_LENGTH(c.Obj, c.Col) IS NULL THEN N'missing' ELSE N'exists' END,
    CASE WHEN COL_LENGTH(c.Obj, c.Col) IS NULL THEN N'FAIL' ELSE N'PASS' END
FROM @Cols c;

DECLARE @Idx TABLE (Area NVARCHAR(40), Idx SYSNAME, Obj NVARCHAR(128));
INSERT INTO @Idx VALUES
    (N'schema', N'IX_Shop_State_City',           N'dir.Shop'),
    (N'schema', N'IX_Shop_Published',            N'dir.Shop'),
    (N'schema', N'IX_ShopEvent_Calendar',        N'mkt.ShopEvent'),
    (N'04',     N'IX_OnboardSub_Queue',          N'crm.OnboardingSubmission'),
    (N'05',     N'IX_GooglePlacePhoto_Expires',  N'dir.GooglePlacePhoto'),
    (N'06',     N'IX_ShopSocial_Shop',           N'dir.ShopSocial');

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    i.Area,
    N'index ' + i.Idx,
    N'exists',
    CASE WHEN EXISTS (
            SELECT 1 FROM sys.indexes x
            WHERE x.name = i.Idx AND x.object_id = OBJECT_ID(i.Obj))
         THEN N'exists' ELSE N'missing' END,
    CASE WHEN EXISTS (
            SELECT 1 FROM sys.indexes x
            WHERE x.name = i.Idx AND x.object_id = OBJECT_ID(i.Obj))
         THEN N'PASS' ELSE N'FAIL' END
FROM @Idx i;

/* ntca_app user + roles + EXECUTE on app schemas */
INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    N'login',
    N'database user ntca_app',
    N'exists',
    CASE WHEN EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ntca_app')
         THEN N'exists' ELSE N'missing' END,
    CASE WHEN EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ntca_app')
         THEN N'PASS' ELSE N'FAIL' END;

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    N'login',
    N'role ' + r.name,
    N'member',
    CASE WHEN rm.member_principal_id IS NULL THEN N'not member' ELSE N'member' END,
    CASE WHEN rm.member_principal_id IS NULL THEN N'FAIL' ELSE N'PASS' END
FROM (VALUES (N'db_datareader'), (N'db_datawriter')) v(RoleName)
JOIN sys.database_principals r ON r.name = v.RoleName AND r.type = N'R'
LEFT JOIN sys.database_principals u ON u.name = N'ntca_app'
LEFT JOIN sys.database_role_members rm
    ON rm.role_principal_id = r.principal_id
   AND rm.member_principal_id = u.principal_id;

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    N'login',
    N'EXECUTE on ' + s.name,
    N'GRANT',
    COALESCE(p.state_desc, N'missing'),
    CASE WHEN p.state_desc IN (N'GRANT', N'GRANT_WITH_GRANT_OPTION') THEN N'PASS' ELSE N'FAIL' END
FROM (VALUES
    (N'core'), (N'dir'), (N'bill'), (N'mkt'), (N'feed'),
    (N'ugc'), (N'karma'), (N'crm'), (N'cms'), (N'seo'), (N'analytics')
) v(SchemaName)
JOIN sys.schemas s ON s.name = v.SchemaName
LEFT JOIN sys.database_principals u ON u.name = N'ntca_app'
LEFT JOIN sys.database_permissions p
    ON p.grantee_principal_id = u.principal_id
   AND p.class_desc = N'SCHEMA'
   AND p.major_id = s.schema_id
   AND p.permission_name = N'EXECUTE';

/* ------------------------------------------------------------------ */
/* Seed counts                                                        */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.Shop;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shops';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'dir.Shop count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent WHERE EventType = N'CARD_SHOW';
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shows';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'mkt.ShopEvent CARD_SHOW', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent WHERE EventType = N'CARD_SHOW' AND Scope = N'NATIONAL';
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shows_national';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'shows NATIONAL', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent WHERE EventType = N'CARD_SHOW' AND Scope = N'REGIONAL';
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shows_regional';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'shows REGIONAL', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent WHERE EventType = N'CARD_SHOW' AND Scope = N'LOCAL';
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shows_local';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'shows LOCAL', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.Promoter', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.Promoter;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'promoters';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'mkt.Promoter count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'mkt.Venue', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.Venue;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'venues';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'mkt.Venue count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.Manufacturer', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.Manufacturer WHERE IsActive = 1;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'manufacturers';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'dir.Manufacturer active', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
SET @txt = N'none';
IF OBJECT_ID(N'dir.Manufacturer', N'U') IS NOT NULL
BEGIN
    SELECT @act = ISNULL((
        SELECT COUNT(*) FROM (
            SELECT NormalizedName
            FROM dir.Manufacturer
            WHERE IsActive = 1 AND NormalizedName IS NOT NULL
            GROUP BY NormalizedName
            HAVING COUNT(*) > 1
        ) d), 0);
    SELECT @txt = STRING_AGG(x.NormalizedName, N', ')
    FROM (
        SELECT NormalizedName
        FROM dir.Manufacturer
        WHERE IsActive = 1 AND NormalizedName IS NOT NULL
        GROUP BY NormalizedName
        HAVING COUNT(*) > 1
    ) x;
    IF @txt IS NULL SET @txt = N'none';
END;
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (
    N'quality',
    N'duplicate manufacturer NormalizedName',
    N'0',
    CONVERT(varchar(20), ISNULL(@act, 0)) + N' — ' + @txt,
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
SET @txt = N'none';
IF OBJECT_ID(N'dir.Manufacturer', N'U') IS NOT NULL
BEGIN
    SELECT @act = ISNULL((
        SELECT COUNT(*) FROM (
            SELECT LOWER(LTRIM(RTRIM(Name))) AS Nm
            FROM dir.Manufacturer
            WHERE IsActive = 1
            GROUP BY LOWER(LTRIM(RTRIM(Name)))
            HAVING COUNT(*) > 1
        ) d), 0);
    SELECT @txt = STRING_AGG(x.Nm, N', ')
    FROM (
        SELECT LOWER(LTRIM(RTRIM(Name))) AS Nm
        FROM dir.Manufacturer
        WHERE IsActive = 1
        GROUP BY LOWER(LTRIM(RTRIM(Name)))
        HAVING COUNT(*) > 1
    ) x;
    IF @txt IS NULL SET @txt = N'none';
END;
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (
    N'quality',
    N'duplicate manufacturer Name',
    N'0',
    CONVERT(varchar(20), ISNULL(@act, 0)) + N' — ' + @txt,
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.Manufacturer', N'U') IS NOT NULL
    INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
    SELECT
        N'seed',
        N'CTCA manufacturer ' + v.Slug,
        N'active',
        CASE WHEN m.ManufacturerId IS NULL THEN N'missing'
             WHEN m.IsActive = 1 THEN N'active' ELSE N'inactive' END,
        CASE WHEN m.IsActive = 1 THEN N'PASS' ELSE N'FAIL' END
    FROM (VALUES (N'fanatics'), (N'onit-athlete-trading-cards')) v(Slug)
    LEFT JOIN dir.Manufacturer m ON m.Slug = v.Slug;

IF OBJECT_ID(N'dir.Manufacturer', N'U') IS NOT NULL AND COL_LENGTH(N'dir.Manufacturer', N'LogoUrl') IS NOT NULL
    INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
    SELECT
        N'seed',
        N'SkyBox LogoUrl',
        N'/media/manufacturers/skybox.jpg',
        ISNULL(m.LogoUrl, N'missing'),
        CASE WHEN m.LogoUrl = N'/media/manufacturers/skybox.jpg' THEN N'PASS' ELSE N'FAIL' END
    FROM dir.Manufacturer m
    WHERE m.Slug = N'skybox';

IF OBJECT_ID(N'cms.GlossaryTerm', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.GlossaryTerm;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'glossary';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.GlossaryTerm count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'cms.ConditionGrade', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.ConditionGrade;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'condition_grades';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.ConditionGrade count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'cms.Post', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.Post WHERE Status = N'PUBLISHED';
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'posts_published';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.Post published', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'cms.Blogger', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.Blogger WHERE IsActive = 1;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'bloggers';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.Blogger active', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'cms.GalleryItem', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.GalleryItem;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'gallery';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.GalleryItem count (SEED_50 + 2)', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.SocialPlatform', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.SocialPlatform;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'social_platforms';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'dir.SocialPlatform catalog', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.ShopSocial WHERE Source = N'manual' AND Verified = 1;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shopsocial_seed17';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'dir.ShopSocial SEED_17 manual', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
    SELECT @act = COUNT(*)
    FROM dir.Shop
    WHERE Slug IN (
        N'boxseat-collectibles-phoenix-az',
        N'burbank-sportscards-burbank-ca',
        N'gamesandcards-com-eagle-rock-ca',
        N'bakersfield-cards-bakersfield-ca',
        N'wonder-water-sports-cards-comics-gaming-clearwater-fl')
      AND Latitude IS NOT NULL AND Longitude IS NOT NULL;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shops_latlng_seed17';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'SEED_17 shops with lat/lng', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'dir.ShopHours', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.ShopHours;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'shophours';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'dir.ShopHours rows', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

IF OBJECT_ID(N'cms.PersonBio', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.PersonBio;
ELSE
    SET @act = -1;
SELECT @exp = N FROM @Expect WHERE K = N'person_bios';
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'seed', N'cms.PersonBio count', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
    CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
SELECT
    N'seed',
    N'gallery slug ' + v.Slug,
    N'exists',
    CASE WHEN g.Slug IS NULL THEN N'missing' ELSE N'exists' END,
    CASE WHEN g.Slug IS NULL THEN N'FAIL' ELSE N'PASS' END
FROM (VALUES (N'about-boys-cards'), (N'network-convention-hall')) v(Slug)
LEFT JOIN cms.GalleryItem g ON g.Slug = v.Slug
WHERE OBJECT_ID(N'cms.GalleryItem', N'U') IS NOT NULL;

/* ------------------------------------------------------------------ */
/* SEED_18 bios no longer contain each other's paragraphs             */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'cms.PersonBio', N'U') IS NOT NULL
BEGIN
    INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
    SELECT
        N'SEED_18',
        N'bio ' + v.Slug + N' has own marker',
        v.OwnMarker,
        CASE WHEN COALESCE(b.BioText, N'') + N' ' + COALESCE(b.Quote, N'') LIKE N'%' + v.OwnMarker + N'%'
             THEN N'present' ELSE N'missing' END,
        CASE WHEN COALESCE(b.BioText, N'') + N' ' + COALESCE(b.Quote, N'') LIKE N'%' + v.OwnMarker + N'%'
             THEN N'PASS' ELSE N'FAIL' END
    FROM (VALUES
        (N'craig-lloyd',   N'Craig Lloyd founded'),
        (N'eric-garrison', N'Eric Garrison started'),
        (N'jay-dudley',    N'Jay Dudley started')
    ) v(Slug, OwnMarker)
    LEFT JOIN cms.PersonBio b ON b.Slug = v.Slug;

    INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
    SELECT
        N'SEED_18',
        N'bio ' + b.Slug + N' excludes ' + x.ForeignMarker,
        N'absent',
        CASE WHEN COALESCE(b.BioText, N'') + N' ' + COALESCE(b.Quote, N'') LIKE N'%' + x.ForeignMarker + N'%'
             THEN N'LEAK' ELSE N'absent' END,
        CASE WHEN COALESCE(b.BioText, N'') + N' ' + COALESCE(b.Quote, N'') LIKE N'%' + x.ForeignMarker + N'%'
             THEN N'FAIL' ELSE N'PASS' END
    FROM cms.PersonBio b
    CROSS APPLY (VALUES
        (N'craig-lloyd',   N'Eric Garrison started'),
        (N'craig-lloyd',   N'Jay Dudley started'),
        (N'eric-garrison', N'Craig Lloyd founded'),
        (N'eric-garrison', N'Jay Dudley started'),
        (N'jay-dudley',    N'Craig Lloyd founded'),
        (N'jay-dudley',    N'Eric Garrison started')
    ) x(Slug, ForeignMarker)
    WHERE b.Slug = x.Slug;
END;

/* ------------------------------------------------------------------ */
/* Data quality                                                       */
/* ------------------------------------------------------------------ */
SET @act = 0;
IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.ShopSocial ss
    WHERE NOT EXISTS (SELECT 1 FROM dir.Shop s WHERE s.ShopId = ss.ShopId);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'orphan ShopSocial.ShopId', N'0', CONVERT(varchar(20), @act),
    CASE WHEN @act = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'dir.ShopHours', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.ShopHours h
    WHERE NOT EXISTS (SELECT 1 FROM dir.Shop s WHERE s.ShopId = h.ShopId);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'orphan ShopHours.ShopId', N'0', CONVERT(varchar(20), @act),
    CASE WHEN @act = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'cms.GalleryItem', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.GalleryItem g
    WHERE g.MediaAssetId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM core.MediaAsset m WHERE m.MediaAssetId = g.MediaAssetId);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'orphan GalleryItem.MediaAssetId', N'0', CONVERT(varchar(20), @act),
    CASE WHEN @act = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent e
    WHERE e.VenueId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM mkt.Venue v WHERE v.VenueId = e.VenueId);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'orphan ShopEvent.VenueId', N'0', CONVERT(varchar(20), @act),
    CASE WHEN @act = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM mkt.ShopEvent e
    WHERE e.PromoterId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM mkt.Promoter p WHERE p.PromoterId = e.PromoterId);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'orphan ShopEvent.PromoterId', N'0', CONVERT(varchar(20), @act),
    CASE WHEN @act = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
    SELECT @act = ISNULL((
        SELECT COUNT(*) FROM (
            SELECT Slug FROM dir.Shop GROUP BY Slug HAVING COUNT(*) > 1
        ) d), 0);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'duplicate dir.Shop slugs', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'mkt.ShopEvent', N'U') IS NOT NULL
    SELECT @act = ISNULL((
        SELECT COUNT(*) FROM (
            SELECT Slug FROM mkt.ShopEvent WHERE Slug IS NOT NULL GROUP BY Slug HAVING COUNT(*) > 1
        ) d), 0);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'duplicate mkt.ShopEvent slugs', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'cms.Post', N'U') IS NOT NULL
    SELECT @act = ISNULL((
        SELECT COUNT(*) FROM (
            SELECT Slug FROM cms.Post GROUP BY Slug HAVING COUNT(*) > 1
        ) d), 0);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'duplicate cms.Post slugs', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.Shop
    WHERE NULLIF(LTRIM(RTRIM(City)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(StateCode)), N'') IS NULL;
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'shops missing city/state', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

/* Social URLs vs platform HostAllowlist / Kind */
SET @act = 0;
IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NOT NULL AND OBJECT_ID(N'dir.SocialPlatform', N'U') IS NOT NULL
BEGIN
    SELECT @act = COUNT(*)
    FROM dir.ShopSocial ss
    JOIN dir.SocialPlatform p ON p.Code = ss.PlatformCode
    WHERE
        (p.Kind = N'EMAIL' AND ss.Url NOT LIKE N'mailto:%')
        OR (p.Kind = N'PHONE' AND ss.Url NOT LIKE N'tel:%')
        OR (p.Kind = N'URL' AND ss.Url NOT LIKE N'http://%' AND ss.Url NOT LIKE N'https://%')
        OR (p.Kind = N'URL' AND p.HostAllowlist IS NOT NULL AND NOT EXISTS (
            SELECT 1
            FROM STRING_SPLIT(p.HostAllowlist, N',') h
            WHERE LOWER(
                REPLACE(REPLACE(
                    CASE
                        WHEN CHARINDEX(N'://', ss.Url) > 0
                            THEN SUBSTRING(ss.Url, CHARINDEX(N'://', ss.Url) + 3, 400)
                        ELSE ss.Url
                    END, N'www.', N''), N'WWW.', N'')
            ) LIKE LOWER(LTRIM(RTRIM(h.value))) + N'%'
        ));
END;
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'invalid social URLs vs platform', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM dir.Shop
    WHERE ListingStatus = N'ACTIVE'
      AND (NULLIF(LTRIM(RTRIM(Name)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(Slug)), N'') IS NULL);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'published shops missing name/slug', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'cms.Post', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.Post
    WHERE Status = N'PUBLISHED'
      AND (NULLIF(LTRIM(RTRIM(Title)), N'') IS NULL
        OR NULLIF(LTRIM(RTRIM(Slug)), N'') IS NULL
        OR (Body IS NULL AND BodyMarkdown IS NULL));
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'published posts missing title/slug/body', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
IF OBJECT_ID(N'cms.PersonBio', N'U') IS NOT NULL
    SELECT @act = COUNT(*) FROM cms.PersonBio
    WHERE IsPublished = 1
      AND (NULLIF(LTRIM(RTRIM(FullName)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(BioText)), N'') IS NULL);
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'published bios missing name/text', N'0', CONVERT(varchar(20), ISNULL(@act, 0)),
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'FAIL' END);

SET @act = 0;
SET @txt = N'0';
IF OBJECT_ID(N'cms.PersonBio', N'U') IS NOT NULL
BEGIN
    SELECT @act = COUNT(*) FROM cms.PersonBio
    WHERE IsPublished = 1
      AND NULLIF(LTRIM(RTRIM(PhotoLegacyPath)), N'') IS NULL
      AND PhotoMediaId IS NULL;
    SELECT @txt = STRING_AGG(Slug, N', ')
    FROM cms.PersonBio
    WHERE IsPublished = 1
      AND NULLIF(LTRIM(RTRIM(PhotoLegacyPath)), N'') IS NULL
      AND PhotoMediaId IS NULL;
END;
INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'quality', N'people without photos', N'0 (WARN if any)',
    CONVERT(varchar(20), ISNULL(@act, 0)) + CASE WHEN @txt IS NULL OR @txt = N'' THEN N'' ELSE N' — ' + @txt END,
    CASE WHEN ISNULL(@act, 0) = 0 THEN N'PASS' ELSE N'WARN' END);

/* ------------------------------------------------------------------ */
/* Google tables: present (already checked) + empty vs populated      */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'dir.GooglePlaceMatch', N'U') IS NOT NULL
    AND OBJECT_ID(N'dir.GooglePlaceCache', N'U') IS NOT NULL
    AND OBJECT_ID(N'dir.GooglePlacePhoto', N'U') IS NOT NULL
    AND OBJECT_ID(N'analytics.GoogleApiCallLog', N'U') IS NOT NULL
BEGIN
    DECLARE @gm INT = (SELECT COUNT(*) FROM dir.GooglePlaceMatch);
    DECLARE @gc INT = (SELECT COUNT(*) FROM dir.GooglePlaceCache);
    DECLARE @gp INT = (SELECT COUNT(*) FROM dir.GooglePlacePhoto);
    DECLARE @gl INT = (SELECT COUNT(*) FROM analytics.GoogleApiCallLog);
    SET @txt = N'match=' + CONVERT(varchar(20), @gm)
        + N' cache=' + CONVERT(varchar(20), @gc)
        + N' photo=' + CONVERT(varchar(20), @gp)
        + N' log=' + CONVERT(varchar(20), @gl);
    INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (
        N'google',
        N'Places tables empty or populated',
        N'populated after a key run; empty is OK before first batch',
        @txt,
        CASE WHEN @gm + @gc + @gp + @gl = 0 THEN N'WARN' ELSE N'PASS' END);
END;

/* ------------------------------------------------------------------ */
/* DATA_REVIEW flagged shops                                          */
/* ------------------------------------------------------------------ */
DECLARE @Flags TABLE (Slug NVARCHAR(200) NOT NULL PRIMARY KEY);
INSERT INTO @Flags (Slug) VALUES
    (N'birmingham-sports-cards-alabaster-al'),
    (N'cards-and-stuff-gilbert-az'),
    (N'chicagocards-com-pittsburg-ca'),
    (N'mojo-sportscards-thousand-oaks-ca'),
    (N'simicards-simi-valley-ca'),
    (N'bakersfield-cards-bakersfield-ca');

IF OBJECT_ID(N'dir.Shop', N'U') IS NOT NULL
BEGIN
    SELECT @act = COUNT(*)
    FROM @Flags f
    JOIN dir.Shop s ON s.Slug = f.Slug
    WHERE s.ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%';
    SELECT @exp = N FROM @Expect WHERE K = N'data_review_flags';
    INSERT INTO @Out (Area, [Check], Expected, Actual, Result) VALUES (N'review', N'DATA_REVIEW 2026-10-23 tokens', CONVERT(varchar(20), @exp), CONVERT(varchar(20), @act),
        CASE WHEN @act = @exp THEN N'PASS' ELSE N'FAIL' END);

    INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
    SELECT
        N'review',
        N'flagged ' + f.Slug,
        N'token present (needs SADMIN)',
        CASE
            WHEN s.ShopId IS NULL THEN N'shop missing'
            WHEN s.ImportNotes LIKE N'%DATA_REVIEW 2026-10-23:%' THEN LEFT(s.ImportNotes, 180)
            ELSE N'token missing'
        END,
        N'WARN'
    FROM @Flags f
    LEFT JOIN dir.Shop s ON s.Slug = f.Slug;
END;

/* ------------------------------------------------------------------ */
/* One result grid + summary row                                      */
/* ------------------------------------------------------------------ */
DECLARE @pass INT = (SELECT COUNT(*) FROM @Out WHERE Result = N'PASS');
DECLARE @fail INT = (SELECT COUNT(*) FROM @Out WHERE Result = N'FAIL');
DECLARE @warn INT = (SELECT COUNT(*) FROM @Out WHERE Result = N'WARN');

INSERT INTO @Out (Area, [Check], Expected, Actual, Result)
VALUES (
    N'SUMMARY',
    N'totals',
    N'0 failed',
    CONVERT(varchar(20), @pass) + N' passed, '
        + CONVERT(varchar(20), @fail) + N' failed, '
        + CONVERT(varchar(20), @warn) + N' warnings',
    CASE WHEN @fail = 0 THEN N'PASS' ELSE N'FAIL' END);

SELECT
    Area,
    [Check],
    Expected,
    Actual,
    Result
FROM @Out
ORDER BY SortOrder;
