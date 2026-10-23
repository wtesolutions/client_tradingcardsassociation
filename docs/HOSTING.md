# NTCA hosting target

The public site is moving off a **flat HTML GitHub Pages** site (`main` root) onto **ASP.NET Core on IIS**, same shape as the NCVetBiz reference (`ncvets.biz`).

## Target

| Item | Value |
|---|---|
| OS | Windows Server 2025 |
| Runtime | ASP.NET Core / .NET 10 LTS (same solution shape as NCVetBiz: Web + API + Data) |
| Web server | IIS + ASP.NET Core Module (ANCM), in-process hosting |
| App pools | `NTCA_Web`, `NTCA_API` |
| Paths (proposed) | `D:\websites\NTCA\NTCA_Web`, `D:\websites\NTCA\NTCA_API`, `D:\websites\NTCA\Logs`, `D:\websites\NTCA\Backups` |
| Deploy | GitHub Actions on the existing self-hosted Windows runner (`wte_apps3`): build → publish → stop pool → copy (preserve server `appsettings.Production.json`) → start pool → smoke |
| SQL | New database `NTCA` on WTE server **HAMMER** (name is a sqlcmd variable; Eric can choose `NTCADirectory` instead) |
| SQL logins | `ntca_app` (runtime, least privilege) and optional `ntca_migrator` (DDL only). See [db/README.md](../db/README.md) |
| Email | SocketLabs (same account pattern as NCVetBiz; SMTP vs Injection API is an Eric decision) |
| Media | Amazon S3 + CloudFront; object keys in `core.MediaAsset` |

**`main` stays untouched** so GitHub Pages keeps serving the current static site until cutover. This branch is a draft-only platform branch. **Do not merge** until IIS is ready and Pages is pointed away or retired.

## What Eric must decide before cutover

1. **Database name** on HAMMER: scripts default to `NTCA`. Architecture plan suggested `NTCADirectory`. Either works; set `-v NtcaDatabase=...`.
2. **SQL host**: HAMMER public IP (IIS, like NCVetBiz) vs VPN IP for workstations.
3. **Passwords**: create `ntca_app` (and optional `ntca_migrator`) with `03_app_login.sql`. Passwords are sqlcmd variables only — never committed. Store the app password in the IIS app-pool environment or the server secret store.
4. **DNS cutover**: apex `tradingcardsassociation.com` → IIS, or keep Pages for marketing and put the app on `app.` / `www.`.
5. **From-address / MX / SPF / DKIM** for SocketLabs before signup mail goes live.

## IIS / `web.config` notes for discovery routes

The .NET app must **own** these URLs (they are not static files in `wwwroot`):

- `/robots.txt`
- `/llms.txt` and `/llms-full.txt`
- `/sitemap.xml` (index) and `/sitemap/*.xml` children

If a physical `wwwroot/robots.txt` exists, static-file middleware wins and the environment-aware robots document never runs. Same for `sitemap.xml`.

`docs/iis/web.config.example` shows:

- ANCM in-process handler for the ASP.NET Core app
- no IIS default-document override that would serve a leftover `index.html`
- optional rewrite comments (usually unnecessary — MVC/minimal APIs can map the exact paths)
- request filtering so `.txt` / `.xml` are not blocked
- forwarding of `X-Forwarded-For` / `X-Forwarded-Proto` if a proxy sits in front

In `Program.cs` (when the web project is added):

```csharp
// Register discovery endpoints BEFORE UseStaticFiles.
app.MapGet("/robots.txt", ...);
app.MapGet("/llms.txt", ...);
app.MapGet("/llms-full.txt", ...);
app.MapGet("/sitemap.xml", ...);
app.MapGet("/sitemap/{name}.xml", ...);
```

While `core.SiteSetting` `Site:Mode` = `Preview` (or ASPNETCORE_ENVIRONMENT != Production), robots must `Disallow: /` and every HTML response should send `X-Robots-Tag: noindex`. See [SEO_DISCOVERY.md](SEO_DISCOVERY.md).

## SQL Server login usage (IIS)

Runtime (Web + API app pools) uses **only** `ntca_app`:

```
Server=HAMMER,1433;Database=NTCA;User ID=ntca_app;Password=***;
Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Set the password via IIS app-pool environment variable `ConnectionStrings__DefaultConnection` (or Windows DPAPI / secret store). **Never** put it in git, in `appsettings.json`, or in the GitHub Actions YAML.

DbUp / schema apply (deploy job or a one-off SSMS session) uses **`ntca_migrator`** (or a DBA sa session). That login is not used by the running site.

`ntca_app` is `db_datareader` + `db_datawriter` + `EXECUTE` on app schemas. It is not `db_owner` and cannot create tables.
