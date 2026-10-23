# NTCA — what and how to test

Playwright-first. Every case lists **route**, **steps**, **expected**, and **STABLE SELECTORS** (`data-testid` unless noted). Prefer `data-testid` over CSS/text. Base URL is configurable.

Do **not** wire these cases into CI yet. Eric will spin up Playwright against a local `dotnet run`.

---

## Prerequisites

### App

```bash
cd src/Ntca.Web
dotnet run
```

Default listen URL from `Properties/launchSettings.json`: **http://localhost:5290**

| Env | Default | Purpose |
|---|---|---|
| `BASE_URL` | `http://localhost:5290` | Playwright baseURL |
| `STAFF_EMAIL` | `egarrison@wte.net` | Seeded SUPERADMIN |
| `OWNER_EMAIL` | *(empty unless Eric set `@OwnerEmail` in SEED_21)* | Hobby Tyme owner login |
| `DEV_LOGIN_CODE` | `888888` | Development-only bypass (never Staging/Production) |
| `FIXTURE_DIR` | `tests/fixtures` | Generated logo + 6 photos |

Development login: request a code, then enter **888888**. UI copy says “Development-only”. Confirm `ASPNETCORE_ENVIRONMENT=Development`.

### SQL Eric must have run (plain T-SQL, SQLCMD Mode off)

On an existing QA database (already has shops):

1. `db/08_owner_workspace.sql` (copy: `db/ssms/08_owner_workspace_SSMS.sql`) — `IsEditorialFeatured`, locks, `LogoObjectKey`, photo columns, `ShopEditRequest.RequestKind`
2. `db/seed/SEED_21_hobby_tyme_featured.sql` (copy: `db/ssms/SEED_21_hobby_tyme_featured_SSMS.sql`) — Hobby Tyme editorial Featured. Optional `@OwnerEmail` at the top — **do not invent Jay Dudley’s email**. Leave empty to skip owner login; Impersonate can create one.

Also required from earlier QA (if not already applied): `04`, `05`, `06`, `07`, `SEED_17`–`SEED_19`, `SEED_61`.

Demo shop (do not substitute Cards And Stuff):

- Name: Hobby Tyme Sports Cards & Toys
- Slug: `hobby-tyme-sports-cards-toys-mebane-nc`
- Public: `/shops/nc/mebane/hobby-tyme-sports-cards-toys-mebane-nc`
- Featured note in audit: `Featured by Eric 2026-10-23 (editorial)` (editorial ribbon, **not** paid / Sponsored)

Staff account: `egarrison@wte.net`. Owner account: whatever Eric put in `@OwnerEmail` / `OWNER_EMAIL`.

### Test fixture images

Generated programmatically (not real photos) under `tests/fixtures/`:

| File | Use |
|---|---|
| `logo.png` | Square shop logo (navy + gold block) |
| `photo-1.png` … `photo-6.png` | Distinct solid-color JPEGs-as-PNG, 640×400 |

Regenerate:

```bash
python3 tests/fixtures/generate_fixtures.py
```

Upload these from `/owner` after Impersonate. Do not commit real shop photos.

### Email confirm (Dev)

Signup confirmation lands in `src/Ntca.Web/App_Data/dev-mail/` (and the console) when SocketLabs is not sending. Open the newest `.html` / `.txt` and follow the confirm link.

### CLI (not the web UI)

```bash
dotnet run --project src/Ntca.Web -- storage check
dotnet run --project src/Ntca.Web -- email test --to you@example.com
```

---

## Stable selectors (catalog)

Public chrome: `public-body`, `site-header`, `nav-home`, `nav-primary`, `nav-find-shops`, `header-join`, `nav-menu`, `site-footer`, `footer-nav`, `impersonate-banner`, `impersonate-stop`

Home / directory: `home-search`, `shops-search`, `shops-q`, `shops-location`, `shops-location-clear`, `shops-search-submit`, `shops-categories`, `cat-all`, `cat-sports`, `shops-filters-open`, `shops-filters`, `shops-chips`, `chip-*`, `shops-clear-all`, `shops-result-count`, `shop-card-{slug}`, `shop-featured-{slug}`, `shop-logo-{slug}`, `shop-placeholder-{slug}`

