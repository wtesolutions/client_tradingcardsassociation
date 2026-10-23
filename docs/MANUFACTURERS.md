# Manufacturers directory

Specialty pages for card manufacturers. Data is **factual fields only** from the Wikipedia [Trading card](https://en.wikipedia.org/wiki/Trading_card) article (CC BY-SA), retrieved 2026-10-23. Long-form copy lives in nullable `dir.Manufacturer.DescriptionHtml` and is staff-authored in `/SADMIN/manufacturers`. Leave it empty unless the text is real; the public page renders nothing when it is empty. Do not invent history copy.

Source snapshot: `db/tools/inputs/wikipedia_trading_card.md`  
Generated CSV: `db/tools/manufacturers_source.csv`  
Seed: `db/seed/SEED_60_manufacturers.sql`

## Routes (SADMIN + public)

| Route | Purpose |
|---|---|
| `/manufacturers` | Landing: sport / non-sport / TCG filters |
| `/manufacturers/{slug}` | Specialty page (name, categories, topics, notable sets, Wikipedia link) |
| `/SADMIN/manufacturers` | List + editor: website, `LogoUrl`, `DescriptionHtml`, active flag |

Sitemap: `seo.vw_SitemapManufacturers` → `/sitemap/manufacturers.xml`  
`llms.txt` / `llms-full.txt` should list the landing page and active manufacturers.

JSON-LD on a manufacturer page: `Organization` + `sameAs` (Wikipedia). Do not paste Wikipedia article text.

## Tables

- `dir.Manufacturer` — slug, name, normalized name, source section (`Manufacturers` / `Non-sports` / `Import`), `SourceSystem`, `RetrievedOn`, nullable `DescriptionHtml`, nullable `LogoUrl`

## SkyBox logo

Committed file: `src/Ntca.Web/wwwroot/uploads/manufacturers/skybox.jpg`  
Public URL: `/media/manufacturers/skybox.jpg` (IFileStorage key `manufacturers/skybox.jpg`)  
Source: [Wikimedia Commons File:Skybox_logo.jpg](https://commons.wikimedia.org/wiki/File:Skybox_logo.jpg)  
Direct bytes: https://upload.wikimedia.org/wikipedia/commons/3/36/Skybox_logo.jpg  

Seed: `db/seed/SEED_61_skybox_logo.sql` (sets `LogoUrl` only; no description).
- `dir.ManufacturerCategory` / `ManufacturerCategoryMap` — `SPORT`, `NONSPORT`, `TCG`
- `dir.ManufacturerTopic` — Yes-columns from the Wikipedia tables (Baseball, Comic book, …)
- `dir.ManufacturerProduct` — notable sets only (seeded: Supersisters, 1979, 72 cards)
- `dir.ManufacturerLink` — `WIKIPEDIA` / `WEBSITE` / `SAMEAS`

Later imports (directory snapshot `SourceSystem = CTCA-2026-10-22`) MERGE on normalized name / alias (Leaf Trading Cards → `leaf-inc`, Fanatics is inserted if missing, Topps stays `topps`).

## SADMIN merge

Match on `NormalizedName` (punctuation stripped, legal suffixes dropped) and the alias list in `db/tools/generate_extra.py` (`MFR_ALIASES`). Do not auto-merge Fanatics into Topps — they are separate rows.
