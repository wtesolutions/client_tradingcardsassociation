# Legacy site crosscheck

Inventory of `legacy-site/` mapped to the ASP.NET app on `app/ntca-platform`. Status values: **migrated** (data already in SQL / a live route), **restyled** (ported into the navy-and-gold UI this pass), **missing** (still no public home), **intentionally dropped**.

Counts after this pass: **migrated 48 · restyled 22 · fixed 8 · missing 3 · intentionally dropped 8**.

`fixed` = items that were missing and now have a route, seed, or 301.

## HTML pages

| Legacy file / URL | Content | New route or DB table | Status | Notes |
|---|---|---|---|---|
| `about.html` / `/about.html` | Mission, vision, values, history teaser, Craig / Eric / Jay bios, Brody & Skylar | `/about`, `/about/{slug}`, `cms.PersonBio` | restyled | Bios corrected in `SEED_18` (SEED_40 had concatenated copy). 301 from `about.html`. |
| `bloggers.html` / `/bloggers.html` | 12 hobby blogs + logos | `/bloggers`, `/bloggers/{slug}`, `cms.Blogger` | restyled | Seeded in `SEED_30`. |
| `contact.html` / `/contact.html` | Contact copy + mock form | `/contact` | restyled | Mock `onsubmit=return false` form **intentionally dropped**; page uses `mailto:info@tradingcardsassociation.com` + `/signup`. |
| `dealers.html` / `/dealers.html` | Alias of manufacturers | `/manufacturers` | migrated | 301. |
| `directory.html` / `/directory.html` | NTCA Network six avenues + hall photo | `/network`, 301 `/directory` | restyled | Hall photo also in `/gallery` via `SEED_18`. |
| `email-autoresponder.html` | Welcome-email HTML template | — | intentionally dropped | Not a public page. 301 → `/` so the old URL does not 404. |
| `events.html` / `/events.html` | Halloween kickoff + 6 more association events | `/events`, `cms.Event` | restyled | Distinct from `/calendar` (`mkt.ShopEvent`). |
| `gallery.html` / `/gallery.html` | 20 community photos + mocked upload | `/gallery`, `cms.GalleryItem`, `core.MediaAsset` | restyled | Lightbox, categories, ImageObject JSON-LD. Upload form was a client mock — dropped (email CTA). |
| `grading-services.html` | 50 grading / auth services | `/grading` | restyled | Copied into `GradingCatalog` (legacy HTML). Not a SQL table. |
| `grading.html` | Meta-refresh to grading-services | `/grading` | migrated | 301. |
| `history.html` / `/history.html` | 150-year hobby essay | `/history` | restyled | Verbatim from the legacy article. |
| `home.html` / `/home.html` | Public home | `/` | restyled | 301 `/home` and `home.html`. |
| `index.html` | Teaser + Jotform `262326772024050` | `/` + `/signup` | restyled | Jotform teaser **replaced** by first-party signup. 301 `index.html` → `/`. |
| `join.html` / `/join.html` | Join / founding supporter | `/signup` (`/join` already mapped) | migrated | 301 `join.html`. |
| `landing.html` / `/landing.html` | Marketing landing | `/` | migrated | 301. |
| `manufacturers.html` | Manufacturer directory | `/manufacturers`, `dir.Manufacturer` | migrated | 68 seeded rows. |
| `members.html` / `members-area.html` | Insider / mock member login | `/account` | migrated | 301. Mock members-area UI dropped. |
| `new-to-trading-cards.html` | Glossary + grades | `/new-to-trading-cards` | migrated | `/new-to-cards` already 301s here. |
| `news.html` | News index | `/news`, `cms.Post` | migrated | |
| `products.html` | Product directory chrome, no SKUs | `/products` | restyled | Hub only — legacy grid had no real product catalog. |
| `retailers.html` | Retailer / marketplace list | `/shops` | migrated | 301. Full 50-row retailer scrape not re-imported (shops + orgs already live). |
| `shops.html` | Hobby shop directory | `/shops`, `dir.Shop` | migrated | 186 active shops. |
| `shows.html` | Card-show calendar | `/calendar` (`/shows`) | migrated | 28 `mkt.ShopEvent` rows. |
| `submit-event.html` | Jotform `262647966153064` | `/shows/submit` | migrated | First-party form replaced Jotform. 301. |

## People / bios

| Legacy file / URL | Content | New route or DB table | Status | Notes |
|---|---|---|---|---|
| `about.html#founder` | Craig Lloyd | `/about/craig-lloyd`, `cms.PersonBio` | restyled | Person JSON-LD + breadcrumbs. Photo `assets/craig-lloyd.jpeg`. |
| `about.html#technology` | Eric Garrison | `/about/eric-garrison` | restyled | `SEED_18` removes Jay's paragraphs that were glued onto Eric. |
| `about.html#first-member` | Jay Dudley | `/about/jay-dudley` | restyled | |
| `about.html` Brody & Skylar | Mascot card sharks | `/about` figure | restyled | `assets/brody-skylar.png` via `IFileStorage`. |
| `/people` | People index | `/people` | restyled | New. |
| Industry figure Gary Vaynerchuk | Draft dossier | `cms.IndustryFigure` | intentionally dropped | Unpublished (`IsPublished=0`). Appears in `/SADMIN/queue/drafts` only. See `docs/GARYVEE.md`. |
| Bloggers (12) | Pokémon TCG, SCI, Collector Nation, … | `/bloggers`, `cms.Blogger` | restyled | Logos under `legacy-site/assets/blogger-logos/`. |

