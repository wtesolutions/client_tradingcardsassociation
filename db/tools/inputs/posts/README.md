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
