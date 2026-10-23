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
| Email | **SocketLabs SMTP** (MailKit, STARTTLS 587) from `reports@wte.net` whenever `Email:SmtpHost` is set. Injection API (`SocketLabs:ServerId` / `ApiKey`) is the alternate. File sink `App_Data/dev-mail/` + an honest “not sent” UI is the fallback. `Auth:DevBypassCode` / `888888` is **Development-only** (not Staging, not Production). |
| Media | Amazon S3 + CloudFront (OAC). Local disk is the Development default. Object keys in `core.MediaAsset` (`legacy/assets/…`, `google-cache/…`) |

> **Dev/test SMTP password committed by Eric's approval 2026-10-23; rotate in SocketLabs and move to IIS env vars/user-secrets before production.**
>
> The password is in `appsettings.Development.json` and `appsettings.Staging.json` only — **not** in `appsettings.json` (Production). For IIS server testing set `ASPNETCORE_ENVIRONMENT=Staging` on the app pool (or `web.config` `<environmentVariable>`). Smoke with:
>
> ```bash
> dotnet run --project src/Ntca.Web -- email test --to you@example.com
> ```

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
app.MapGet("/calendar", ...);          // alias of /shows
app.MapGet("/calendar.ics", ...);
app.MapGet("/shows/{state}.ics", ...);
app.MapGet("/api/v1/calendar", ...);
```

Calendar, show, promoter, and manufacturer routes must also be MVC-owned (not leftover `wwwroot` HTML). See [CALENDAR.md](CALENDAR.md) and [MANUFACTURERS.md](MANUFACTURERS.md).

While `core.SiteSetting` `Site:Mode` = `Preview` (or ASPNETCORE_ENVIRONMENT != Production), robots must `Disallow: /` and every HTML response should send `X-Robots-Tag: noindex`. See [SEO_DISCOVERY.md](SEO_DISCOVERY.md).

## SQL Server login usage (IIS)

Runtime (Web + API app pools) uses **only** `ntca_app`:

```
Server=HAMMER,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=***;
Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Set the password via IIS app-pool environment variable `ConnectionStrings__DefaultConnection` (or Windows DPAPI / secret store). **Never** put it in git, in `appsettings.json`, or in the GitHub Actions YAML.

DbUp / schema apply (deploy job or a one-off SSMS session) uses **`ntca_migrator`** (or a DBA sa session). That login is not used by the running site.

`ntca_app` is `db_datareader` + `db_datawriter` + `EXECUTE` on app schemas. It is not `db_owner` and cannot create tables.

## Media: local disk vs S3

`IFileStorage` is selected by `Storage:Provider` (default `Local`).

| Provider | Behavior |
|---|---|
| `Local` (Development default) | Writes `wwwroot/uploads`. Reads uploads first, then `legacy-site/` for `legacy/assets/…`. Public URLs are `/media/{key}`. |
| `S3` | `AWSSDK.S3` against `Storage:Bucket` in `Storage:Region`. Public URLs use `Storage:PublicBaseUrl` (CloudFront). Credentials come from the default AWS chain (instance role, env vars, or `AWS_PROFILE`). Optional non-secret `Storage:Profile` selects a named shared profile. **Never from git.** |

### Provision the bucket (Eric)

From the repo root, AWS CLI v2, an admin profile:

```bash
./provision-s3.sh --profile default --region us-east-1 --env prod
```

Windows Server:

```powershell
.\provision-s3.ps1 -Profile default -Region us-east-1 -Env prod
```

The script is idempotent. It creates the bucket (override `--bucket` if the default `ntca-tradingcardsassociation-media` is taken), turns **Block Public Access ON**, SSE-S3, versioning, `BucketOwnerEnforced`, lifecycle (abort multipart 7 days; expire `google-cache/` 30 days; noncurrent versions 90 days), CORS for `localhost:5290` + `tradingcardsassociation.com` + `www`, a CloudFront distribution with Origin Access Control, a bucket policy that allows only that distribution, and a least-privilege IAM user/policy (`s3:GetObject` / `PutObject` / `DeleteObject` / `ListBucket`).

