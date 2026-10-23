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
| `db/03_app_login.sql` | Least-privilege `ntca_app` login; `EXECUTE` on app schemas including `analytics` |
| `db/run_all.sql` | sqlcmd `:r` chain |
| `db/run_all.sh` / `db/run_all.cmd` | Apply 00 → 01 → 02 → `SEED_*` → 03; default `NtcaMigratorPassword=__SKIP__` |
| `db/NTCA_FULL_INSTALL.sql` | Same chain inlined for SSMS SQLCMD Mode (`wte_NtcaDatabase` / `NtcaAppDev2026x` defaults) |
| `db/ssms/NTCA_FULL_INSTALL_SSMS.sql` | Plain T-SQL (no SQLCMD Mode); same defaults |
| `db/source/ntca_schema.original.sql` | v1 tables |
| `db/source/01_schema_additions.sql` | crm/cms/seo, owner hours/stats, industry figures, analytics impressions + **AI concierge** (`AgentUaRule` / `AgentVisit` / `AgentInquiry`) |
| `db/source/01_schema_directory_calendar.sql` | Manufacturers, orgs, promoters, venues, exhibitors |
| `db/tools/generate_seed.py` | `--schema-only` rebuilds `01_schema.sql` without rewriting seed docs |
| `db/tools/generate_combined.py` | Writes `run_all.sql` + `NTCA_FULL_INSTALL.sql` |
| `db/05_google_places.sql` | Idempotent Google Places match / 30-day cache / photo / call-log tables |
| `db/ssms/05_google_places_SSMS.sql` | Same, plain T-SQL for Eric’s SSMS session |
| `db/06_shop_social.sql` | Social platform catalog + `dir.ShopSocial`; migrates Website/Facebook/Instagram |
| `db/ssms/06_shop_social_SSMS.sql` | Same, plain T-SQL for Eric’s SSMS session |
| `db/seed/SEED_17_shop_enrichment_manual.sql` | Verified 2026-10-23 first-10 enrichment (socials, Beckett-only sites, coords, hours) + SADMIN review notes |
| `db/ssms/SEED_17_shop_enrichment_manual_SSMS.sql` | Same, plain T-SQL |
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

**Email:** official `SocketLabs.EmailDelivery` **2.0.1**, from `reports@wte.net` / National Trading Card Association. Empty ServerId/ApiKey → console log + `App_Data/mail/` pickup. Dev prints the login code in the console.

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
| SEED_17 manual shop enrichment | **In this change** | Official-page socials for Boxseat / Burbank / GAC; Beckett WEBSITE for Bakersfield + Wonder Water; lat/lng `manual-maps-2026-10-23`; Burbank + Bakersfield hours (owner-confirm). Review flags in `docs/DATA_REVIEW.md`. No Google ratings. No name/phone/address changes. |

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

Alternatives to SSMS: **Azure Data Studio** or the **SQL Server (mssql)** VS Code extension. For `NTCA_FULL_INSTALL.sql` you still need SQLCMD variable mode (`:setvar`).

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
./run_all.sh -S localhost,1433 -U sa -P 'NtcaSqlDev2026x' -C \
  -v NtcaAppPassword='NtcaAppDev2026x'
```

HAMMER example:

```bash
./run_all.sh -S HAMMER -E -v NtcaAppPassword='YourAppPasswordHere'
```

A second `run_all` is safe. `$(NtcaAppPassword)` is required. `NtcaMigratorPassword` defaults to `__SKIP__`.

SSMS / Azure Data Studio: enable SQLCMD Mode, open `db/NTCA_FULL_INSTALL.sql`:

```
:setvar NtcaDatabase wte_NtcaDatabase
:setvar NtcaAppLogin ntca_app
:setvar NtcaAppPassword NtcaAppDev2026x
```

### 3.6 App connection

Development default is committed in `src/Ntca.Web/appsettings.Development.json` (**DEV ONLY**): Eric’s SQL Server `10.100.1.87,1433`, database `wte_NtcaDatabase`, login `ntca_app` / `NtcaAppDev2026x`. Needs VPN/LAN. `dotnet user-secrets` still overrides. Production `appsettings.json` has a placeholder with **no password**. If the DB is down, Development shows a friendly page (server, database, VPN / user-secrets) instead of a raw exception.

Local Docker override:

```bash
cd src/Ntca.Web
dotnet user-secrets set ConnectionStrings:DefaultConnection \
  "Server=localhost,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web"
```

Optional SocketLabs (otherwise console + `App_Data/mail/`):

```bash
dotnet user-secrets set SocketLabs:ServerId "12345"
dotnet user-secrets set SocketLabs:ApiKey "YOUR-KEY"
```

### 3.7 Run

From the repo root:

```bash
dotnet run --project src/Ntca.Web --urls http://localhost:5290
```

Open http://localhost:5290

**Login codes:** `/owner/login` and `/SADMIN/login` with `egarrison@wte.net`. With SocketLabs empty, the six-digit code is printed in the **terminal** (`DEV login code for egarrison@wte.net (LOGIN|SADMIN_LOGIN): ######`) and written under `src/Ntca.Web/App_Data/mail/`.

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
| sqlcmd truncates the password | Do not use `#` in `-v` passwords |
| Apple Silicon image errors | Enable Docker Rosetta; keep `--platform linux/amd64` |
| `NtcaAppPassword` missing | `run_all.sh` exits until you pass `-v NtcaAppPassword=...` |
| Re-run `run_all` | Safe. Schema is guarded; seeds MERGE; migrator skipped unless you set a real migrator password |
| Site starts but queries fail | Development shows a friendly DB-down page. Check VPN to `10.100.1.87`, or `dotnet user-secrets list` if a local connection string is overriding |
| No login code | Watch the `dotnet run` terminal, not the browser |
)