Shop detail: `shop-detail`, `shop-detail-name`, `shop-featured-ribbon`, `shop-detail-logo`, `shop-detail-cover`, `shop-detail-placeholder`, `shop-gallery`, `shop-owner-photos`, `shop-photo-{id}`, `shop-socials`

Manufacturers: `mfr-logo`, `mfr-description`

Pages: `page-about`, `page-people`, `page-grading`, `gallery-grid`, `gallery-item`, `lightbox`

Signup / onboarding: `signup-form`, `signup-name`, `signup-email`, `signup-submit`, `onboarding-step`, `onboarding-roles`, `onboarding-shop-q`

SADMIN: `sadmin-shell`, `sadmin-masthead`, `sadmin-brand`, `sadmin-public-site`, `sadmin-sign-out`, `sadmin-nav`, `sadmin-nav-dashboard`, `sadmin-nav-shops`, `sadmin-login-form`, `sadmin-login-email`, `sadmin-login-code`, `sadmin-login-submit`, `dashboard-h1`, `site-mode`, `site-mode-value`, `manage-mode`, `mode-form`, `mode-confirm`, `mode-save`, `inventory-strip`, `kpi-shops`, `kpi-manufacturers`, `shops-filters`, `shops-filter-q`, `shops-apply`, `shops-table`, `shop-row-{id}`, `shop-inspector`, `impersonate-owner`, `create-owner-email`, `create-owner-impersonate`, `impersonate-form`, `impersonate-confirm`, `impersonate-start`, `edits-pending`, `edit-approve-{id}`, `fyi-undo-{id}`

Owner: `owner-login-form`, `owner-login-email`, `owner-login-code`, `owner-login-submit`, `owner-h1`, `owner-logo-input`, `owner-photo-input`, `owner-logo-upload`, `owner-photo-upload`, `owner-photo-list`, `owner-public-link`, `owner-claim-q`, `owner-claim-submit`

---

## Smoke subset (run first)

Run these before the full suite. Fail-fast.

