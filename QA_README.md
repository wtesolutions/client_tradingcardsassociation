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
| `/shops` | Directory, state filter; destination shops show a Destination chip |
| `/shops/{state}/{city}/{slug}` | Shop detail (try Burbank Sportscards) |
| `/calendar` and `/shows` | Show list; Subscribe (.ics) |
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
| `/SADMIN/login` | Same email; dashboard KPIs are live counts |
| `/SADMIN/queue/drafts` | Gary Vaynerchuk industry-figure draft (not public) |

## Known gaps

- Calendar month/map views are not in this pass yet (list + ICS + state hub + detail + JSON `/api/v1/calendar` are).
- No S3 / CloudFront — uploads go to `wwwroot/uploads`.
- Turnstile is off in Development.
- Upcoming GaryVee card-show rows were **not** seeded: the 2026-10-22 dossier had no confirmed card show with date + location + his role.
- Top-100 destination shops are in `SEED_16` (`SourceSystem=TOP100-2026-10-22`). Matched rows fill empty fields only. See `docs/TOP100_SHOPS.md`.
- Schema apply is idempotent (`IF NOT EXISTS` / `CREATE OR ALTER`). A second full `run_all` succeeds on an existing database.

## Email

From-address is `reports@wte.net` / National Trading Card Association. Config keys: `SocketLabs:ServerId`, `SocketLabs:ApiKey`, `Email:FromAddress`, `Email:FromName`. Official `SocketLabs.EmailDelivery` 2.0.1 client. Success/failure is logged without the API key.
