/* One-shot sqlcmd apply. Requires SQLCMD mode (sqlcmd.exe or SSMS Query → SQLCMD Mode).
   cd db
   sqlcmd -S localhost,1433 -U sa -P "..." -C -I -i run_all.sql `
     -v NtcaDatabase=NTCA -v NtcaAppLogin=ntca_app `
     -v NtcaAppPassword="..."
   Optional: -v NtcaMigratorPassword=...
   $(NtcaAppPassword) is required by 03_app_login.sql.
*/
:on error exit

:r 00_create_database.sql
:r 01_schema.sql
:r 02_seed.sql
:r seed/SEED_00_static_pages.sql
:r seed/SEED_10_shops.sql
:r seed/SEED_15_ctca_members.sql
:r seed/SEED_20_events.sql
:r seed/SEED_30_blog_bloggers.sql
:r seed/SEED_40_about_bios.sql
:r seed/SEED_50_gallery_media.sql
:r seed/SEED_60_manufacturers.sql
:r seed/SEED_65_glossary_conditions.sql
:r 03_app_login.sql
