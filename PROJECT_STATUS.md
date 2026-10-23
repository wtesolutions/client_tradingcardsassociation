# NTCA platform status (`app/ntca-platform`)

Draft branch only. **Do not merge or push to `main`** (GitHub Pages on `main` stays live). Pull request: https://github.com/wtesolutions/client_tradingcardsassociation/pull/18

This file describes what is **actually on the branch**. Update it at the end of each later run.

---

## 1) What was created

### Database

| Script | Role |
|---|---|
| `db/00_create_database.sql` | Creates `wte_NtcaDatabase` if missing |
| `db/01_schema.sql` | Assembled, **re-runnable** schema (`IF NOT EXISTS` / `CREATE OR ALTER`) |
| `db/02_seed.sql` | Lookup MERGE (roles, shop tiers, features, site settings, QA SUPERADMIN) |
| `db/03_app_login.sql` | Least-privilege `ntca_app` / `NtcaAppDev2026x`; optional `@MigratorPassword = N'__SKIP__'` |
| `db/run_all.sh` / `db/run_all.cmd` | Apply 00 → 01 → 04 → 05 → 06 → 02 → `SEED_*` → 03 via sqlcmd on plain T-SQL files |
| `db/NTCA_FULL_INSTALL.sql` | Same chain concatenated (plain T-SQL; SQLCMD Mode not required) |
| `db/ssms/*_SSMS.sql` | Identical copies of the canonical `db/` scripts (Eric’s existing filenames) |
| `db/source/ntca_schema.original.sql` | v1 tables |
| `db/source/01_schema_additions.sql` | crm/cms/seo, owner hours/stats, industry figures, analytics impressions + **AI concierge** (`AgentUaRule` / `AgentVisit` / `AgentInquiry`) |
| `db/source/01_schema_directory_calendar.sql` | Manufacturers, orgs, promoters, venues, exhibitors |
| `db/tools/generate_seed.py` | `--schema-only` rebuilds `01_schema.sql` without rewriting seed docs |
| `db/tools/generate_combined.py` | Writes `NTCA_FULL_INSTALL.sql` (plain T-SQL concat) |
| `db/tools/generate_ssms.py` | Copies canonical `db/` scripts to identical `db/ssms/*_SSMS.sql` files |
| `db/05_google_places.sql` | Idempotent Google Places match / 30-day cache / photo / call-log tables |
| `db/06_shop_social.sql` | Social platform catalog + `dir.ShopSocial`; migrates Website/Facebook/Instagram |
| `db/07_manufacturer_detail.sql` | `dir.Manufacturer.DescriptionHtml` + `LogoUrl` |
| `db/seed/SEED_61_skybox_logo.sql` | SkyBox `LogoUrl` = `/media/manufacturers/skybox.jpg` |
| `db/seed/SEED_17_shop_enrichment_manual.sql` | Verified 2026-10-23 first-10 enrichment (socials, Beckett-only sites, coords, hours) + SADMIN review notes |
| `db/seed/SEED_18_legacy_content_fix.sql` | Corrected About bios + boys/hall gallery rows |
| `db/99_verify.sql` | Read-only SSMS cross-check (one PASS/FAIL/WARN grid). Copy: `db/ssms/99_verify_SSMS.sql` |
| `db/seed/SEED_19_bio_fix.sql` | Force-set Craig / Eric / Jay bios to their own legacy founder-body (fixes SEED_40 glue) |
| `docs/DATA_REVIEW.md` | First-10-shop flags (Birmingham / Cards And Stuff / ChicagoCards / Mojo / Simicards / Bakersfield) |

**Seed files and live counts** (Docker SQL after a full `run_all` into `wte_NtcaDatabase`; second apply is idempotent):

