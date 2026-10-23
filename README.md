# NTCA platform branch (`app/ntca-platform`)

**Do not merge this branch into `main`.** `main` is the live GitHub Pages site (flat HTML at repo root). Merging would move those pages into `legacy-site/` and take [tradingcardsassociation.com](https://tradingcardsassociation.com) offline until IIS cutover.

The production host is changing from GitHub Pages to **ASP.NET Core on IIS (Windows Server 2025)**, same shape as NCVetBiz, deployed with the existing GitHub Actions self-hosted runner. SQL Server is a new `NTCA` database on **HAMMER**. See [docs/HOSTING.md](docs/HOSTING.md).

## What this branch contains

| Path | Purpose |
|---|---|
| `legacy-site/` | Current static HTML/CSS/JS/images (holding folder; relative `./` links still work) |
| `db/` | SQL Server create / schema / seed / app login scripts |
| `docs/HOSTING.md` | IIS, `web.config`, SQL login usage |
| `docs/SEO_DISCOVERY.md` | Dynamic `/robots.txt`, `/llms.txt`, sitemaps |
| `docs/iis/web.config.example` | ANCM notes so discovery routes are not shadowed by static files |
| `CNAME`, `.nojekyll` | Left at repo root (Pages metadata). Unused once IIS serves the apex |

The runnable site is **.NET 9** under `src/` (`Ntca.Web` MVC + `/api/v1`, `Ntca.Core`, `Ntca.Data`). Schema SQL remains the source of truth — no EF migrations. Laptop QA: [QA_README.md](QA_README.md). **Do not merge to `main`.**

## Database

```bash
python3 db/tools/generate_seed.py
# then, against HAMMER or local SQL Server 2022:
./db/run_all.sh -S localhost,1433 -U sa -P '...' -C -v NtcaAppPassword='...'
```

Details: [db/README.md](db/README.md).
