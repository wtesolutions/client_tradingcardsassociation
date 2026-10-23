# NTCA Shop Directory — Design and Implementation Specification

Version 1.1 · October 23, 2026

Companion design: `NTCA_Directory_Design.png`

## Purpose and reference

Help collectors find relevant shops, compare specialties and services, and open a shop profile or directions. Follow the approved NTCA homepage: substantial navy header with complete emblem, gold actions, white content, restrained borders, and navy footer.

This specification is authoritative for behavior. The generated image is visual direction, not exact production data or a pixel-perfect functional specification. All shops, distances, counts, hours, and map placements are illustrative. The map is not geographic evidence. Use the original NTCA logo artwork in production.

The current live-site navigation could not be verified because the supplied URL redirected to the invitation teaser. Mockup labels are provisional; preserve the real menu labels and destinations once supplied.

## Page hierarchy

1. Shared NTCA header and membership CTA.
2. Compact navy search masthead.
3. Category navigation.
4. Active-filter chips and result summary.
5. Filter sidebar and spacious two-column shop-card grid.
6. Pagination.
7. Shared navy footer.

Do not repeat the homepage promotional hero photograph here. Keep the search and first results visible early.

## Visual foundation

| Property | Recommended specification |
| --- | --- |
| Header/footer | Navy `#10182B` |
| Primary action | Gold `#E5B956`, navy text |
| Main surface | White `#FFFFFF` |
| Page background | `#F7F8FA` |
| Headings | `#11192D` |
| Secondary text | `#536077` |
| Borders | `#DCE1E8` |
| Type | Inter with Arial/sans-serif fallback |
| Radius | 6–8px |
| Main container | Up to 1600px; 24–40px desktop gutters |
| Header | Approximately 120px, emblem 88–96px with clearance |
| Search title | 36–44px desktop; 28–32px mobile |
| Body/control text | 16px; metadata 13–14px minimum |

Values are implementation recommendations inferred from the selected visual direction.

## Search masthead

Headline: **Find your next favorite card shop.**

Desktop search: keyword field, location field, gold Search button in one row. Controls 48–52px tall. Keyword label: “Shop name or specialty.” Location label: “City or ZIP code”; an example populated state may show Raleigh, NC.

- Keyword searches shop name, supported specialties, and concise descriptive content.
- Location selection resolves to a geographic center and a display label.
- An explicit Use my location action may request location permission; never request it on load.
- Enter submits. Retain values after submission and when navigating back from a shop.
- Use real labels, not placeholder-only accessibility.
- Show location suggestions with keyboard navigation and a clear selected state.
- Ambiguous locations require selection; invalid locations produce an inline message rather than silently choosing another city.
- Permit nationwide keyword browsing without location; then disable radius/open-distance assumptions and use relevance sorting.

## Category and filter model

Category navigation: All cards, Sports cards, Pokémon, Magic: The Gathering, Other TCGs. Treat category tabs and specialty checkboxes as two views of the SAME filter state.

If Pokémon is selected, highlight Pokémon rather than All cards. If multiple specialties are selected, replace the category indication with a clear multi-selection summary. The generated mockup's All cards underline with a Pokémon chip is not an intended production state.

| Filter | Control | Behavior |
| --- | --- | --- |
| Distance | Select: 5, 10, 25, 50, 100 miles; Any distance | Requires resolved location |
| Open now | Switch | Include shops with known current opening status only |
| Specialties | Multi-select checkboxes | Sports cards, Pokémon, Magic, Other TCGs |
| Services | Multi-select checkboxes | Buy, Sell, Trade, Grading submissions, In-store play |
| Shop type | Single select | Physical shops, Online only, All |

Use OR within specialties and AND across filter groups. For services, use AND: the result must provide every selected service. Split Buy/Sell/Trade into explicit underlying flags even if a compact combined display label is used.

Default physical-shop discovery to 25 miles only when a location has been selected. Online-only mode removes distance and Open now constraints, removes map pins, and offers relevance/name sorting. Do not locate online-only stores at a private address.

### Applying filters

Desktop: changes update results with a short debounce; cancel superseded requests. Keep typed search edits pending until Search or Enter. Mobile: filters use a full-height sheet with draft selections and Apply filters / Reset actions. Closing without Apply discards draft changes.

Active chips must mirror the actual query. A chip remove button removes only that constraint. Clear all resets optional filters but retains keyword and selected location. A separate Reset search action may clear everything.

Filter-button counts include active optional constraints, not the keyword/location. Default radius is not counted; a changed radius is. Counts, checkboxes, tabs, and chips must remain consistent.

## Results and layout

Use a 220–240px desktop filter sidebar and a flexible two-column results grid with 20–24px gaps. Cards show landscape photos above metadata. Below approximately 1100px, move filters into a drawer if needed to maintain comfortable card widths. Below 768px use one full-width card column.

The directory has NO map, map pins, numbered photo markers, map toggle, or map-area search. Keep location input, radius filtering, distance labels, and distance sorting. Maps and directions may remain on the separate shop detail page.

Toolbar: total result count, location, and sort dropdown. Pagination remains below results even when it is outside the PNG crop.