| File | What it loads | Live / documented count |
|---|---|---|
| `SEED_00_static_pages.sql` | `cms.StaticPage` | 24 MERGE statements |
| `SEED_10_shops.sql` | Beckett / directory shops | **58** shops |
| `SEED_15_ctca_members.sql` | CTCA import | **34** new shops, 1 shop match, **60 orgs**, 2 new + 1 matched manufacturers; 8 listings matched as orgs. See `docs/CTCA_MATCHED_PAIRS.md` |
| `SEED_16_top100_shops.sql` | Destination shops (`SourceSystem=TOP100-2026-10-22`) | **94 new + 6 matched**; destination tag on **100** shops; 97 rows keep `TOP100-2026-10-22` (3 matches already had CTCA). See `docs/TOP100_SHOPS.md` |
| **All shops** | | **186** active (`58 + 34 + 94`) |
| `SEED_20_events.sql` | National card-show calendar | **28** `mkt.ShopEvent` CARD_SHOW rows, **23** promoters, **27** venues (plus 7 `cms.Event` association rows) |
| `SEED_30_blog_bloggers.sql` | News + bloggers | 5 published posts (includes Pokémon: `pokemon-trading-at-gaming-shows`); 12 bloggers |
| `SEED_40_about_bios.sql` | Person bios | 3 |
| `SEED_50_gallery_media.sql` | Gallery | 20 |
| `SEED_60_manufacturers.sql` | Manufacturer directory | **68** active `dir.Manufacturer` |
| `SEED_65_glossary_conditions.sql` | New-to-cards copy | **42** glossary terms, **10** condition grades |
| `SEED_70_industry_figures.sql` | Gary Vaynerchuk | 1 unpublished figure (`gary-vaynerchuk`); 1 **DRAFT** post. No invented calendar rows. See `docs/GARYVEE.md` |

Impression / review / hours / agent-visit seeds are empty by design (live traffic and real owner data only). `02_seed` MERGEs the AI UA catalog insert-only so SADMIN role/enable edits survive a re-seed.

### Website

Solution `src/Ntca.sln` (**net9.0**):

- `Ntca.Core` — entities
- `Ntca.Data` — EF Core maps existing SQL tables only (no diverging migrations)
- `Ntca.Web` — MVC/Razor public site + `/owner` + `/SADMIN` + `/api/v1`
- `Ntca.Web.Tests` — JSON-LD parse + schema.org rule tests (`dotnet test src/Ntca.sln`)
- CI: `.github/workflows/ntca-app.yml` (build + test)

**Auth:** signed cookies `ntca.owner` and `ntca.sadmin` (passwordless emailed 6-digit codes). QA user `egarrison@wte.net` is SUPERADMIN and owns Burbank Sportscards.

**Email:** SocketLabs **SMTP** (MailKit, STARTTLS 587) is the normal path whenever `Email:SmtpHost` is set. From `reports@wte.net` / National Trading Card Association. Injection API (`SocketLabs.EmailDelivery` 2.0.1) is the alternate if ServerId/ApiKey are set. No host and no keys → console + `App_Data/dev-mail/` and the login UI says the email was **not** sent. Development-only staff/owner bypass: `888888` when `Auth:DevBypassCode` is true (logged to `core.AuditLog`) — **not** Staging, **not** Production.

> **Dev/test SMTP password committed by Eric's approval 2026-10-23; rotate in SocketLabs and move to IIS env vars/user-secrets before production.** Staging servers: `ASPNETCORE_ENVIRONMENT=Staging`. Smoke: `dotnet run --project src/Ntca.Web -- email test --to you@example.com`.

**Uploads:** `wwwroot/uploads` via `IFileStorage` (no S3 on this branch).

**Working public routes** (from controllers, not docs wish-lists):

