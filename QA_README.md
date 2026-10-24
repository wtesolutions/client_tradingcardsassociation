# NTCA laptop QA

Draft branch only. **Do not merge to `main`.** Runtime is **.NET 9** (SDK 9.0.x). .NET 10 was not required for a clean build.

## Prerequisites

- .NET 9 SDK (`dotnet --list-sdks`)
- SQL Server 2019+ reachable. **Development default** is Eric’s box `10.100.1.87,1433` / `wte_NtcaDatabase` / `ntca_app` (VPN). Docker/localhost still works via user-secrets.
- `sqlcmd` **or** SSMS (plain T-SQL — SQLCMD Mode is not required)

### Windows

```bat
winget install Microsoft.DotNet.SDK.9
cd db
run_all.cmd -S HAMMER -E
```

Docker alternative:

```bat
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="NtcaSqlDev2026x" -p 1433:1433 --name ntca-sql -d mcr.microsoft.com/mssql/server:2022-latest
run_all.cmd -S localhost,1433 -U sa -P "NtcaSqlDev2026x" -C
```

### macOS

```bash
brew install --cask dotnet-sdk
# sqlcmd: brew tap microsoft/mssql-release && brew install mssql-tools18
cd db
./run_all.sh -S localhost,1433 -U sa -P 'NtcaSqlDev2026x' -C
```

SSMS: open `db/NTCA_FULL_INSTALL.sql` (or the identical `db/ssms/NTCA_FULL_INSTALL_SSMS.sql`) and Execute. Database `wte_NtcaDatabase`, login `ntca_app` / `NtcaAppDev2026x` — DEV ONLY. SQLCMD Mode is not required.

**Existing QA database (Eric already has 186 shops):** run these scripts in order (open either `db/` or the identical `db/ssms/*_SSMS.sql` copy):

1. `04_onboarding_funnel.sql` — two-stage funnel columns
2. `05_google_places.sql` — Google Places match/cache/photo/call-log tables
3. `06_shop_social.sql` — social platform catalog + `dir.ShopSocial` (migrates Website / Facebook / Instagram)
4. `SEED_17_shop_enrichment_manual.sql` — 2026-10-23 verified first-10 enrichment (Boxseat / Burbank / GamesandCards socials, Beckett-only websites for Bakersfield and Wonder Water, descriptions, lat/lng, Burbank + Bakersfield hours) plus SADMIN review notes (`docs/DATA_REVIEW.md`)
5. `SEED_18_legacy_content_fix.sql` — corrected About bios + boys/hall gallery rows
6. `SEED_19_bio_fix.sql` — force-set Craig / Eric / Jay `BioText` (and Quote) to their own legacy founder-body; use this if `99_verify` still reports a bio LEAK after SEED_18
7. `07_manufacturer_detail.sql` — `dir.Manufacturer.DescriptionHtml` + `LogoUrl`
8. `SEED_61_skybox_logo.sql` — SkyBox `LogoUrl` = `/media/manufacturers/skybox.jpg`
9. `08_owner_workspace.sql` — editorial Featured, owner locks/logo, photo columns, FYI edit status
10. `09_places_enrich.sql` — rich Place Details columns, Google reviews, field sources, enrich resume
11. `SEED_21_hobby_tyme_featured.sql` — Hobby Tyme editorial Featured (optional `@OwnerEmail`; do not invent Jay’s email)