There is no legacy board/directors page. No invented board roster.

## Gallery / photos

| Legacy file / URL | Content | New route or DB table | Status | Notes |
|---|---|---|---|---|
| `assets/graded-slabs.jpeg` … (20 `gallery.html` slots) | Graded stacks, shops, youth, shows, Jordan cutout, Hobby & Collectibles sign, CardVault desk, Durham Bulls, Mebane sign, concourse tables, Panthers counter | `/gallery`, `cms.GalleryItem` | restyled | Already in `SEED_50`. Served as `/media/legacy/assets/…`. |
| `assets/about-boys-cards.jpg` | Boys with stacks (About “why we exist”) | `/about`, `/gallery` slug `about-boys-cards` | fixed | Added in `SEED_18`. |
| `assets/network-convention-hall.jpeg` | Convention hall / NTCA screen | `/network`, `/gallery` slug `network-convention-hall` | fixed | Added in `SEED_18`. |
| `assets/vintage-cards.jpeg` | Tobacco-era cards | `/about#history` | restyled | About figure; not a separate gallery slug. |
| `assets/founder-mantle.jpeg` | Craig on the show floor | `/about#together` | restyled | |
| `assets/craig-lloyd.jpeg` / `eric-garrison.jpeg` / `jay-dudley.jpeg` | Officer photos | `/about/{slug}` | restyled | |
| `assets/cooper-flagg.jpeg` | Unused in any HTML page | — | intentionally dropped | In repo, never published. No invented caption. |
| `assets/novelty-cards.jpeg` | Unused in any HTML page | — | intentionally dropped | Same. |
| `ntca-logo.png` | Emblem | header/footer `~/ntca-logo.png` | migrated | |
| `ntca-flipping-logo.gif` | Spinning emblem | — | intentionally dropped | Style guide: static emblem only. |
| `assets/ntca-certified.png`, `assets/ntca-hero.webp` | Decorative | — | missing | Not referenced on a restyled page yet. |
| `assets/dir-logos/*` | Grading / manufacturer wordmarks | `/grading` (text), manufacturers | missing | 50+ PNGs exist; grader cards use names + URLs from HTML. Logo mapping was incomplete in the extractor. |
| `assets/card-show.jpeg` | Extra show photo | — | missing | Not in `gallery.html`; left uncaptioned rather than inventing a credit. |

## Events / CTAs / footer / embeds

| Legacy file / URL | Content | New route or DB table | Status | Notes |
|---|---|---|---|---|
| Halloween Community Kickoff Oct 31 2026 | Livestream / founding Q&A | `/events#ntca-community-kickoff`, `cms.Event` | restyled | Homepage teaser + `/events`. |
| Other `cms.Event` rows | Trade night, education, charity, roundtable, lobby days, convention | `/events` | restyled | Seeded in `SEED_20`. |
| Welcome / teaser CTA | Jotform keep-me-posted | `/signup` | migrated | |
| Join / Founding Supporter | `join.html` | `/signup` | migrated | |
| Footer mailto / @NTCA / member login | Footer | layout footer | restyled | Added gallery, people, network, events, grading, contact. |
| Jotform `262326772024050` | Teaser iframe | — | intentionally dropped | Replaced by `/signup`. |
| Jotform `262647966153064` | Submit event | `/shows/submit` | migrated | |
| PDFs | none in `legacy-site/` | — | — | No PDFs to port. |

## Redirects (301)

Every `*.html` filename above plus `/directory`, `/dealers`, `/retailers`, `/home`, `/landing`, `/members`, `/members-area`, `/submit-event`, `/grading-services`. Implemented in `LegacyUrls` / `LegacyRedirectsController`.

## Cannot port (and why)

1. **Jotform embeds** — replaced by first-party `/signup` and `/shows/submit`. Re-embedding HIPAA Jotform on the public home was already rejected for Preview.
2. **`email-autoresponder.html`** — SocketLabs/HTML email template, not a site page.
3. **Gallery upload + contact mock forms** — `onsubmit="return false"`; they never stored data. Wiring a fake queue would lie to submitters.
4. **Product SKU catalog** — `products.html` had filters and empty/demo chrome, no real product rows.
5. **Retailer 50-row list** — superseded by the live shop + org directory; 301 to `/shops`.
6. **Unpublished Gary Vee figure** — staff draft only.
7. **Unused images** (`cooper-flagg.jpeg`, `novelty-cards.jpeg`, `card-show.jpeg`) — never appeared on a legacy page with a caption/credit.
8. **Spinning logo GIF** — conflicts with the style guide.

## How media is served

`IFileStorage.PublicUrl` → `/media/{objectKey}` locally (`legacy/assets/…` resolved from `legacy-site/`), or `Storage:PublicBaseUrl` when `Storage:Provider=S3`. Object keys match `core.MediaAsset.ObjectKey` so the same files can move to the provisioned bucket.