| Route | What it does |
|---|---|
| `/` | Home: destination shops, new-in-directory, upcoming shows, posts, live counts |
| `/shops` | Directory search/filter; shop name links to detail; no Free Listing badge; **no map** |
| `/shops/{state}` | State hub (full state name in H1, title, breadcrumb, BreadcrumbList JSON-LD) |
| `/shops/{state}/{city}` | City hub |
| `/shops/{state}/{city}/{slug}` | Shop detail, AI summary, JSON-LD `@graph`, click links via `/t` |
| `/shops/{state}/{city}/{slug}.md` or `?format=md` | Plain-text shop page |
| `/shops.md` | Markdown index of every active shop |
| `/t/{website\|phone\|directions\|social}?shop={id}` | Click redirect (known URL only; social `net=` is a platform code) |
| `/api/v1/shops` | JSON shops; `?source=app` records API impressions |
| `/calendar`, `/shows` | Month (default) / list / map |
| `/shows/submit` | Promoter show submission → staff queue |
| `/shows/{state}` | Show state hub |
| `/shows/{state}/{city}/{slug}` | Show detail + Add to calendar |
| `/calendar.ics`, `/shows/{state}.ics`, `/shows/{state}/{city}/{slug}.ics` | ICS feeds |
| `/api/v1/calendar` | JSON shows |
| `/manufacturers`, `/manufacturers/{slug}` | Manufacturer directory |
| `/news`, `/news/{slug}` | Blog (Pokémon post is public) |
| `/new-to-trading-cards`, `/new-to-cards`, `/guide` | Glossary + condition scale (`/new-to-cards` → 301) |
| `/about`, `/people`, `/about/{slug}` | Restyled bios + Person JSON-LD (`cms.PersonBio`) |
| `/gallery` | Legacy photos + lightbox + ImageObject JSON-LD (`cms.GalleryItem`) |
| `/network`, `/history`, `/events`, `/contact`, `/bloggers`, `/grading`, `/products` | Ported legacy pages |
| `*.html` | 301 map in `LegacyUrls` — `docs/LEGACY_CROSSCHECK.md` |
| `/signup`, `/join` | Simple Preview form (name, email, optional ZIP / phone / social) |
| `/signup/confirm/{token}` | Double opt-in; CTA to `/onboarding` |
| `/onboarding`, `/account` | Skippable multi-select “get to know you”; resume from account |
| `/robots.txt`, `/llms.txt`, `/sitemap.xml` | Discovery (llms.txt invites AI assistants) |
| `/agent` | AI concierge page (`?role=&city=&state=&zip=&interests=`) |
| `/.well-known/ai-concierge` | Assistant manifest JSON |
| `POST /api/v1/agent/ask` | Rate-limited (30/min) deterministic answers from real rows |

**`/owner`:** `/owner/login`, edit-own-shop (queued `dir.ShopEditRequest`), live social add/edit/reorder/remove, 7/30/90 impression/view/click cards + 30-day bar chart.

**`/SADMIN`:** login; dashboard KPIs; Preview/Live mode; event / owner-edit / draft / onboarding-submission queues; shop table with Imp 7d; `/SADMIN/shops/{id}/social`; `/SADMIN/social` platform catalog; `/SADMIN/agents`; `/SADMIN/funnel` (registration vs onboarding, per-role).

### Calendar

Implemented in `CalendarController` + `CalendarIcs`:

- Views: `month` (Monday-first grid), `list`, `map` (Leaflet + OSM; pins only when venue lat/lng exist — **seeded shows have none**, so the map is empty until staff add coords)
- Filters: state, q, scope
- Add to calendar on show detail: `.ics` / Google / Outlook
- ICS: national, per-state, single-show (RFC 5545; all-day `VALUE=DATE`)
- `/api/v1/calendar`

`docs/CALENDAR.md` also describes `/shows/series/{seriesKey}` and `/promoters/{slug}` — those routes are **not** wired in the C# app yet.

### SEO / AI discovery