All eleven are safe to run twice. Playwright cases: **[TESTING.md](TESTING.md)** at the repo root (`dotnet run` → http://localhost:5290). A second full `run_all` is also safe: `01_schema.sql` uses `IF NOT EXISTS` / `CREATE OR ALTER`. Optional migrator: edit `DECLARE @MigratorPassword` at the top of `03_app_login.sql` (default `N'__SKIP__'`).

**After those scripts:** open `db/99_verify.sql` (or the identical `db/ssms/99_verify_SSMS.sql`) and Execute. It is read-only and returns one PASS/FAIL/WARN grid (schema, seed counts, SEED_18 bios, data quality, Google tables, DATA_REVIEW flags) plus a summary row. Edit the `@Expect` table at the top if a documented count changes. `db/tools/validate.sql` only points at this file.

## Connection string

`src/Ntca.Web/appsettings.Development.json` is committed (**DEV ONLY**): `Server=10.100.1.87,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x;…`. Production `appsettings.json` has no password. `dotnet user-secrets` still overrides Development. If SQL is unreachable, Development shows a friendly page (server, database, VPN / user-secrets hint) instead of a raw exception.

To use local Docker instead:

```bash
cd src/Ntca.Web
dotnet user-secrets set ConnectionStrings:DefaultConnection \
  "Server=localhost,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web"
```

Development and Staging send login codes and signup confirmations over **SocketLabs SMTP** (STARTTLS 587, from `reports@wte.net`). The Injection API keys are optional. If SMTP fails, the SADMIN/owner UI says so, writes `src/Ntca.Web/App_Data/dev-mail/`, and prints the code in the console. In Development only, staff/owner login also accepts **888888** (`Auth:DevBypassCode` — never Staging or Production).

```bash
dotnet run --project src/Ntca.Web -- email test --to you@example.com
```

> Dev/test SMTP password committed by Eric's approval 2026-10-23; rotate in SocketLabs and move to IIS env vars/user-secrets before production.

Optional Google Places (batch only — visitor pages never call Google):

```bash
dotnet user-secrets set "GooglePlaces:ApiKey" "<KEY>" --project src/Ntca.Web
```

Enable **Places API (New)** on the Google Cloud project. Leave the key unset to no-op. Daily/monthly caps default to 300 / 3000 (`GooglePlaces:DailyCallLimit` / `MonthlyCallLimit`). Enrich existing shops (local Mac CLI — see `docs/PLACES.md`):

```bash
dotnet run --project src/Ntca.Web -- places enrich --dry-run
dotnet run --project src/Ntca.Web -- places enrich --limit 5
dotnet run --project src/Ntca.Web -- places status
```

Older match-only CLI still works:

```bash
dotnet run --project src/Ntca.Web -- google-places match --dry-run
dotnet run --project src/Ntca.Web -- google-places match
```

Or `/SADMIN/google` → Dry-run match → Run match. Low-confidence rows stay in the review list until Approve / Reject / manual place ID. Then `google-places refresh` (or Refresh now) for ratings, hours, and 1–3 cached photos. Nightly hosted job refreshes about 1/30 of matched shops.

`appsettings.Development.json` is committed. Use user-secrets or `appsettings.Development.local.json` (gitignored) for a laptop override.

## Run

```bash
dotnet run --project src/Ntca.Web --urls http://localhost:5290
```

## URLs

| Path | What to check |
|---|---|
| `/` | Home with real shop / show / manufacturer counts |
| `/shops` | Mobile-first cards (thumb + Call / View shop); shop name links to detail; no Free Listing badge; sticky search/ZIP/Near me; `?zip=` / `?lat=&lng=&sort=distance`; **no map** |
| `/shops/{state}` | State hub (e.g. `/shops/ca`) — impressions use `DIRECTORY_STATE` |
| `/shops/{state}/{city}` | City hub (breadcrumb target) |
| `/shops/{state}/{city}/{slug}` | Shop detail + AI summary + JSON-LD `@graph` (try Burbank Sportscards or Dave & Adam's) |
| `/shops/{state}/{city}/{slug}.md` or `?format=md` | Plain-text shop page for LLM crawlers |
| `/shops.md` | Markdown index of every active shop |
| `/t/website?shop={id}` `/t/phone` `/t/directions` `/t/social` | Click redirects (website / phone / maps / social) |
| `/api/v1/shops?source=app` | Shop API; `source=app` records `API_APP` impressions |
| `/calendar` and `/shows` | Month (default), list, map; Subscribe (.ics); Submit a show |
| `/shows/submit` | Promoter show submission → `mkt.EventSubmission` PENDING_REVIEW |
| `/shows/{state}` | State hub |
| `/shows/{state}/{city}/{slug}` | Show detail + Add to calendar (Google / Outlook / .ics) |
| `/calendar.ics` | National ICS feed |
| `/shows/{state}.ics` | Per-state ICS |
| `/shows/{state}/{city}/{slug}.ics` | Single-show VEVENT |
| `/manufacturers` + `/{slug}` | Manufacturer pages |
| `/news` + `/news/pokemon-trading-at-gaming-shows` | Pokémon post |
| `/new-to-trading-cards`, `/new-to-cards`, `/guide` | Glossary + condition guide (`/new-to-cards` 301s) |
| `/signup` and `/join` | Simple Preview form (name, email, optional ZIP / phone / social). Turnstile + confirm email |
| `/signup/confirm/{token}` | Double opt-in; CTA to skippable `/onboarding` |
| `/onboarding` | Post-signup “get to know you” (multi-select roles, typeahead, skippable) |
| `/account` | Preview member resume / skip |
| `/SADMIN/funnel` | Registration vs onboarding conversion, per-role |
| `/SADMIN/queue/onboarding` | Pending shop/show/mfr add + claim |
| `/robots.txt` `/llms.txt` `/sitemap.xml` | Discovery; sitemap has dynamic shop/show/mfr URLs; llms.txt invites assistants |
| `/agent` | Human-readable concierge; `?role=collector&city=Raleigh&state=NC&interests=pokemon` |
| `/.well-known/ai-concierge` | JSON manifest for assistants |
| `POST /api/v1/agent/ask` | Rate-limited JSON ask (no auth). Do not send name/email/phone |
| `/owner/login` | `egarrison@wte.net` — code in console; edits Burbank Sportscards |
| `/SADMIN/login` | Same email; dashboard KPIs are live counts + 7-day shop impressions + AI visits |
| `/SADMIN/agents` | AI Visitors: visits/inquiries + editable UA catalog |
| `/SADMIN/shops` | Per-shop 7-day impression column; open a row for 7/30/90 + chart |
| `/SADMIN/queue/drafts` | Gary Vaynerchuk industry-figure draft (not public) |
| `/SADMIN/google` | Places match / refresh / low-confidence review / daily SKU call counts |
| `/about`, `/people`, `/about/{slug}` | Restyled bios (Craig, Eric, Jay) + Person JSON-LD. Run `SEED_18` if bios still look concatenated. |
| `/gallery` | Legacy photos, lightbox, categories, ImageObject JSON-LD. `/media/legacy/assets/…` |
| `/network` | Six-avenue Network page (`directory.html`). `/directory` and `directory.html` 301 here. |
| `/history` | Full hobby history essay |
| `/events` | Association calendar (`cms.Event`) including Halloween kickoff |
| `/bloggers` | 12 blogs + logos |
| `/contact` | Mailto + signup (legacy mock form dropped) |
| `/grading` | 50 grading services from `grading-services.html` |
| `/products` | Hub (no SKU catalog in legacy) |
| `*.html` | 301 to the new route — see `docs/LEGACY_CROSSCHECK.md` |

## Known gaps

- Calendar map plots only venues that already have lat/lng. Seeded `shows.html` editions have no coordinates, so the map is empty until staff add them.
- S3 is optional. Development default is local disk + `legacy-site/` via `IFileStorage`. Provision with `./provision-s3.sh --profile default --region us-east-1 --env prod` (see `docs/HOSTING.md`). Never commit keys.
- Turnstile is off in Development.
- Upcoming GaryVee card-show rows were **not** seeded: the 2026-10-22 dossier had no confirmed card show with date + location + his role.
- Top-100 destination shops are in `SEED_16` (`SourceSystem=TOP100-2026-10-22`). Matched rows fill empty fields only. See `docs/TOP100_SHOPS.md`.
- Shop hours, photos, and reviews are largely unseeded. JSON-LD omits `openingHoursSpecification`, `image`, and `aggregateRating` unless real rows exist. Do not invent ratings.
- Impression seeds are intentionally empty. Counts appear after you browse public pages.

## Impression tracking + shop JSON-LD

Raw events: `analytics.ShopImpression` (ShopId, Surface, Position, PageUrl, SessionHash, IsBot, CreatedAt). Session hash is `SHA256(sid + yyyy-MM-dd + daily salt)` — no raw IP. Known bots are stored with `IsBot=1` and excluded from dashboards.

Daily rollup (BackgroundService, no SQL Agent): `analytics.usp_RollupShopDailyStats` fills `analytics.ShopDailyStats` and refreshes `dir.ShopDailyStat`. `feed.FeedImpression` stays rotation-feed-only.

Surfaces: `DIRECTORY_LIST`, `DIRECTORY_SEARCH`, `DIRECTORY_STATE`, `HOME_ROTATION`, `FEATURED`, `SHOW_EXHIBITOR`, `MAP_PIN`, `API_APP`, `DETAIL`, `CLICK_WEBSITE`, `CLICK_PHONE`, `CLICK_DIRECTIONS`, `CLICK_SOCIAL`.

Verify:

1. Browse `/shops` then a shop detail in a real browser (or `curl -A "Mozilla/5.0 ..."`). `curl`/`wget` user-agents are stored as `IsBot=1` and excluded from dashboards. Click Website / Phone / Directions if present.
2. Open `/owner` as `egarrison@wte.net` (Burbank Sportscards). 7/30/90 cards and the bar chart should show the list + detail traffic.
3. Open `/SADMIN/dashboard` — impression / view / click KPIs and a top-shops table. `/SADMIN/shops` has an Imp 7d column.
4. SQL: `SELECT TOP 20 Surface, IsBot, PageUrl, CreatedAt FROM analytics.ShopImpression ORDER BY ImpressionId DESC;`
5. Shop JSON-LD: view-source on `/shops/ny/williamsville/dave-adam-s-card-world-retail-superstore-williamsville-ny` and find `application/ld+json`. `dotnet test src/Ntca.sln` runs the parse + schema.org rule checks. Paste the graph into https://validator.schema.org/ for a visual spot-check.
6. `curl -s http://localhost:5290/llms.txt` should list shop `.md` URLs. `curl -s http://localhost:5290/shops/ca/burbank/burbank-sportscards-burbank-ca.md` is the text version.

## Mobile directory cards

`/shops`, `/shops/{state}`, and `/shops/{state}/{city}` use the same card: OSM static-map thumb when lat/lng exist, otherwise a card-icon placeholder; distance when ZIP/Near me is set; specialty / Destination pills (**no Free Listing badge**); one-line blurb; shop **name is a link** to the detail page plus **Call** / **View shop →**. Sticky bar: search + ZIP, Near me / state / distance sort. **No map** on `/shops`. State hubs (`/shops/mo`) breadcrumb, H1, title, and BreadcrumbList JSON-LD use the full name (Missouri). Shop detail still has a compact header. Try `/shops?zip=91502&sort=distance` and `/shops/mo`.

## AI concierge / VIP AI visitors

Same page HTML for every visitor — no cloaking. Assistants get an extra welcome aside, `Link: </.well-known/ai-concierge>; rel="agent-concierge"`, and `X-NTCA-Agent-*` headers. Visits store assistant name, role, verified flag, path, timestamp — never IP, UA string, name, email, or phone.

Verify:

```bash
curl -sI -A 'ChatGPT-User' http://localhost:5290/ | grep -iE 'link:|x-ntca-agent'
curl -s -A 'Claude-User' http://localhost:5290/ | grep -i 'Welcome, Claude'
curl -s http://localhost:5290/.well-known/ai-concierge
curl -s -A 'ChatGPT-User' -H 'Content-Type: application/json' \
  -d '{"assistant":"ChatGPT","humanRole":"collector","location":{"city":"Raleigh","state":"NC","zip":"27601"},"interests":["pokemon"],"question":"Where can we buy Pokémon cards?"}' \
  http://localhost:5290/api/v1/agent/ask
```

Expect Triangle Area Trading Cards (Raleigh) and an NC card show when one is published. `name` / `email` / `phone` fields are ignored and listed in `warnings`. `/SADMIN/agents` shows the visit + inquiry after a few seconds (flush interval is 3s).

## Preview signup + get-to-know-you

`/signup` and `/join` (and every Join Preview CTA) are a **simple** Preview form matching the live/legacy fields: **Name**, **Email**, optional **ZIP**, optional **contact number**, optional **social**, Submit. No “who are you”, no shop/show pickers before the account exists. Turnstile stays on (skipped in Development). Submit writes `crm.Signup` PENDING_CONFIRM, emails the confirm link (`reports@wte.net` / console fallback), and shows `/signup/thanks`.

After confirm (`/signup/confirm/{token}`), **Get to know you** (`/onboarding`) is optional, skippable, and resumable from `/account`. Role cards are **multi-select** (collector + shop owner + promoter, …). Selected roles run in sequence (shared steps such as interests only once). Shop / show / manufacturer pickers are type-ahead (`/api/v1/search/{shops|shows|promoters|manufacturers}`, min 2 chars, 10 results, name + city/state/ZIP). “I don’t see my shop/show” writes `crm.OnboardingSubmission` PENDING_REVIEW (and `mkt.EventSubmission` for a new show). Selecting an existing shop as owner is a **pending claim**, not instant ownership.

`/SADMIN/funnel` reports registration conversion vs onboarding completion, per-role. `/SADMIN/queue/onboarding` is the add/claim queue.

Walk: name + email (+ ZIP) → confirm link → Get to know you → check Collector and Shop owner → ZIP 27601 → interests → shop type-ahead “bur” → claim or add. `/new-to-cards` 301s to `/new-to-trading-cards`.

Screenshots (390×844 unless noted): `/opt/cursor/artifacts/ntca_shops_390.png`, `ntca_shops_1440.png` (1440×900), `ntca_shop_detail_390.png`, `ntca_funnel_who_390.png`, `ntca_funnel_zip_390.png`, `ntca_funnel_interests_390.png`, `ntca_funnel_experience_390.png`, `ntca_funnel_travel_390.png`, `ntca_funnel_email_390.png`, `ntca_funnel_results_390.png`, `ntca_funnel_shop_find_390.png`, `ntca_funnel_shop_tier_390.png`, `ntca_funnel_show_390.png`.

## Desktop screenshots (1440×900)

Captured from this branch against the local Docker DB:

| Page | File |
|---|---|
| Home | `/opt/cursor/artifacts/ntca_home.png` |
| `/shops` | `/opt/cursor/artifacts/ntca_shops.png` |
| `/calendar` month | `/opt/cursor/artifacts/ntca_calendar_month.png` |
| Show (River City) | `/opt/cursor/artifacts/ntca_show_river_city.png` |
| Manufacturer (Ace Authentic) | `/opt/cursor/artifacts/ntca_manufacturer_ace_authentic.png` |
| `/SADMIN` dashboard | `/opt/cursor/artifacts/ntca_sadmin_dashboard.png` |

## Email

From-address is `reports@wte.net` / National Trading Card Association. Normal path: `Email:SmtpHost` / `SmtpPort` / `SmtpUser` / `SmtpPassword` (MailKit STARTTLS). Alternate: `SocketLabs:ServerId` + `SocketLabs:ApiKey` (Injection API 2.0.1). Fallback: console + `App_Data/dev-mail/`. Success/failure is logged without the SMTP password or API key. Production `appsettings.json` does not contain the SMTP password.
