@echo off
REM Apply NTCA database scripts with sqlcmd (SSMS SQLCMD mode or cmd).
REM Example:
REM   run_all.cmd -S HAMMER -E -v NtcaAppPassword="***"
REM   run_all.cmd -S localhost,1433 -U sa -P "..." -C -v NtcaAppPassword="***"
setlocal
set DIR=%~dp0
set ORIG=%*
echo %ORIG% | find /I "NtcaAppPassword=" >nul
if errorlevel 1 (
  echo ERROR: pass -v NtcaAppPassword=...  Do not commit the password.
  exit /b 1
)
set SQLCMD=sqlcmd
if exist "%ProgramFiles%\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE" set SQLCMD=%ProgramFiles%\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE

REM Defaults first; any -v in ORIG overrides.
set EXTRA=-v NtcaDatabase=wte_NtcaDatabase -v NtcaAppLogin=ntca_app -v NtcaMigratorLogin=ntca_migrator -v NtcaMigratorPassword=__SKIP__

call :run 00_create_database.sql
if errorlevel 1 exit /b 1
call :run 01_schema.sql
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
call :run seed\SEED_20_events.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_30_blog_bloggers.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_40_about_bios.sql
if errorlevel 1 exit /b 1
call :run seed\SEED_50_gallery_media.sql
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
"%SQLCMD%" %EXTRA% %ORIG% -b -I -i "%DIR%%1"
exit /b %ERRORLEVEL%
