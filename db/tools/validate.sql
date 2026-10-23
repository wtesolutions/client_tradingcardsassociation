/* SSMS: enable Query > SQLCMD Mode before running.
   ':' directives (:on error, :setvar, :r) and $(vars) fail if SQLCMD Mode is off.
   Plain T-SQL (no SQLCMD Mode): db/ssms/NTCA_FULL_INSTALL_SSMS.sql
*/
/* Smoke checks after run_all. Expected to run against $(NtcaDatabase). */
:on error exit
USE [$(NtcaDatabase)];
SET NOCOUNT ON;
GO

PRINT '--- counts ---';
SELECT 'dir.Shop' AS k, COUNT(*) AS n FROM dir.Shop
UNION ALL SELECT 'dir.Organization', COUNT(*) FROM dir.Organization
UNION ALL SELECT 'dir.Manufacturer', COUNT(*) FROM dir.Manufacturer
UNION ALL SELECT 'dir.Manufacturer sport', COUNT(DISTINCT ManufacturerId) FROM dir.ManufacturerCategoryMap WHERE CategoryCode = 'SPORT'
UNION ALL SELECT 'dir.Manufacturer nonsport', COUNT(DISTINCT ManufacturerId) FROM dir.ManufacturerCategoryMap WHERE CategoryCode = 'NONSPORT'
UNION ALL SELECT 'dir.ManufacturerProduct', COUNT(*) FROM dir.ManufacturerProduct
UNION ALL SELECT 'cms.Event', COUNT(*) FROM cms.Event
UNION ALL SELECT 'mkt.ShopEvent', COUNT(*) FROM mkt.ShopEvent
UNION ALL SELECT 'mkt.ShopEvent NATIONAL', COUNT(*) FROM mkt.ShopEvent WHERE Scope = 'NATIONAL'
UNION ALL SELECT 'mkt.Promoter', COUNT(*) FROM mkt.Promoter
UNION ALL SELECT 'mkt.Venue', COUNT(*) FROM mkt.Venue
UNION ALL SELECT 'mkt.vw_CalendarUpcoming', COUNT(*) FROM mkt.vw_CalendarUpcoming
UNION ALL SELECT 'seo.vw_SitemapShows', COUNT(*) FROM seo.vw_SitemapShows
UNION ALL SELECT 'seo.vw_SitemapShowStates', COUNT(*) FROM seo.vw_SitemapShowStates
UNION ALL SELECT 'seo.vw_SitemapPromoters', COUNT(*) FROM seo.vw_SitemapPromoters
UNION ALL SELECT 'seo.vw_SitemapManufacturers', COUNT(*) FROM seo.vw_SitemapManufacturers
UNION ALL SELECT 'cms.GlossaryTerm', COUNT(*) FROM cms.GlossaryTerm
UNION ALL SELECT 'cms.ConditionGrade', COUNT(*) FROM cms.ConditionGrade;
GO

PRINT '--- calendar sample ---';
SELECT TOP 5 Slug, Title, Scope, StateCode, City, TimeZoneId FROM mkt.vw_CalendarUpcoming ORDER BY StartsAt;
GO

PRINT '--- smoke procs ---';
-- AwardPoints / EvaluateBadges / rotation view exist
SELECT OBJECT_SCHEMA_NAME(object_id) AS sch, name
FROM sys.objects
WHERE name IN ('usp_AwardPoints','usp_EvaluateBadges','vw_NewShopRotation','vw_CalendarUpcoming')
ORDER BY sch, name;
GO

SELECT TOP 1 * FROM feed.vw_NewShopRotation;
GO
