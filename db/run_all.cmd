@echo off
REM Apply NTCA database scripts with sqlcmd. Files are plain T-SQL (no -v variables).
REM Example: run_all.cmd -S HAMMER -E
REM          run_all.cmd -S localhost,1433 -U sa -P "..." -C
REM Database and app login are hardcoded: wte_NtcaDatabase / ntca_app / NtcaAppDev2026x
setlocal
set DIR=%~dp0
set ORIG=%*
set SQLCMD=sqlcmd
if exist "%ProgramFiles%\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE" set SQLCMD=%ProgramFiles%\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE

call :run 00_create_database.sql
if errorlevel 1 exit /b 1
call :run 01_schema.sql
if errorlevel 1 exit /b 1
call :run 04_onboarding_funnel.sql
if errorlevel 1 exit /b 1
call :run 05_google_places.sql
if errorlevel 1 exit /b 1
call :run 06_shop_social.sql
if errorlevel 1 exit /b 1
call :run 07_manufacturer_detail.sql
if errorlevel 1 exit /b 1
call :run 08_owner_workspace.sql
if errorlevel 1 exit /b 1
call :run 09_places_enrich.sql
if errorlevel 1 exit /b 1
call :run 02_seed.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_00_static_pages.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_10_shops.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_15_ctca_members.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_16_top100_shops.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_17_shop_enrichment_manual.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_20_events.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_30_blog_bloggers.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_40_about_bios.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_50_gallery_media.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_18_legacy_content_fix.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_19_bio_fix.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_60_manufacturers.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_65_glossary_conditions.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_70_industry_figures.sql
if errorlevel 1 exit /b 1
call :run 03_app_login.sql
if errorlevel 1 exit /b 1
echo ==== done ====
exit /b 0

:run
echo ==== %1 ====
"%SQLCMD%" %ORIG% -b -I -i "%DIR%%1"
exit /b %ERRORLEVEL%
