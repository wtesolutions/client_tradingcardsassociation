# NTCA laptop QA

Draft branch only. **Do not merge to `main`.** Runtime is **.NET 9** (SDK 9.0.x). .NET 10 was not required for a clean build.

## Prerequisites

- .NET 9 SDK (`dotnet --list-sdks`)
- SQL Server 2019+ reachable (HAMMER, localhost, or Docker 2022)
- `sqlcmd` **or** SSMS with SQLCMD Mode

### Windows

```bat
winget install Microsoft.DotNet.SDK.9
cd db
run_all.cmd -S HAMMER -E -v NtcaAppPassword="YourAppPasswordHere"
```

Docker alternative:

```bat
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="NtcaSqlDev2026x" -p 1433:1433 --name ntca-sql -d mcr.microsoft.com/mssql/server:2022-latest
run_all.cmd -S localhost,1433 -U sa -P "NtcaSqlDev2026x" -C -v NtcaAppPassword="NtcaAppDev2026x"
```

### macOS

```bash
brew install --cask dotnet-sdk
# sqlcmd: brew tap microsoft/mssql-release && brew install mssql-tools18
cd db
./run_all.sh -S localhost,1433 -U sa -P 'NtcaSqlDev2026x' -C \
  -v NtcaAppPassword='NtcaAppDev2026x'
```

SSMS: Query → SQLCMD Mode, then open `db/NTCA_FULL_INSTALL.sql` and:

```
:setvar NtcaDatabase NTCA
:setvar NtcaAppLogin ntca_app
:setvar NtcaAppPassword YourAppPasswordHere
```

`$(NtcaAppPassword)` is required. Avoid `#` in passwords (sqlcmd `-v` can truncate). A second full `run_all` is safe: `01_schema.sql` uses `IF NOT EXISTS` / `CREATE OR ALTER`, and `run_all` passes `NtcaMigratorPassword=__SKIP__` unless you set a real migrator password.

## Connection string (never commit)

```bash
cd src/Ntca.Web
dotnet user-secrets set ConnectionStrings:DefaultConnection \
  "Server=localhost,1433;Database=NTCA;User ID=ntca_app;Password=NtcaAppDev2026x;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=Ntca.Web"
```

Optional SocketLabs (login codes + signup confirmations from `reports@wte.net`):

```bash
dotnet user-secrets set SocketLabs:ServerId "12345"
dotnet user-secrets set SocketLabs:ApiKey "YOUR-KEY"
```

Leave those empty on a laptop. The app logs the message and writes `App_Data/mail/`. In Development the **login code is printed in the console**.

Copy `appsettings.Development.json.example` only if you want a local file — it is gitignored.

## Run

```bash
dotnet run --project src/Ntca.Web --urls http://localhost:5290
```

## URLs

| Path | What to check |
|---|---|
| `/` | Home with real shop / show / manufacturer counts |
| `/shops` | Directory, state filter; destination shops show a Destination chip; `?view=map` plots pins with lat/lng |
| `/shops/{state}` | State hub (e.g. `/shops/ca`) — impressions use `DIRECTORY_STATE` |
| `/shops/{state}/{city}` | City hub (breadcrumb target) |
| `/shops/{state}/{city}/{slug}` | Shop detail + AI summary + JSON-LD `@graph` (try Burbank Sportscards or Dave & Adam's) |
| `/shops/{state}/{city}/{slug}.md` or `?format=md` | Plain-text shop page for LLM crawlers |
| `/shops.md` | Markdown index of every active shop |
| `/t/website?shop={id}` `/t/phone` `/t/directions` `/t/social` | Click redirects (website / phone / maps / social) |
| `/api/v1/shops?source=app` | Shop API; `source=app` records `API_APP` impressions |
| `/calendar` and `/shows` | Month (default), list, map; Subscribe (.ics) |
| `/shows/{state}` | State hub |
| `/shows/{state}/{city}/{slug}` | Show detail + Add to calendar (Google / Outlook / .ics) |
| `/calendar.ics` | National ICS feed |
| `/shows/{state}.ics` | Per-state ICS |
| `/shows/{state}/{city}/{slug}.ics` | Single-show VEVENT |
| `/manufacturers` + `/{slug}` | Manufacturer pages |
| `/news` + `/news/pokemon-trading-at-gaming-shows` | Pokémon post |
| `/new-to-trading-cards` | Glossary + condition guide |
| `/signup` | Inserts `crm.Signup` |
| `/robots.txt` `/llms.txt` `/sitemap.xml` | Discovery; sitemap has dynamic shop/show/mfr URLs |
| `/owner/login` | `egarrison@wte.net` — code in console; edits Burbank Sportscards |
| `/SADMIN/login` | Same email; dashboard KPIs are live counts + 7-day shop impressions |
| `/SADMIN/shops` | Per-shop 7-day impression column; open a row for 7/30/90 + chart |
| `/SADMIN/queue/drafts` | Gary Vaynerchuk industry-figure draft (not public) |

## Known gaps

- Calendar map plots only venues that already have lat/lng. Seeded `shows.html` editions have no coordinates, so the map is empty until staff add them.
- No S3 / CloudFront — uploads go to `wwwroot/uploads`.
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

From-address is `reports@wte.net` / National Trading Card Association. Config keys: `SocketLabs:ServerId`, `SocketLabs:ApiKey`, `Email:FromAddress`, `Email:FromName`. Official `SocketLabs.EmailDelivery` 2.0.1 client. Success/failure is logged without the API key.