Sort options: relevance, distance (when geocoded), and shop name. Default to relevance for a keyword query and distance for location-only discovery. Use stable shop ID as a tie-breaker. Do not invent popularity scores.

Use 12 results per page. Paginate with real links and preserve all query parameters. Counts describe the total matches, not just rendered rows. Scroll to the result heading after explicit pagination and announce the new range accessibly.

## Shop card anatomy

- Full-width landscape store photo with reserved 16:9 aspect ratio.
- Optional Featured or Destination ribbon at the photo’s upper left.
- Shop title as the primary link.
- City/state and distance when available.
- Compact specialty labels.
- Opening status when known.
- Owner-claimed badge only when backed by a claim record.
- One short description, at most two lines.
- Save button and View shop link.

The whole visual card may feel interactive, but avoid nested anchors or a link wrapper around the save button. Keep button and link semantics separate. Missing photos use a branded neutral placeholder. Missing hours say “Hours not provided”; never infer Closed. Ratings are omitted until a real review system and provenance exist.

Owner claimed is an account-management status, not certification or NTCA endorsement. Prefer a neutral owner/location icon over a checkmark shield.

## Image ribbons: Featured and Destination

Use one prominent ribbon per photo, inset approximately 12px from the top-left. Height 28–32px, 12–13px semibold uppercase text, modest notched edge. Keep the photo-top-right save action separate. Ribbons are real HTML text, not baked into uploaded images.

| Ribbon | Visual style | Recommended meaning |
| --- | --- | --- |
| Featured | Gold background, navy text | Time-limited promotional placement selected by NTCA |
| Destination | Navy background, white text, small gold compass | Editorial designation for a shop worth a dedicated visit |

These meanings are proposed product policy, not existing NTCA policy. Featured may be editorial or paid; paid placement must also show a visible Sponsored disclosure near the shop title, as illustrated. Do not imply all Featured shops are paid.

Destination is not a purchasable verification seal. Suggested review criteria: distinctive inventory, substantial in-store experience, established events, and practical visitor information. Store the reason, reviewer, review date, and expiry/review-due date. Publish a concise explanation of the designation once approved.

If both apply, show Featured on the photo and Destination as a secondary text label below; avoid stacking multiple ribbons across the image. The shop detail page should show the same active designations and their explanations. Owner claimed remains independent and must not imply quality certification.

Paid placements must match the user's filters. Place them in a clearly labeled promoted area before the organically sorted grid, or otherwise visibly distinguish them from Distance ordering. Do not silently intermix paid rank with a nearest-first promise. Count shops uniquely if a promoted card also appears organically; preferably suppress duplicates within a page.

Additional fields: promotionType (editorial/paid), promotionStartsAt, promotionEndsAt, destinationStatus, destinationReason, destinationReviewedAt. Expired promotions remove the ribbon automatically. Use an explicit precedence rule when more than one designation is active.

## Mobile

Below 768px, stack keyword, location, and Search. Show category scrolling, Filters and Sort controls, active chips, count, and vertical photo cards. No permanent left sidebar.

Reserve space below results so bottom navigation cannot obscure the last card or pagination. No floating map action is shown. The filters sheet supports keyboard focus containment, Escape/close, and focus restoration. Mobile bottom navigation follows the shared NTCA pattern.

## URL state and suggested contract

Suggested route: `/shops`.

Example query shape: `q`, `location`, `lat`, `lng`, `radiusMiles`, `specialty`, `service`, `shopType`, `openNow`, `sort`, `page`.

Use repeated values for multi-select filters and canonical ordering. Browser Back restores search, filters, page and scroll position. Avoid persisting a precise user-derived coordinate in long-term analytics.

Suggested response fields: `items`, `total`, `page`, `pageSize`, `resolvedLocation`, `appliedFilters`, `facetCounts`, `generatedAt`.

Shop summary: `id`, `slug`, `name`, `city`, `region`, `coordinates`, `distanceMiles`, `photo`, `specialties`, `services`, `shopType`, `claimStatus`, `timezone`, `openingStatus`, `nextHoursChange`.

Validate filters server-side, cap page sizes, cancel stale client requests, and compute opening status in each shop's timezone using exceptions/holiday hours.

## States and measurement

Support initial search, loading, no results, invalid location, denied geolocation, partial data, network error, and saved/unsaved states. No-results actions: increase radius, remove filters, browse all shops.

Track search submission, filter changes, result opens, saves, and direction clicks with shop ID and query categories. Label paid placement as Sponsored and separate it from organic ordering. Never fabricate counts or trust badges.

## Acceptance checklist

- Search/filter state is shareable and restored by Back.
- Category selection agrees with chips and checkboxes.
- Online-only mode does not display distance, map pin, or physical-hours assumptions.
- No map controls or numbered map markers remain in the directory.
- Featured, Sponsored, Destination, and Owner claimed have distinct meanings.
- Filter panel and cards work with keyboard and touch.
- Search fields remain at least 16px on mobile; targets aim for 44px.
- No fixed element hides content at 320px width or text zoom.
- Real logo and verified production data replace generated content.
- Full navigation is mapped from the actual site before launch.