- `robots.txt` — `Disallow: /` in Preview; Allow + sitemap when Live
- `llms.txt` — site summary + primary pages + every shop `.md` URL
- `sitemap.xml` — static paths, shop HTML + `.md`, shows, manufacturers, posts
- Shop detail: one schema.org JSON-LD `@graph` (Store/HobbyShop/SportingGoodsStore, breadcrumbs, WebPage speakable, `dateModified`). Empty fields omitted; `aggregateRating` only if approved reviews exist (none seeded)
- Unit tests + `ntca-app` workflow
- Preview mode sets `X-Robots-Tag: noindex, nofollow`

### Docs

| Path | Topic |
|---|---|
| `QA_README.md` | Laptop QA, URLs, impression + JSON-LD verify |
| `docs/CALENDAR.md` | Calendar design / ICS |
| `docs/MANUFACTURERS.md` | Manufacturer import |
| `docs/SADMIN_DESIGN.md` | Staff area |
| `docs/TOP100_SHOPS.md` | Destination shop match notes |
| `docs/SEO_DISCOVERY.md` | robots / llms / sitemap |
| `docs/CTCA_MATCHED_PAIRS.md` | CTCA match table |
| `docs/GARYVEE.md` | Dossier-only industry figure |
| `docs/HOSTING.md` | Hosting notes |
| `docs/iis/web.config.example` | IIS ANCM sample |
| `docs/ntca_schema_outline.md` | Schema outline |

### `legacy-site/` hold

Static HTML/CSV snapshot used as seed input (`hobby-shop-directory.csv`, `shows.html`, news, about, gallery). **Not** the runnable app. `main` still serves the GitHub Pages teaser from this tree.

### Major milestone commits (this branch)

| Hash | What landed |
|---|---|
| `774e25c` | Foothold: `legacy-site/` hold, SQL scripts, Pokémon post seed |
| `fabdfa4` | National show calendar, CTCA import, manufacturers |
| `319ea66` | Calendar seed apply fixes |
| `64350ab` | Looser CTCA match, glossary, combined SQL |
| `ce4d31a` | Runnable .NET 9 site, SocketLabs, GaryVee draft |
| `fdf295a` | SEED_16 top-100 destination shops |
| `d3518f0` | Re-runnable `01_schema` / `run_all` |
| `4b7da4a` | ICS feeds + Add to calendar |
| `f67756f` | Calendar month + map views |
| `e406efe` | QA_README + desktop screenshot paths |
| `d5e57c3` | Shop impressions (all tiers) + shop JSON-LD / AI summary / `.md` |
| `9bdacc0` | Raw `application/ld+json` script type + city breadcrumb hubs |
| `881fc67` | AI concierge / VIP visitors + ask API + SADMIN AI Visitors |
| `eba4172` | Mobile-first shop directory cards + Near me / ZIP distance |
| `4133ffa` / `4a572a5` | Directory cards matched to Eric’s list layout (static-map thumbs, Call/View) |
| `76e81b1` | Multi-step Preview signup funnel + SADMIN funnel report |
| `49464f2` / `e7e944f` / `0d639c9` | Funnel polish: ZIP early, icon cards, travel radius, live result cards, shared ask intent |
| `5859d2d` | Development default SQL `10.100.1.87` / `wte_NtcaDatabase`; friendly DB-down page |

---

## 2) What still needs to be created

Update the **Status** column when a later run lands the item.

### In this session / queued