| # | Case |
|---|---|
| S1 | [P01 Home](#p01--home) |
| S2 | [P02 /shops search](#p02--shops-search) |
| S3 | [P05 Hobby Tyme featured](#p05--shop-detail--hobby-tyme-featured) |
| S4 | [P08 SkyBox manufacturer](#p08--manufacturers--skybox) |
| S5 | [A01 SADMIN login 888888](#a01--sadmin-login-development) |
| S6 | [A02 Dashboard counts](#a02--operations-dashboard) |
| S7 | [P14 Footer has no Staff / robots / llms / sitemap](#p14--footer-has-no-staff-or-discovery-links) |
| S8 | [P15 Preview noindex](#p15--robots-noindex-in-preview) |

Playwright entry: `tests/e2e/smoke.spec.ts` covers S1–S4, S7–S8 (unauthenticated). S5–S6 need the Dev login.

```bash
cd tests/e2e
npm install
npx playwright install chromium
BASE_URL=http://localhost:5290 npx playwright test
```

---

## Public pages

### P01 — Home

- **Route:** `/`
- **Steps:** Open home. Confirm header Join CTA, hero search, Discover shops.
- **Expected:** H1 “Protecting, Promoting and Powering the Hobby.” Featured/destination cards render. No preview banner text. If SEED_21 ran, Hobby Tyme may appear with a gold Featured ribbon.
- **Selectors:** `nav-primary`, `header-join`, `home-search`, `shop-card-hobby-tyme-sports-cards-toys-mebane-nc` (if in the six), `shop-featured-hobby-tyme-sports-cards-toys-mebane-nc`

### P02 — /shops search

- **Route:** `/shops`
- **Steps:** Type `hobby` in `shops-q`. Submit `shops-search-submit`.
- **Expected:** Result count updates. Hobby Tyme card is in the list when the shop is ACTIVE.
- **Selectors:** `shops-search`, `shops-q`, `shops-search-submit`, `shops-result-count`, `shop-card-hobby-tyme-sports-cards-toys-mebane-nc`

### P03 — Filters + category tabs + chips

- **Route:** `/shops`
- **Steps:** Click `cat-sports`. Open `shops-filters-open`, apply Open now or a specialty. Confirm chips. Click a chip, then `shops-clear-all`.
- **Expected:** URL holds `specialty` / `openNow`. Chips match filters. Clear all returns to `/shops` without specialty.
- **Selectors:** `shops-categories`, `cat-all`, `cat-sports`, `shops-filters`, `shops-chips`, `shops-clear-all`, `shops-result-count`

### P04 — State page

- **Route:** `/shops/mo` and `/shops/nc`
- **Steps:** Open each. Confirm shops for that state only.
- **Expected:** Heading uses the state name. Cards link to city/slug URLs.
- **Selectors:** `shop-card-*` (any), `nav-find-shops`

### P05 — Shop detail · Hobby Tyme Featured

- **Route:** `/shops/nc/mebane/hobby-tyme-sports-cards-toys-mebane-nc`
- **Steps:** Open the listing. Note ribbons, socials, photos.
- **Expected:** Gold **Featured** ribbon (navy text), not “Sponsored”. Destination copy is secondary if both flags apply. Social icons render when links exist. Before owner uploads: `shop-detail-placeholder` or Google photos. After owner uploads: `shop-detail-logo` + `shop-owner-photos` (6 items).
- **Selectors:** `shop-detail`, `shop-detail-name`, `shop-featured-ribbon`, `shop-socials`, `shop-detail-placeholder`, `shop-detail-logo`, `shop-gallery`, `shop-owner-photos`

### P06 — Placeholder images

- **Route:** any shop without owner/Google photos (many Free listings)
- **Steps:** Open `/shops` and a detail page that has no media.
- **Expected:** “Photo coming soon” placeholder, not a broken image.
- **Selectors:** `shop-placeholder-{slug}`, `shop-detail-placeholder`

### P07 — Manufacturers index

- **Route:** `/manufacturers`
- **Steps:** Open list, search or scroll to SkyBox.
- **Expected:** SkyBox links to `/manufacturers/skybox`.
- **Selectors:** (name link; no testid on every row)

### P08 — Manufacturers · SkyBox

- **Route:** `/manufacturers/skybox`
- **Steps:** Open detail.
- **Expected:** Logo image + description HTML (Wikimedia JPEG / seeded `LogoUrl` + `DescriptionHtml`).
- **Selectors:** `mfr-logo`, `mfr-description`

### P09 — Legacy 301 redirects

- **Route:** each key in `src/Ntca.Web/Services/LegacyUrls.cs` (`LegacyUrls.Map`)
- **Steps:** `GET` with redirects off. Assert **301** and `Location`.
- **Expected (minimum):**

| From | To |
|---|---|
| `/about.html` | `/about` |
| `/shops.html` | `/shops` |
| `/retailers` | `/shops` |
| `/dealers` | `/manufacturers` |
| `/gallery.html` | `/gallery` |
| `/join.html` | `/signup` |
| `/grading.html` | `/grading` |
| `/new-to-cards` | `/new-to-trading-cards` (separate controller) |

### P10 — About / people / grading

- **Routes:** `/about`, `/people`, `/grading`
- **Steps:** Open each. Confirm main heading and no preview banner.
- **Expected:** About mission copy; People list; Grading directory.
- **Selectors:** `page-about`, `page-people`, `page-grading`

### P11 — Gallery lightbox

- **Route:** `/gallery`
- **Steps:** Click `gallery-item`. Esc / Close dismisses.
- **Expected:** `lightbox` dialog opens with the full image + caption.
- **Selectors:** `gallery-grid`, `gallery-item`, `lightbox`

### P12 — Signup + email confirm via App_Data/dev-mail

- **Route:** `/signup`
- **Steps:** Submit name + unique email. Open newest file under `src/Ntca.Web/App_Data/dev-mail/`. Follow confirm link.
- **Expected:** Thanks page, then confirm succeeds. Dev mail hint may appear on the page.
- **Selectors:** `signup-form`, `signup-name`, `signup-email`, `signup-submit`

### P13 — Onboarding multi-select + type-ahead

- **Route:** `/onboarding/who` (after confirm, or with funnel token)
- **Steps:** Check more than one role (`onboarding-roles`). Continue. On `shop_find`, type 2+ letters in `onboarding-shop-q`. Pick a hit.
- **Expected:** Multi-select posts all roles. Typeahead calls `/api/v1/search/shops?q=`. Selecting a shop files a pending claim, not a live owner row (unless email domain matches).
- **Selectors:** `onboarding-step`, `onboarding-roles`, `onboarding-shop-q`

### P14 — Footer has no Staff or discovery links

- **Route:** `/` and `/shops`
- **Steps:** Inspect `site-footer`.
- **Expected:** No `Staff`, no `/SADMIN` link, no `/robots.txt`, `/llms.txt`, or `/sitemap.xml` links. `/account` and `/owner` remain.
- **Selectors:** `site-footer`, `footer-nav`, `header-join`

### P15 — Robots noindex in Preview

- **Route:** `/` (and `/shops`)
- **Steps:** Read response headers while `Site:Mode` is Preview.
- **Expected:** `X-Robots-Tag: noindex, nofollow`. No visible preview banner.
- **Selectors:** header only (no `preview-bar`)

### P16 — Mobile viewport

- **Routes:** `/`, `/shops`, Hobby Tyme detail, `/SADMIN/shops` (after login)
- **Steps:** 390×844 and 1280×800.
- **Expected:** Directory search stacks; category tabs scroll; SADMIN nav becomes a drawer (`sadmin-nav-toggle`); inspector is a drawer under 1200px. 44px tap targets on primary actions.

### P17 — Accessibility basics

- **Routes:** `/shops`, `/SADMIN/login`, `/signup`
- **Steps:** Tab through search + Join + filters. Check labels and one h1 per page.
- **Expected:** Visible focus, labelled inputs (`dir-q`, `dir-location`, login email), no missing button names. Reduced-motion does not break layout.

---

## SADMIN

### A01 — SADMIN login (Development)

- **Route:** `/SADMIN/login`
- **Steps:** Enter `STAFF_EMAIL`. Submit. Enter `888888`. Submit.
- **Expected:** Redirect `/SADMIN/dashboard`. Bypass hint mentions Development-only. Staff cookie `ntca.sadmin`.
- **Selectors:** `sadmin-login-form`, `sadmin-login-email`, `sadmin-login-code`, `sadmin-login-submit`

### A02 — Operations dashboard

- **Route:** `/SADMIN/dashboard`
- **Steps:** Read inventory strip and Needs attention / Activity.
- **Expected:** H1 “Operations”. Site mode is **read-only** text. Counts are queried (not hardcoded). Empty queues say “No pending items” only when the query succeeded. Activity title is “Activity” (not “Collector activity”). Top shops column is “Impressions (7 days)”. No Join CTA in the masthead.
- **Selectors:** `sadmin-shell`, `sadmin-nav`, `dashboard-h1`, `site-mode-value`, `manage-mode`, `inventory-strip`, `kpi-shops`, `kpi-manufacturers`, `attention-list` / `attention-empty`, `activity-list`, `top-shops`

### A03 — Manage mode confirm

- **Route:** `/SADMIN/dashboard/mode`
- **Steps:** Open Manage mode. Change Preview/Live without checking confirm — expect error. Check `mode-confirm`, submit.
- **Expected:** POST + antiforgery. Audit `SITE_MODE`. Dashboard status text updates. Status itself is not a toggle.
- **Selectors:** `manage-mode`, `mode-form`, `mode-select`, `mode-confirm`, `mode-save`, `mode-current`

### A04 — Shops filters + inspector

- **Route:** `/SADMIN/shops`
- **Steps:** Filter q=`Hobby Tyme`, state=`NC`. Apply. Click the name to open inspector (`?inspect=`).
- **Expected:** URL holds filters. Table: Shop, City/state, Claim (title case), Listing, Designation, Impressions (7 days), Links. Inspector shows raw source, designation review state, public preview, Open editor. Destination shops show “Destination — imported, review needed”, never auto-published as a paid designation. Pagination 25/50/100.
- **Selectors:** `shops-filters`, `shops-filter-q`, `shops-filter-state`, `shops-apply`, `shops-table`, `shop-inspector`, `inspector-name`, `inspector-source`, `inspector-designation`, `inspector-public`, `impersonate-owner`

### A05 — Impersonate owner (Hobby Tyme)

- **Route:** `/SADMIN/shops?q=Hobby+Tyme` → inspector → Impersonate
- **Steps:** Confirm checkbox. Start. If no owner, enter `OWNER_EMAIL` (or a disposable Eric address) on “Create owner account & impersonate”.
- **Expected:** POST + antiforgery. Lands on `/owner?shop=…`. Banner “Viewing as {owner} — Stop impersonating” on owner **and** public pages. Cannot impersonate staff. 60 minute TTL. Audit `IMPERSONATE_START`.
- **Selectors:** `impersonate-owner`, `impersonate-form`, `impersonate-confirm`, `impersonate-start`, `impersonate-banner`, `impersonate-stop`, `create-owner-email`, `create-owner-impersonate`

### A06 — Owner edit + logo + 6 photos + public verify

- **Route:** `/owner` (while impersonating)
- **Steps:** Upload `tests/fixtures/logo.png` via `owner-logo-input`. Upload `photo-1.png`…`photo-6.png` via `owner-photo-input`. Open `owner-public-link`.
- **Expected:** Logo square on public detail + directory card. Six photos in `shop-owner-photos`. Google photos hidden when owner media exists. Edit queue shows FYI rows (already live). Audit attributes “by staff X as owner Y”.
- **Selectors:** `owner-logo-input`, `owner-logo-upload`, `owner-photo-input`, `owner-photo-upload`, `owner-photo-list`, `shop-detail-logo`, `shop-owner-photos`, `shop-logo-hobby-tyme-sports-cards-toys-mebane-nc`

### A07 — Stop impersonating

- **Route:** any page with the banner
- **Steps:** Click `impersonate-stop`.
- **Expected:** Owner cookie cleared. Banner gone. Redirect `/SADMIN/shops`. Audit `IMPERSONATE_STOP`.
- **Selectors:** `impersonate-stop`, `impersonate-banner`

### A08 — Edit queue approval

- **Route:** `/SADMIN/queue/edits`
- **Steps:** From `/owner` submit a name/address change (`owner-identity-submit`). Approve the pending row. FYI media rows expose Undo.
- **Expected:** Identity fields go live only after Approve. FYI undo restores previous logo or removes the uploaded photo.
- **Selectors:** `edits-pending`, `edit-approve-{id}`, `edits-fyi`, `fyi-undo-{id}`, `owner-identity-submit`

### A09 — Manufacturers staff table

- **Route:** `/SADMIN/manufacturers`
- **Steps:** Filter q=`SkyBox`. Open inspector / Public.
- **Expected:** Logo thumbnail or “Missing logo”. Description excerpt or “No description”. Same pagination/inspector pattern as shops.
- **Selectors:** `mfr-filters`, `mfr-filter-q`, `mfr-table`, `mfr-inspector`

---

## CLI

### C01 — Storage sync/check

```bash
dotnet run --project src/Ntca.Web -- storage check
dotnet run --project src/Ntca.Web -- storage sync --dry-run
```

- **Expected:** Check reports local vs S3 (or local-only if `Storage:UseS3` is false). No secrets printed. Object keys at bucket root (`shops/{id}/logo.webp`, `shops/{id}/photos/{id}-{size}.webp`).

### C02 — Email test

```bash
dotnet run --project src/Ntca.Web -- email test --to you@example.com
```

- **Expected:** SMTP path in Development/Staging when `Email:SmtpHost` is set; otherwise honest failure / file sink. Do not print the SMTP password.

---

## Known not-yet-built

- SADMIN pages for **people**, **gallery**, and **storage** (do not invent them; public `/people` and `/gallery` exist)
- Paid **Sponsored** designation / `CurrentTierId = PAID` for Hobby Tyme (Featured here is editorial only)
- Bulk shop operations (no checkboxes)
- Funnel drop-off **rates** (counts only; “Definition pending”)
- Community photo *submissions* into `/gallery` (legacy import is live; new upload queue is not)
- Production SMTP secrets (Dev/Staging SMTP is committed by 2026-10-23 approval; rotate before Live)
- Owner self-service **Featured / Destination / claim** flags (staff only)
- Jay Dudley email/phone (never seeded; owner email is opt-in via `@OwnerEmail`)
- Cards And Stuff remains `DATA_REVIEW` — not the demo owner shop

---

## Suggested Playwright mapping

| Spec file | Cases |
|---|---|
| `tests/e2e/smoke.spec.ts` | S1–S4, S7–S8 |
| *(Eric)* `public.spec.ts` | P01–P17 |
| *(Eric)* `sadmin.spec.ts` | A01–A09 |
| *(Eric)* `owner.spec.ts` | A05–A07 with fixtures |

Use `test.skip` when `OWNER_EMAIL` is empty and the case needs a pre-seeded owner (Impersonate can still create one).