Pass `--create-access-key` (or `-CreateAccessKey`) only if the app cannot use an instance/role profile. The key is printed once and **must not** be committed. `--dry-run` prints commands.

After it finishes, set (user-secrets locally, IIS env on the server):

```
Storage:Provider=S3
Storage:Bucket=<bucket>
Storage:Region=us-east-1
Storage:PublicBaseUrl=https://<cloudfront-domain>
```

```bat
cd src\Ntca.Web
dotnet user-secrets set "Storage:Provider" "S3"
dotnet user-secrets set "Storage:Bucket" "ntca-tradingcardsassociation-media"
dotnet user-secrets set "Storage:Region" "us-east-1"
dotnet user-secrets set "Storage:PublicBaseUrl" "https://XXXXXXXX.cloudfront.net"
```

Do not put access keys — or the CloudFront URL / bucket IDs — in `appsettings.json`. User-secrets, IIS env, or docs only.

### Object keys (must match IAM)

`S3FileStorage` / `MediaPaths.ObjectKey` write at the **bucket root**, not under `uploads/`:

| Local | S3 key |
|---|---|
| `wwwroot/uploads/manufacturers/skybox.jpg` | `manufacturers/skybox.jpg` |
| `wwwroot/uploads/gplace/{shopId}/{slot}.jpg` | `gplace/{shopId}/{slot}.jpg` |
| `legacy-site/assets/…` | `legacy/assets/…` |
| Google cache (if used) | `google-cache/…` |

A policy of only `arn:aws:s3:::BUCKET/uploads/*` 403s those keys. Object RW must be `arn:aws:s3:::BUCKET/*`.

### Dev bucket (provisioned)

These are not secrets; keep them out of `appsettings.json`.

| Item | Value |
|---|---|
| Bucket | `ntca-tradingcardsassociation-media` |
| Region | `us-east-1` |
| CloudFront | `E3B0BMPLKILPJ3` · `https://dgo8ht4qmva4e.cloudfront.net` |
| IAM user / policy | `ntca-media-dev` |

### Mac: profile + sync

```bash
aws configure --profile ntca-media-dev
# paste the ntca-media-dev access key (once; not committed)

cd src/Ntca.Web
dotnet user-secrets set "Storage:Provider" "S3"
dotnet user-secrets set "Storage:Bucket" "ntca-tradingcardsassociation-media"
dotnet user-secrets set "Storage:Region" "us-east-1"
dotnet user-secrets set "Storage:PublicBaseUrl" "https://dgo8ht4qmva4e.cloudfront.net"
# optional, instead of AWS_PROFILE:
# dotnet user-secrets set "Storage:Profile" "ntca-media-dev"
cd ../..

# Widen the existing user policy if it was scoped to uploads/* (no new user):
aws iam put-user-policy --user-name ntca-media-dev --policy-name ntca-media-dev-object-rw --policy-document '{"Version":"2012-10-17","Statement":[{"Sid":"ListBucket","Effect":"Allow","Action":["s3:ListBucket","s3:GetBucketLocation"],"Resource":"arn:aws:s3:::ntca-tradingcardsassociation-media"},{"Sid":"ObjectRW","Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject"],"Resource":"arn:aws:s3:::ntca-tradingcardsassociation-media/*"}]}'

AWS_PROFILE=ntca-media-dev dotnet run --project src/Ntca.Web -- storage sync --dry-run
AWS_PROFILE=ntca-media-dev dotnet run --project src/Ntca.Web -- storage sync
AWS_PROFILE=ntca-media-dev dotnet run --project src/Ntca.Web -- storage check
```

`storage sync` uploads every file under `wwwroot/uploads` and the legacy assets `LocalFileStorage` serves, using the same keys as `S3FileStorage`, with `Content-Type` and `Cache-Control`. Existing objects with the same size and ETag are skipped. `storage check` HEADs `manufacturers/skybox.jpg`, `legacy/assets/graded-slabs.jpeg`, and `legacy/assets/craig-lloyd.jpeg` via the S3 API and `PublicBaseUrl`.
