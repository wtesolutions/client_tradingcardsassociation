# /SADMIN design

Staff console for NTCA. Visual language is a dark operations dashboard (XCHAIN-like: near-black canvas, charcoal cards, one bright accent, sparse orange highlights). **Do not commit the inspiration screenshot.**

This document is the source of truth for tokens, type, spacing, and components. Implement against **real queries**. Empty queues show an empty state, never invented counts.

## Surfaces

| Route | Purpose |
|---|---|
| `/SADMIN` | Login (emailed code) → dashboard |
| `/SADMIN/dashboard` | KPI row + latest lists |
| `/SADMIN/queue/events` | `mkt.EventSubmission` where `Status = 'PENDING_REVIEW'` |
| `/SADMIN/queue/edits` | `dir.ShopEditRequest` where `Status = 'PENDING_REVIEW'` |
| `/SADMIN/shops` | `dir.Shop` list (filter by state / claim / listing status) |

Auth: ASP.NET cookie authentication, signed + encrypted (`Ntca.Sadmin` scheme). Separate cookie from `/owner`. Roles: `SUPERADMIN`, `ADMIN`, `MODERATOR`, `EDITOR`. Seeded QA user: `egarrison@wte.net`.

## Tokens

| Token | Value | Use |
|---|---|---|
| `--sadmin-bg` | `#0b0d10` | Page |
| `--sadmin-surface` | `#14181d` | Cards, top bar |
| `--sadmin-surface-2` | `#1b2128` | Nested wells, table header |
| `--sadmin-border` | `#2a323c` | Hairlines |
| `--sadmin-text` | `#e8eef4` | Body |
| `--sadmin-muted` | `#8b97a5` | Meta, empty copy |
| `--sadmin-accent` | `#3d8bfd` | Primary actions, hero chart |
| `--sadmin-accent-2` | `#f59e3b` | Daily-bar highlight, warnings |
| `--sadmin-ok` | `#34d399` | Approved / live |
| `--sadmin-danger` | `#f87171` | Reject / overdue |
| `--sadmin-radius` | `16px` | Cards (20px on hero) |
| `--sadmin-pad` | `20px` | Card padding |
| `--sadmin-gap` | `16px` | Grid gap |
| `--sadmin-font` | `"IBM Plex Sans", "Segoe UI", sans-serif` | UI |
| `--sadmin-mono` | `"IBM Plex Mono", ui-monospace, monospace` | IDs, counts |

Contrast: text on `--sadmin-bg` / `--sadmin-surface` meets WCAG AA for body (≥ 4.5:1). Accent blue on charcoal is for large UI chrome (≥ 3:1). Do not put small muted text on the blue hero.

## Type

- Page title: 22 / 28, weight 600
- KPI value: 28 / 32, weight 650, tabular figures
- KPI label: 12 / 16, uppercase, letter-spacing 0.04em, muted
- Body: 14 / 22
- Table: 13 / 20
- Chip: 11 / 16, weight 600, uppercase

## Layout

1. **Top bar** (56px): mark + “NTCA Staff”, search (shops / shows / users — later), account chip (email + role).
2. **KPI row** (4–6 cards): shops, upcoming shows, pending event submissions, pending owner edits, preview signups (7d), manufacturers. Each card is one SQL `COUNT`. Zero is valid.
3. **Hero** (optional when a series exists): 7D / 1M / 1Y / All toggle over a real series (signups/day or shows/week). If the series is empty, hide the chart and show the empty copy — do not draw dummy bars.
4. **Two columns**: approval queues (events, owner edits) and latest lists (new shops, upcoming featured shows).
5. **Status chips**: `PENDING_REVIEW` (accent), `APPROVED` (ok), `REJECTED` (danger), `CLAIMED` / `UNCLAIMED` (muted/ok).

## Components

- **Card**: charcoal, 16–20px radius, 1px border, no drop shadow.
- **Queue row**: title, submitter, relative time, Approve / Reject. Approve writes the domain row (`mkt.ShopEvent` or applies `ProposedJson` onto `dir.Shop`) then sets status.
- **Empty state**: one muted sentence + link to the public form. Example: “No event submissions waiting.”
- **Shops table**: name, city/state, claim, listing, tier, source system.

## Accessibility

- Focus rings: 2px `--sadmin-accent` offset on dark.
- Do not rely on color alone for status — include the word on the chip.
- Login code field: `autocomplete="one-time-code"`, 6 digits, numeric.

## Out of scope for this pass

Maps, billing, SocketLabs campaign composer, karma moderation, full user admin. Those stay documented routes, not fake tiles.
