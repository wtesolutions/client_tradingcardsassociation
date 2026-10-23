# NTCA /SADMIN design spec v1.0

Date: 2026-10-23  
Author: Eric (staff workspace)

## Shell

- Solid navy masthead (`#10182B`) 96–112px with the original NTCA emblem 72–88px and the full organization name in white.
- Right side: “Staff workspace”, outlined “Public site ↗”, staff menu with Sign out.
- **No** public Join CTA in admin.
- Left sidebar 224–240px, background `#F4F5F6`, fine right rule.
- Small uppercase muted section labels:
  - **WORKSPACE** — Dashboard, Event queue, Edit queue, Drafts
  - **DIRECTORY** — Shops, Manufacturers
  - **INSIGHTS** — Onboarding, Funnel, AI Visitors, Google, Social catalog
- Keep every existing href. Put any other existing SADMIN pages (onboarding queue, people, gallery, storage, etc.) into sensible groups. Never remove pages.
- Active item: navy text, background `#FBF5E6`, 3–4px gold left rule, `aria-current`.
- Navy footer in document flow (not fixed).
- Workspace canvas `#F7F8FA`, surfaces white, border `#DCE1E8`, muted `#536077`, link `#214E85`, gold `#E5B956` (hover `#D6A744`) with **dark** text, success `#206344`, danger `#A92C38`.
- Single 6px radius, Inter font, tabular numerals for counts.
- Status words: “Unclaimed” / “Active” — not ALL CAPS.
- Scoped `.ntca-admin` CSS variables so public pages do not regress.
- No pure black, neon, gradients, fake charts/trends/avatars.
- Prefer hairline rules over a card per section.

## Operations dashboard

- Breadcrumb Workspace / Dashboard.
- H1 “Operations”, subtitle “Manage the directory. Support the hobby.”
- **Read-only** “Site mode: Preview/Live” status plus a separate “Manage mode” action that opens a focused confirm workflow (server auth + audit). The status itself must not toggle.
- Inventory strip: Active shops, Upcoming shows, Manufacturers (real counts, link to pages).
- Needs attention (left): Event submissions, Owner edits, Draft posts.
  - “No pending items” only after a successful query.
  - Query failure shows an error, not “Up to date”.
  - Draft posts links to Drafts (“Review draft →” gold when count is 1).
- Activity (right), title **Activity**, last 7 days — **not** “Collector activity” (bots are included): Shop impressions, views, clicks, Signups, AI visits, AI inquiries.
  - Missing data: “Not available”, not 0.
- Top shops last 7 days: Shop, Location, count (label the column by what the existing query actually measures), Stats →.
- All numbers from real queries. Never hardcode mock values.

## Shops

- One filter toolbar with labels, Enter/Apply filters, Clear filters.
- State in the URL; Back works.
- Name/city and state filters.
- Claim / listing / source / designation filters only where the backend supports them.
- Semantic table: Shop (name = editor link), City/state, Claim, Listing, Designation, Impressions (7 days, right aligned), Actions (existing Links menu, labeled).
- Source available in inspector; “Unknown source” if empty. Never modify source strings.
- Imported `· dest` source suffix shows as “Destination — imported, review needed” in table and inspector. Never auto-publish as a designation.
- Checkboxes only if a real bulk operation exists (none today — omit).
- Pagination with real totals/range, page size 25/50/100.
- Row selection (separate from checkbox) opens a read-only 300–340px inspector: name, location, claim, listing, raw source, designation review state, neutral photo placeholder if no real photo, “Preview public listing ↗”, gold “Open editor →”.
- `<1200px` inspector becomes a drawer (Esc closes, focus returns to row).
- `<900px` nav becomes a drawer.
- Mobile filters stack; table scroll only inside its own container.

## Manufacturers

- Same table pattern.
- Description: “Maintain the brands and historical makers in the catalog.”
- Keep Name/slug filter and Public action.
- Columns: Name, Slug, Logo thumbnail or “Missing logo”, Description excerpt or “No description”, Actions.
- Optional Missing logo / Missing description filters.
- Inspector.

## Onboarding (funnel)

- Heading “Signup & onboarding”.
- Period default Last 30 days.
- Note that onboarding is skippable.
- Keep “No raw IPs”.
- Two compact strips: Registration (Started, Registered, Confirmed), Onboarding (Started, Completed, Skipped).
- Two tables Step / Entered / Completed with readable labels (register = Registration, thanks = Thank-you page, confirm = Confirmation; who = About you, zip = ZIP code, interests, experience, travel — match the actual questions).
- Do not invent funnel drop-off or rates. Show a “Definition pending” note.
- Entered = 0 shows an em dash.

## Other SADMIN pages

Apply shell / table / form / empty / error states without changing behavior.

## Accessibility

Keyboard, visible focus (3px `#2463c5`), labels, 44px touch targets, 200% zoom, reduced motion.
