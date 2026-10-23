# NTCA SQL Server scripts

Ordered, idempotent where it matters. Every `.sql` file is **plain T-SQL** — open in SSMS and Execute. SQLCMD Mode is not required.

Canonical files live in `db/` and `db/seed/`. `db/ssms/*_SSMS.sql` are identical copies (same content, Eric’s existing filenames).

Full one-file installer: `db/NTCA_FULL_INSTALL.sql` (copy: `db/ssms/NTCA_FULL_INSTALL_SSMS.sql`).

Target: Microsoft SQL Server 2019+ on WTE **HAMMER**. Validated in this branch against `mcr.microsoft.com/mssql/server:2022-latest`.

DEV login hardcoded in `03_app_login.sql`: `ntca_app` / `NtcaAppDev2026x` (change for production). Optional migrator: edit `DECLARE @MigratorPassword NVARCHAR(128) = N'__SKIP__'` at the top of that file.

## Order

| # | Script | What it does |
|---|---|---|
| 0 | `00_create_database.sql` | `CREATE DATABASE` if missing. `RECOVERY FULL`, `READ_COMMITTED_SNAPSHOT ON`, instance default collation |
| 1 | `01_schema.sql` | v1 schema (36 tables, views, procs) **plus** auth/media/crm/cms/seo additions. Assembled — do not edit by hand |
| 2 | `04_onboarding_funnel.sql` | Two-stage Preview funnel columns on `crm.SignupFunnel` |
| 3 | `05_google_places.sql` | Google Places match / 30-day cache / photo / call-log tables |
| 4 | `06_shop_social.sql` | Social platform catalog + `dir.ShopSocial`; migrates Website / Facebook / Instagram |
| 5 | `02_seed.sql` | Lookup data: roles (incl. SUPERADMIN), shop tiers, features, entitlements, rotation, karma, badges, perks, `Site:Mode=Preview` |
| 6 | `seed/SEED_00_static_pages.sql` | Marketing URLs from `legacy-site/*.html` |
| 7 | `seed/SEED_10_shops.sql` | Hobby shops from CSV + `shops.html` (Free / unclaimed) |
| 8 | `seed/SEED_15_ctca_members.sql` | Directory snapshot 2026-10-22: shops / orgs / manufacturer merges |
| 9 | `seed/SEED_16_top100_shops.sql` | 100 US destination shops (`SourceSystem=TOP100-2026-10-22`) |
| 10 | `seed/SEED_17_shop_enrichment_manual.sql` | Verified 2026-10-23 first-10 enrichment (`docs/DATA_REVIEW.md`) |
| 11 | `seed/SEED_20_events.sql` | Association `cms.Event` + national calendar (`mkt.ShopEvent` / Venue / Promoter) |
| 12 | `seed/SEED_30_blog_bloggers.sql` | Bloggers directory + news + authored posts |
| 13 | `seed/SEED_40_about_bios.sql` | About-page officer/member bios |
| 14 | `seed/SEED_50_gallery_media.sql` | Gallery images as `core.MediaAsset` + `cms.GalleryItem` |
| 15 | `seed/SEED_18_legacy_content_fix.sql` | Correct concatenated About bios + boys/hall gallery rows |
| 15b | `seed/SEED_19_bio_fix.sql` | Force-set each PersonBio to their own legacy founder-body (slug or FullName) |
| 16 | `seed/SEED_60_manufacturers.sql` | Wikipedia Trading card manufacturers (factual fields only) |
| 17 | `seed/SEED_65_glossary_conditions.sql` | Glossary + condition grades from New to Trading Cards |
| 18 | `seed/SEED_70_industry_figures.sql` | Gary Vaynerchuk figure + DRAFT post (not public) |
| 19 | `03_app_login.sql` | Login `ntca_app` (least privilege). Optional `ntca_migrator` via `@MigratorPassword` |

**Existing QA database (already has shops):** run `04`, `05`, `06`, `SEED_17`, `SEED_18`, then `SEED_19` if bios still leak. All are idempotent. Then run `99_verify.sql` (read-only PASS/FAIL/WARN grid; identical copy `db/ssms/99_verify_SSMS.sql`). Active manufacturers expected **68** (66 Wikipedia + Fanatics + Onit Athlete from SEED_15).

Regenerate 01 + `seed/SEED_*.sql` (except hand-written 17/18):

```bash
python3 db/tools/generate_seed.py
python3 db/tools/generate_combined.py
python3 db/tools/generate_ssms.py
```

## HAMMER

From a workstation or the IIS box that can reach HAMMER (Windows auth as a DBA):

```bat
cd db
run_all.cmd -S HAMMER -E
```

Or open `NTCA_FULL_INSTALL.sql` in SSMS and Execute.

Database name is **`wte_NtcaDatabase`**. IIS connection strings must match.

## App connection string (placeholder — password not in git)

Set on the IIS app pool as `ConnectionStrings__DefaultConnection` (or the server secret store). Never commit the production password.

```
Server=HAMMER,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=***REPLACE***;
Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web
```

`ntca_app`: `db_datareader`, `db_datawriter`, `EXECUTE` on `core`, `dir`, `bill`, `mkt`, `feed`, `ugc`, `karma`, `crm`, `cms`, `seo`. Not `db_owner`, no DDL.

Optional `ntca_migrator` (edit `@MigratorPassword` in `03_app_login.sql`): `db_ddladmin` + data r/w/exec for schema apply only. Not used by the running site.

## Local Docker (what this branch was validated against)

```bash
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='A_Demo_Password_1' \
  -p 1433:1433 --name ntca-sql -d mcr.microsoft.com/mssql/server:2022-latest

./run_all.sh -S localhost,1433 -U sa -P 'A_Demo_Password_1' -C
```

Run `run_all` a **second** time to confirm schema + seeds are idempotent.

## Source files

- `source/ntca_schema.original.sql` — attached v1 draft (not executed as-is)
- `source/01_schema_additions.sql` — plan §8 / §12 + SEO views
- `source/01_schema_directory_calendar.sql` — manufacturers, organizations, national calendar
- `tools/inputs/posts/*.md` — authored blog posts kept so regeneration does not drop them
- `tools/inputs/wikipedia_trading_card.md` + `tools/manufacturers_source.csv`
- `tools/inputs/ctca_member_directory.csv` — 99-row directory snapshot (skip “Not Displayed”)
- `tools/inputs/top_us_destination_card_shops.csv` — 100 researched US destination shops (2026-10-22)

Public route docs: [docs/CALENDAR.md](../docs/CALENDAR.md), [docs/MANUFACTURERS.md](../docs/MANUFACTURERS.md), [docs/SEO_DISCOVERY.md](../docs/SEO_DISCOVERY.md).
