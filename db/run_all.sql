/* SSMS: enable Query > SQLCMD Mode before running.
   ':' directives (:on error, :setvar, :r) and $(vars) fail if SQLCMD Mode is off.
   Plain T-SQL (no SQLCMD Mode): db/ssms/NTCA_FULL_INSTALL_SSMS.sql
   sqlcmd -S localhost,1433 -U sa -P "..." -C -I -i run_all.sql -v NtcaAppPassword="..."
   Default database: wte_NtcaDatabase.
*/
:setvar NtcaDatabase wte_NtcaDatabase
:setvar NtcaAppLogin ntca_app
:setvar NtcaAppPassword NtcaAppDev2026x
:on error exit

:r 00_create_database.sql
:r 01_schema.sql
:r 02_seed.sql
:r seed/SEED_00_static_pages.sql
:r seed/SEED_10_shops.sql
:r seed/SEED_15_ctca_members.sql
:r seed/SEED_16_top100_shops.sql
:r seed/SEED_20_events.sql
:r seed/SEED_30_blog_bloggers.sql
:r seed/SEED_40_about_bios.sql
:r seed/SEED_50_gallery_media.sql
:r seed/SEED_60_manufacturers.sql
:r seed/SEED_65_glossary_conditions.sql
:r seed/SEED_70_industry_figures.sql
:r 03_app_login.sql
