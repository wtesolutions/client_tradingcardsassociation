# Authored blog posts (generator inputs)

`db/tools/generate_seed.py` MERGEs every `*.md` here into `cms.Post` via `SEED_30_blog_bloggers.sql`. Regeneration reads these files, so authored posts stay in the seed.

Front matter:

```
---
title: Exact title
slug: url-slug
published_at: 2026-10-22
author: Eric Garrison
author_org: NTCA
status: published
excerpt: Optional one-liner
---
verbatim markdown body
```

`cms.Post.BodyMarkdown` is the markdown after the front matter, stored verbatim. `cms.Post.Body` is the same copy converted to HTML (headings, bold, lists).

Required in this folder:

- `pokemon-trading-at-shows.md` — title `Bringing Pokémon Cards to Trade at Gaming Shows: Smart Moves for Collectors Who Want to Hold Their Keepers`, slug `pokemon-trading-at-gaming-shows`, PublishedAt 2026-10-22, author Eric Garrison / NTCA, status published.
- `what-is-pokemon-tcg-pocket.md` — title `What Is Pokémon TCG Pocket? A Parent's Guide`, slug `what-is-pokemon-tcg-pocket-a-parents-guide`, PublishedAt 2026-10-23.
- `from-phone-to-binder.md` — title `From Phone to Binder: How Pocket Connects to Real Card Collecting`, slug `from-phone-to-binder-how-pocket-connects-to-real-card-collecting`, PublishedAt 2026-10-23.
- `your-first-pokemon-prerelease.md` — title `Your First Pokémon Prerelease: What to Expect at a Local Shop`, slug `your-first-pokemon-prerelease-what-to-expect-at-a-local-shop`, PublishedAt 2026-10-23.
- `booster-etb-display-box.md` — title `Booster, ETB, Display Box: Which Sealed Pokémon Product Should You Buy?`, slug `booster-etb-display-box-which-sealed-pokemon-product-should-you-buy`, PublishedAt 2026-10-24.
- `protecting-and-grading-pokemon-cards.md` — title `Protecting and Grading Pokémon Cards: Sleeves to Slabs`, slug `protecting-and-grading-pokemon-cards-sleeves-to-slabs`, PublishedAt 2026-10-24.
- `30-years-of-pokemon-cards.md` — title `30 Years of Pokémon Cards: A Collector's Timeline`, slug `30-years-of-pokemon-cards-a-collectors-timeline`, PublishedAt 2026-10-24.
