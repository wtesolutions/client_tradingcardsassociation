# NTCA SQL Server scripts

Ordered, idempotent where it matters, runnable with **sqlcmd** (command line or SSMS SQLCMD mode).

Target: Microsoft SQL Server 2019+ on WTE **HAMMER**. Validated in this branch against `mcr.microsoft.com/mssql/server:2022-latest`.

## Order

| # | Script | What it does |
|---|---|---|
| 0 | `00_create_database.sql` | `CREATE DATABASE` if missing. `RECOVERY FULL`, `READ_COMMITTED_SNAPSHOT ON`, instance default collation |
| 1 | `01_schema.sql` | v1 schema (36 tables, views, procs) **plus** auth/media/crm/cms/seo additions. Assembled — do not edit by hand |
| 2 | `02_seed.sql` | Lookup data: roles (incl. SUPERADMIN), shop tiers, features, entitlements, rotation, karma, badges, perks, `Site:Mode=Preview` |
| 3 | `seed/SEED_00_static_pages.sql` | Marketing URLs from `legacy-site/*.html` |
| 4 | `seed/SEED_10_shops.sql` | Hobby shops from CSV + `shops.html` (Free / unclaimed) |
| 5 | `seed/SEED_15_ctca_members.sql` | Directory snapshot 2026-10-22: shops / orgs / manufacturer merges (`SourceSystem` marker, no third-party branding) |
| 6 | `seed/SEED_16_top100_shops.sql` | 100 US destination shops (`SourceSystem=TOP100-2026-10-22`); match SEED_10+15, fill empty only |
| 7 | `seed/SEED_20_events.sql` | Association `cms.Event` + national calendar (`mkt.ShopEvent` / Venue / Promoter) from `shows.html` |
| 8 | `seed/SEED_30_blog_bloggers.sql` | Bloggers directory + news + authored posts in `tools/inputs/posts/` |
| 9 | `seed/SEED_40_about_bios.sql` | About-page officer/member bios |
| 10 | `seed/SEED_50_gallery_media.sql` | Gallery images as `core.MediaAsset` + `cms.GalleryItem` |
| 11 | `seed/SEED_60_manufacturers.sql` | Wikipedia Trading card manufacturers (factual fields only) |
| 12 | `seed/SEED_65_glossary_conditions.sql` | Glossary + condition grades from New to Trading Cards |
| 13 | `seed/SEED_70_industry_figures.sql` | Gary Vaynerchuk figure + DRAFT post (not public; no calendar rows) |
| 14 | `03_app_login.sql` | Login `ntca_app` (least privilege). Optional `ntca_migrator` |

Regenerate 01 + `seed/SEED_*.sql`:

```bash
python3 db/tools/generate_seed.py
```

## HAMMER

From a workstation or the IIS box that can reach HAMMER (Windows auth as a DBA):

```bat
cd db
run_all.cmd -S HAMMER -E -v NtcaAppPassword="***"
```

Or each file in SSMS with **Query → SQLCMD Mode**, then:

```
:setvar NtcaDatabase wte_NtcaDatabase
:setvar NtcaAppPassword ***
:r 00_create_database.sql
```

Database name defaults to **`wte_NtcaDatabase`**. Override with `-v NtcaDatabase=OtherName` if needed. IIS connection strings must match.

## App connection string (placeholder — password not in git)

Set on the IIS app pool as `ConnectionStrings__DefaultConnection` (or the server secret store). Never commit the password.

```
Server=HAMMER,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=***REPLACE***;
Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web
```

`ntca_app`: `db_datareader`, `db_datawriter`, `EXECUTE` on `core`, `dir`, `bill`, `mkt`, `feed`, `ugc`, `karma`, `crm`, `cms`, `seo`. Not `db_owner`, no DDL.

Optional `ntca_migrator` (pass `-v NtcaMigratorPassword=...`): `db_ddladmin` + data r/w/exec for DbUp / schema apply only. Not used by the running site.

## Local Docker (what this branch was validated against)

```bash
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='A_Demo_Password_1' \
  -p 1433:1433 --name ntca-sql -d mcr.microsoft.com/mssql/server:2022-latest

./run_all.sh -S localhost,1433 -U sa -P 'A_Demo_Password_1' -C \
  -v NtcaAppPassword='A_Demo_App_Password_1' \
  -v NtcaMigratorPassword='A_Demo_Mig_Password_1'
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
