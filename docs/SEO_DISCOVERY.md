# SEO and LLM discovery (robots, llms.txt, sitemaps)

The ASP.NET Core site on IIS (Windows Server 2025) serves these as **dynamic routes**, not static files. Data comes from SQL Server views in the `seo` schema. See [HOSTING.md](HOSTING.md) for IIS/`web.config` notes.

Canonical host (live): `https://tradingcardsassociation.com`

## `/robots.txt`

Generated each request (short cache) from `core.SiteSetting` + environment.

**Staging, non-Production environment, or `Site:Mode = Preview`:**

```
User-agent: *
Disallow: /
```

Also send `X-Robots-Tag: noindex, nofollow` on HTML until Live.

**Live (`Site:Mode = Live` and Production):**

```
User-agent: *
Allow: /
Disallow: /owner
Disallow: /owner/
Disallow: /SADMIN
Disallow: /SADMIN/
Disallow: /api
Disallow: /api/
Disallow: /account
Disallow: /account/
Disallow: /signup/confirm
Disallow: /preview

Sitemap: https://tradingcardsassociation.com/sitemap.xml
```

Block owner admin, super-admin, API, and account/session pages. Do not list preview-gated URLs while the site is in Preview.

## `/llms.txt` and `/llms-full.txt`

Follow [llmstxt.org](https://llmstxt.org): Markdown, UTF-8, one H1, a short summary, then link lists.

`/llms.txt` (small, always generated):

- Site name and one-paragraph summary (from `Site:LlmsSummary` or a default)
- Key sections: Home, About, History, Directory, Shops, Events, News/Blog, Membership/Join, Contact
- Link each section to its canonical public URL
- Optional: “Optional” block pointing at `/llms-full.txt`

`/llms-full.txt` (optional, larger):

- Same header
- Directory overview + top shops from `seo.vw_SitemapShops` (name, city, state, loc)
- Upcoming public events from `seo.vw_SitemapEvents`
- Published posts from `seo.vw_SitemapPosts`
- Featured bloggers from `cms.Blogger`
- Membership / join links
- Omit `/owner`, `/SADMIN`, `/api`, private profiles

Both must honor Preview/staging: either `Disallow` via robots or return `noindex` content that says the site is not public yet.

## `/sitemap.xml` (index)

A sitemap index, not a single urlset. Children stay under 50 000 URLs and 50 MB uncompressed (split before either limit).

| Child | Source view | Notes |
|---|---|---|
| `/sitemap/static.xml` | `seo.vw_SitemapStatic` | Marketing/content pages (about, join, directory, …) |
| `/sitemap/shops.xml` | `seo.vw_SitemapShops` | Split by state (`/sitemap/shops-nc.xml`) when a file would exceed limits |
| `/sitemap/events.xml` | `seo.vw_SitemapEvents` | Association events + published shop events |
| `/sitemap/posts.xml` | `seo.vw_SitemapPosts` | Published news/blog only |
| `/sitemap/bloggers.xml` | `seo.vw_SitemapBloggers` | Public blogger directory |
| `/sitemap/profiles.xml` | `seo.vw_SitemapProfiles` | **Only** `IsPublicProfile = 1` members |
| `/sitemap/images.xml` | `seo.vw_SitemapImages` | Image sitemap extension: approved shop photos + published gallery |

Each `<url>`:

- `loc` = `https://tradingcardsassociation.com` + path from the view (`LocPath`)
- `lastmod` = `UpdatedAt` (UTC) from the row
- Only **approved / public / active** records
- Canonical slugs only (no query strings, no `?code=`)

Shop loc pattern (matches the architecture plan): `/shops/{state}/{city-slug}/{slug}`

Event loc: `/events/{slug}` (association) or `/shops/{state}/{city-slug}/{shopSlug}/events/{slug}` (shop calendar)

Image sitemap: `xmlns:image` with `image:loc` built from CloudFront/`Media:PublicBaseUrl` + `core.MediaAsset.ObjectKey` (or `LegacyPath` until S3 cutover).

## Caching

| Route | Cache |
|---|---|
| `/robots.txt` | 5 minutes public; vary on site mode |
| `/llms.txt` | 15 minutes |
| `/llms-full.txt` | 15 minutes |
| `/sitemap.xml` and children | 1 hour; `ETag` from `MAX(lastmod)` of the view |

Invalidate on publish/unpublish of a shop, event, post, or `Site:Mode` change.

## Ping / IndexNow (Live only)

After a sitemap rebuild or a batch publish:

1. Google / Bing sitemap ping is legacy; prefer Search Console URL inspection + IndexNow.
2. IndexNow: POST changed URLs to `api.indexnow.org` with a key file served at `/{key}.txt` (key in secret store, not git).
3. Do **not** ping while `Site:Mode = Preview` or on staging hosts.

## SQL support (this branch)

`01_schema.sql` adds:

- Slug + unique indexes on shops (already present), events (`mkt.ShopEvent`, `cms.Event`), posts (`cms.Post`), bloggers, bios
- `UpdatedAt` on those entities and on `core.MediaAsset`
- `seo.vw_SitemapStatic`, `Shops`, `Events`, `Posts`, `Bloggers`, `Profiles`, `Images`

The app should `SELECT LocPath, LastModUtc, StateCode` from those views — no extra filtering in C# except host prefix and file splitting.
