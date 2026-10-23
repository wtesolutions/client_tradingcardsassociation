# Google Places enrich (local CLI)

Eric runs this **on his Mac** against the NTCA SQL Server. It is **not** part of the IIS web app runtime. Visitor pages never call Google. The nightly hosted refresh (`GooglePlacesHostedService`) still only refreshes already-matched shops; new matching and rich details happen here.

Places API (New) must be enabled on the Google Cloud project that owns the key.

## Key (never commit)

Config key: **`GooglePlaces:ApiKey`**

User secrets (Eric’s Mac):

```bash
cd src/Ntca.Web
dotnet user-secrets set "GooglePlaces:ApiKey" "<KEY>"
```

Environment variable equivalent:

```bash
export GooglePlaces__ApiKey='<KEY>'
```

`appsettings.json` keeps `"ApiKey": ""`. Do not paste a key into git.

## SQL Eric must run first (plain T-SQL, SQLCMD Mode off)

On an existing QA database (already has `05`):

1. `db/09_places_enrich.sql` (copy: `db/ssms/09_places_enrich_SSMS.sql`)

If Google tables are missing, run `db/05_google_places.sql` first.

## Commands

From the repo root, Development environment (same connection string as `dotnet run`):

```bash
# How many shops, how many need a Place ID, estimated calls + cost. No Google calls.
dotnet run --project src/Ntca.Web -- places enrich --dry-run

# First small real run (5 shops that still need work).
dotnet run --project src/Ntca.Web -- places enrich --limit 5

# Full run (honors DailyCallLimit 300 / MonthlyCallLimit 3000 and --max-calls).
dotnet run --project src/Ntca.Web -- places enrich

# Counts + remaining quota.
dotnet run --project src/Ntca.Web -- places status

# Existing nightly-style refresh (rating / hours / photos only).
dotnet run --project src/Ntca.Web -- places refresh --dry-run
```

Useful flags: `--rate-ms 1500` (default), `--max-calls N`, `--skip-website`, `--shop ID`, `--force` (redo fresh rows), `--dry-run` / `-n`.

Resume: `dir.ShopPlaceEnrich` stores last match / details / website timestamps. Re-runs skip combos younger than 30 days unless `--force`. Low-confidence matches stay `PENDING_REVIEW` and are not retried until staff acts in `/SADMIN/google`.

## What enrich does

1. **Match** shops without a Place ID via Places API (New) `places:searchText` (`pageSize` 5, `locationBias` circle 10 miles when the shop has coordinates). Query is `{name} {address} {city} {ST} {zip}`.
2. Auto-link only when name similarity is high **and** phone, street number, distance ≤ 0.5 mi, or same city + very strong name agree. Otherwise the candidate lands on `/SADMIN/google` for Approve / Reject / manual Place ID.
3. **Place Details (New)** with the rich field mask below. Rating, review count, up to 5 reviews (author, rating, text, relative time, attribution URI), hours, business status, website, phones, Maps URI, address, location, photo refs, editorial summary, types, primary type, price level, plus code, parking/payment/accessibility when present.
4. Cache several photos under `gplace/{shopId}/{slot}.jpg` via `IFileStorage` (same as the existing refresh). Skip when the owner uploaded photos or locked `photos`.
5. **Website crawl** (optional; `--skip-website` to omit): home page, then `/contact`, `/contact-us`, `/about`, `/about-us`. 8 second timeout. User-Agent `NTCA Directory Bot/1.0`. Regex only — no OpenAI/LLM. Fills **empty** email + Facebook / Instagram / TikTok / YouTube / X / Whatnot / eBay store.

**Never overwrite owner-edited fields.** Owner locks (`OwnerFieldLocks`) and existing `dir.ShopSocial` rows win. Blanks only. Each fill writes `dir.ShopFieldSource` (`google` or `website` + `FetchedUtc`).

Place IDs are stored permanently on `dir.GooglePlaceMatch`. Cached content expires after 30 days (Google Maps Platform terms: refresh cached content on a schedule; the nightly job already does this for matched shops).

## Field mask → SKU (highest wins)

Published field SKUs: [Place Data Fields (New)](https://developers.google.com/maps/documentation/places/web-service/data-fields) (checked 2026-10-23).

**Match `SearchTextNear`** mask includes `nationalPhoneNumber` → **Places API Text Search Enterprise** (not Pro).

**Rich details** mask includes `reviews` + `editorialSummary` → **Places API Place Details Enterprise + Atmosphere**.

**Photo bytes** → **Places API Place Details Photos**.

Existing cheap match (`SearchTextAsync`, used by older `google-places match` tests) stays Pro-tier: `places.id,displayName,formattedAddress,location`. Enrich uses the Enterprise match mask.

## Cost (published list, USD)

Source: [Google Maps Platform pricing](https://developers.google.com/maps/billing-and-pricing/pricing) (checked 2026-10-23). Prices are per 1,000 events in the first paid tier after the monthly free cap.

| SKU | Free cap / month | Then (0–100k) |
|---|---|---|
| Text Search Enterprise | 1,000 | $35.00 |
| Place Details Enterprise + Atmosphere | 1,000 | $25.00 |
| Place Details Photos | 1,000 | $7.00 |

`--dry-run` prints list price **and** price after those free caps.

### Full directory enrich (example: 186 ACTIVE shops, none matched yet, 5 photos)

| Call | Count | List | After free cap |
|---|---|---|---|
| Text Search Enterprise | 186 | $6.51 | $0.00 |
| Place Details E+A | 186 | $4.65 | $0.00 |
| Photos (5 each) | 930 | $6.51 | $0.00 |
| **Total** | **1,302** | **$17.67** | **$0.00** if these SKUs are unused this month |

If every shop already has a Place ID, drop the Text Search row (details + photos only): list **$11.16**, still $0 under unused free caps.

Default `GooglePlaces:DailyCallLimit` is **300**. A first full run of ~186 shops + photos is ~1,300 Google calls, so it will stop at 300 and resume the next day (`places enrich` is resume-safe). Raise the limit in user-secrets if Eric wants one sitting:

```bash
dotnet user-secrets set "GooglePlaces:DailyCallLimit" "1500"
```

At `--rate-ms 1500`, 300 calls take about 7.5 minutes.

## Review in /SADMIN

1. Sign in at `/SADMIN/login` (Dev bypass `888888`).
2. Open `/SADMIN/google` (`google-places-page`).
3. Low-confidence rows: Approve (stores Place ID, Featured stays false), Reject, or paste a manual Place ID.
4. After approve, re-run `places enrich --limit 5` (or `--shop ID`) to pull details + photos + website.
5. Public shop page shows rating + **Reviews from Google** (`shop-google-reviews`) with required Google attribution. Those reviews are **not** emitted as schema.org `aggregateRating`.

## New-shop discovery

`places discover` is **not implemented in this pass**. Enrich existing shops first. City-list + `ShopCandidate` staging can follow.

## Related

- Existing `google-places match|refresh|purge` CLI still works.
- Spend log: `analytics.GoogleApiCallLog` (`TEXT_SEARCH`, `PLACE_DETAILS`, `PLACE_PHOTO`).
- Photos URL: `/media/gplace/{shopId}/{slot}`.