| Item | Status | Notes |
|---|---|---|
| Impression tracking for all shops + owner/SADMIN stats | **Done** (`d5e57c3`) | `analytics.ShopImpression` / `ShopDailyStats` + rollup; `/t` clicks; surfaces include directory, state, home, map, API, detail |
| AI-optimized shop JSON-LD + AI summary + `.md` | **Done** (`d5e57c3`, `9bdacc0`) | Real fields only; `llms.txt` lists shop markdown |
| AI concierge / VIP AI visitors + `POST /api/v1/agent/ask` + SADMIN AI Visitors | **Done** (`881fc67`) | UA catalog (SADMIN-editable), welcome aside (no cloaking), `Link` + `X-NTCA-Agent-*`, `/.well-known/ai-concierge`, `/agent`, rate-limited ask API, anonymized `AgentVisit`/`AgentInquiry`, `/shows/submit` for promoters |
| Mobile directory cards (SoberNation-style) | **Done** (`4133ffa`, `4a572a5`) | Cards on `/shops` + state/city; OSM static-map thumb; sticky ZIP/Near me; `/t/view` + `/t/phone`; compact shop-detail header |
| Care.com-style multi-step signup funnel + funnel report | **Done** (`76e81b1`, `49464f2`, `e7e944f`, `0d639c9`) | original one-flow funnel (superseded below) |
| Simple Preview signup + post-signup multi-select onboarding | **In this change** | `/signup` = name/email/ZIP; `/onboarding` after confirm; typeahead + pending add/claim; `/SADMIN/funnel` two-stage; SQL `db/ssms/04_onboarding_funnel_SSMS.sql` |
| Directory QA: no Free Listing badge, name links, full state name, no `/shops` map | **Done** | cards + state hubs + `/shops` |
| Public restyle (navy/gold homepage, directory, shop detail) | **Done** (`ffd1616`) | specs in `docs/design/` |
| Google Places batch sync | **Done** | Text Search match + nightly Details/photos; no Google calls on visitor paths; `/SADMIN/google`; SQL `05` |
| Robust shop social links | **Done** (`9ad319b`) | `dir.SocialPlatform` + `dir.ShopSocial`; FA6 icons + Whatnot badge; `/t` click tracking; owner + SADMIN editors; SQL `06` |
| SEED_17 manual shop enrichment | **Done** | Official-page socials for Boxseat / Burbank / GAC; Beckett WEBSITE for Bakersfield + Wonder Water; lat/lng `manual-maps-2026-10-23`; Burbank + Bakersfield hours (owner-confirm). Review flags in `docs/DATA_REVIEW.md`. |
| Every `.sql` is plain T-SQL | **Done** | No `:setvar` / `:on error` / `:r` / sqlcmd variables. SSMS works with SQLCMD Mode off. `db/ssms/*_SSMS.sql` are identical copies. `SqlCmdFreeTests` fails CI if a line starts with `:` or contains a dollar-paren token. |
| `99_verify.sql` read-only cross-check | **Done** | One PASS/FAIL/WARN grid for schema, seed counts, SEED_18 bios, data quality, Google tables, DATA_REVIEW flags. |
| Bio leak + manufacturer 68 | **In this change** | SEED_40 parser stopped gluing the next officer. `SEED_19` force-updates bios. Active manufacturers expected 68 (Wikipedia 66 + Fanatics + Onit Athlete). |

### Remaining platform

- [ ] Venue (and remaining shop) **lat/lng** — shop coords now fill from approved Google matches; calendar venues still need geocoding
- [ ] **S3 + CloudFront** (today: `wwwroot/uploads`)
- [ ] Cloudflare **Turnstile** production keys (off in Development)
- [ ] **Stripe** shop tiers / payments (`bill.*` tables exist; no checkout UI)
- [ ] **IIS on Windows Server 2025** deploy + GitHub Actions publish (workflow today only builds/tests)
- [ ] Production **SocketLabs** keys and **wte.net** SPF/DKIM (from-address already `reports@wte.net`)
- [ ] SADMIN **role assignments** beyond the QA SUPERADMIN
- [ ] Written **owner-edit approval policy** (queue works; policy text is open)
- [ ] **MAUI** apps against `/api/v1` (shops + calendar JSON exist; no mobile client on this branch)
- [ ] **DNS cutover** from GitHub Pages (`main`) to the IIS/.NET site
- [ ] **Credential rotation** for anything still shared with the NCVetBiz era — rotate in the secret store / user-secrets / SQL logins. **No secrets are listed in this file.**

### Open decisions for Eric

1. When to flip `/SADMIN` site mode from Preview (`noindex`) to Live.
2. Paid tier prices (`dir.ShopTier` PAID/PREMIUM monthly/annual are NULL).
3. Whether destination-shop “Free / unclaimed” listings stay free after claim.
4. Owner-edit auto-approve rules vs staff-only.
5. Whether calendar map waits on geocoding the 27 venues or ships empty.
6. Production hostname vs keeping `tradingcardsassociation.com` on Pages until IIS is ready.
7. How much PII an AI concierge may infer vs the hard ban on name/email/phone.
8. Stripe vs invoice-later for founding shop partners.

---

## 3) Start the site locally on a Mac for QA

Placeholder passwords only. Do not commit secrets. Avoid `#` in sqlcmd `-v` passwords.

### 3.1 Install .NET 9 SDK

```bash
brew install --cask dotnet-sdk
dotnet --list-sdks
# Expect a 9.0.x line. .NET 10 is not required.
```

### 3.2 SQL Server

**A) Docker Desktop for Mac** (typical laptop):

1. Install Docker Desktop.
2. Apple Silicon: Settings → General → enable **Use Rosetta for x86/amd64 emulation**.
3. Start SQL Server 2022:

```bash
docker run --platform linux/amd64 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='NtcaSqlDev2026x' \
  -p 1433:1433 --name ntca-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

**B) HAMMER over VPN** — skip Docker; use `-S HAMMER -E` (Windows auth) or the SQL login your DBA gives you. Do not put production passwords in the repo.

### 3.3 sqlcmd (or a GUI)

```bash
brew tap microsoft/mssql-release
brew install mssql-tools18
# Add to PATH, e.g. in ~/.zprofile:
# export PATH="/opt/homebrew/opt/mssql-tools18/bin:$PATH"
sqlcmd -?
```

Alternatives to SSMS: **Azure Data Studio** or the **SQL Server (mssql)** VS Code extension. Every `.sql` file is plain T-SQL — SQLCMD Mode is not required.

### 3.4 Clone / fetch this branch

```bash
git clone https://github.com/wtesolutions/client_tradingcardsassociation.git
cd client_tradingcardsassociation
git fetch origin app/ntca-platform
git checkout app/ntca-platform
```

Already cloned: `git pull origin app/ntca-platform`.

### 3.5 Apply the database

```bash
cd db
./run_all.sh -S localhost,1433 -U sa -P 'NtcaSqlDev2026x' -C
```

HAMMER example:

```bash
./run_all.sh -S HAMMER -E
```

A second `run_all` is safe. App login is hardcoded `ntca_app` / `NtcaAppDev2026x` (DEV ONLY). Optional migrator: edit `DECLARE @MigratorPassword` at the top of `03_app_login.sql` (default `N'__SKIP__'`).

SSMS / Azure Data Studio: open `db/NTCA_FULL_INSTALL.sql` (or the identical `db/ssms/NTCA_FULL_INSTALL_SSMS.sql`) and Execute. SQLCMD Mode is not required.

**Existing QA database:** run `04_onboarding_funnel.sql`, `05_google_places.sql`, `06_shop_social.sql`, `SEED_17_shop_enrichment_manual.sql`, `SEED_18_legacy_content_fix.sql`, `07_manufacturer_detail.sql`, `SEED_61_skybox_logo.sql` in that order.

### 3.6 App connection

Development default is committed in `src/Ntca.Web/appsettings.Development.json` (**DEV ONLY**): Eric’s SQL Server `10.100.1.87,1433`, database `wte_NtcaDatabase`, login `ntca_app` / `NtcaAppDev2026x`. Needs VPN/LAN. `dotnet user-secrets` still overrides. Production `appsettings.json` has a placeholder with **no password**. If the DB is down, Development shows a friendly page (server, database, VPN / user-secrets) instead of a raw exception.

Local Docker override:

```bash
cd src/Ntca.Web
dotnet user-secrets set ConnectionStrings:DefaultConnection \
  "Server=localhost,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web"
```

Development and Staging already have SocketLabs SMTP (`Email:SmtpHost` / `SmtpUser` / `SmtpPassword`). Production `appsettings.json` does **not** include the password — set IIS env vars after rotation.

Optional Injection API alternate:

```bash
dotnet user-secrets set SocketLabs:ServerId "12345"
dotnet user-secrets set SocketLabs:ApiKey "YOUR-KEY"
```

SMTP smoke:

```bash
dotnet run --project src/Ntca.Web -- email test --to you@example.com
```

### 3.7 Run

From the repo root:

```bash
dotnet run --project src/Ntca.Web --urls http://localhost:5290
```

Open http://localhost:5290

**Login codes:** `/owner/login` and `/SADMIN/login` with `egarrison@wte.net`. Development/Staging send via SocketLabs SMTP from `reports@wte.net`. If SMTP fails, the UI says so, the code is printed in the **terminal**, and a copy is written under `src/Ntca.Web/App_Data/dev-mail/`. Development also accepts `888888` for an existing staff/owner account (not Staging).

### 3.8 QA checklist

Use a real browser (or `curl -A "Mozilla/5.0 …"`). Bare `curl`/`wget` count as bots and are excluded from owner/SADMIN impression totals.

| URL | Verify |
|---|---|
| `/` | Shop / show / manufacturer counts; destination + new-shop strips |
| `/shops` | ~186 cards; name links; no Free Listing badge; no map |
| `/shops/mo` | Missouri hub (full name in H1 / breadcrumb / JSON-LD) |
| `/shops/ca` | California hub |
| `/shops/ca/burbank/burbank-sportscards-burbank-ca` | Detail, AI summary, JSON-LD in view-source |
| `/shops/ny/williamsville/dave-adam-s-card-world-retail-superstore-williamsville-ny.md` | Markdown |
| `/shops.md` / `/llms.txt` / `/sitemap.xml` / `/robots.txt` | Discovery |
| `/calendar` | Month grid; list; map (empty until venue coords) |
| `/shows/nc/raleigh/nc-collector-s-roadshow-raleigh-2026-11` | Show + Add to calendar |
| `/calendar.ics` | `BEGIN:VCALENDAR` / `VEVENT` |
| `/manufacturers` | 68 active |
| `/news/pokemon-trading-at-gaming-shows` | Pokémon post |
| `/new-to-trading-cards` / `/new-to-cards` | Glossary + grades; alias 301s |
| `/signup` | Simple form → `crm.Signup` + confirm email |
| `/onboarding` | After confirm; skippable |
| `/owner/login` | Code in console; Burbank stats after browsing `/shops` |
| `/SADMIN/login` | KPIs + top shops; `/SADMIN/queue/drafts` has GaryVee (not public) |
| `dotnet test src/Ntca.sln` | JSON-LD tests green |

### 3.9 Troubleshooting

| Symptom | Fix |
|---|---|
| Port 1433 in use | `docker ps`; stop the other SQL container or publish `-p 14333:1433` and use `localhost,14333` |
| Certificate / encrypt errors | `TrustServerCertificate=True` in the connection string; sqlcmd `-C` |
| `Incorrect syntax near ':'` | You opened an old SQLCMD copy. Pull this branch — every `.sql` is now plain T-SQL |
| Apple Silicon image errors | Enable Docker Rosetta; keep `--platform linux/amd64` |
| Re-run `run_all` | Safe. Schema is guarded; seeds MERGE; migrator skipped unless you edit `@MigratorPassword` |
| Site starts but queries fail | Development shows a friendly DB-down page. Check VPN to `10.100.1.87`, or `dotnet user-secrets list` if a local connection string is overriding |
| No login code | Watch the `dotnet run` terminal, not the browser |
)
