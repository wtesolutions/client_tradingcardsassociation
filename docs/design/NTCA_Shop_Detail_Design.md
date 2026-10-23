# NTCA Shop Detail — Design and Implementation Specification

Version 1.0 · October 23, 2026

Companion design: `NTCA_Shop_Detail_Design.png`

## Purpose

Help a collector decide whether a shop matches their interests, plan a visit, contact the store, and discover its events. Maintain continuity with the NTCA homepage and directory: navy header/footer, full-size original emblem, white surfaces, navy headings, and gold primary actions.

The illustration uses fictional Oak City Collectibles and sample operating hours/events. Replace every sample fact with supplied shop data. Generated photographs and map positions are conceptual. Navigation labels remain provisional pending the actual live-site menu.

## Page hierarchy

1. Shared NTCA header.
2. Breadcrumb and return-to-results link.
3. Shop identity, specialties, location, opening status, save/share.
4. Photo gallery with visit/contact panel alongside on desktop.
5. Section navigation: Overview, Photos, Events, Details.
6. About, services, events, and factual shop details.
7. Claim/manage and correction links.
8. Shared footer; mobile visit actions as a sticky action bar.

Unlike the homepage, do not put the association slogan ahead of the shop name. The shop name is this page's H1.

## Visual rules

| Element | Recommended treatment |
| --- | --- |
| Header/footer | `#10182B`, white text, gold accents |
| Primary CTA | Gold `#E5B956`, navy text |
| Text | Primary `#11192D`; secondary `#536077` |
| Surfaces | White; secondary canvas `#F7F8FA` |
| Borders | `#DCE1E8`, 1px |
| Typeface | Inter; Arial/sans-serif fallback |
| Corners | 6–8px |
| Desktop H1 | 40–48px, weight 750–800 |
| Mobile H1 | 28–32px, flexible wrapping |
| Body | 16px, line-height 1.5 |
| Container | Up to 1440px; 24–40px desktop gutters |

Use the original emblem; do not trace the AI-rendered version. Maintain 88–96px desktop emblem height inside the substantial padded header.

## Identity area

Breadcrumb: Find shops > State/region > City. Back to results preserves the originating query and scroll position; a direct visit falls back to the directory.

Display shop name, specialty summary, city/state, and opening status. Use Save and Share actions with labels. Support long names without truncating the H1.

Only show Owner claimed when there is an approved claim record. Explain it as “This listing is managed by the business owner.” Do not imply independent certification. Do not display Member, Verified, or ratings without corresponding data and a defined policy.

## Gallery

Desktop gallery: one large landscape image with two smaller images stacked beside it, aligned to equal total height. A View all photos button includes the actual count. The visit panel occupies the adjacent right column.

Mobile: one landscape image, pagination count, and swipe/button navigation. Allow all photos through a full-screen viewer. Provide previous/next controls and meaningful alt text; swipe is not the only navigation mechanism.

Lightbox supports Escape, focus containment/restoration, keyboard arrows, image captions, and a clearly labeled Close button. Preserve aspect ratios and avoid cropping important signage. Reserve dimensions to prevent layout shifts. Load the lead image eagerly and remaining images lazily.

Zero photos: branded placeholder without a misleading photo count. One photo: no carousel indicators. Never show fabricated store photographs on a live business listing.

## Plan your visit panel

Place the panel beside the gallery on desktop and move its actions immediately after the lead photo on mobile. Desktop may use sticky positioning only if the panel fits the viewport; disable sticky behavior for tall panels.

Action hierarchy:

1. **Get directions**: gold, full width.
2. **Visit website** and **Call shop**: outline buttons.
3. Address, weekly hours, small map.
4. Manage/claim listing and Suggest an edit.

Show the actual public street address and copy-address action when available. The mockup's “Address shown in live listing” is an illustration placeholder and must not ship.

Use a destination-specific directions URL. On mobile, use available map-app handling; do not claim the user has arrived or checked in merely because they opened directions. Use `tel:` for a valid public phone number. Use normalized HTTP(S) website links. Label external navigation appropriately.

When an action lacks required data, omit it and show an informative fallback. With only a city and no reliable destination, do not offer misleading precise directions. Online-only shops replace Plan your visit with Shop online, omit physical map and driving directions, and display supported contact channels.

## Opening hours

Store timezone-aware weekly intervals and dated exceptions, including closed days, multiple daily intervals, overnight hours, and appointment-only periods.

- Compute Open now and the next opening/closing change from shop-local time.
- Show today's status near the name; expand to the full weekly schedule.
- Highlight today with weight or a label, not color alone.
- Show holiday exceptions when supplied; a generic holiday note is supplemental.
- Unknown hours: “Hours not provided.” Unknown status is not Closed.
- Appointment-only: explicit label and contact/booking action if available.
- Show “Hours updated [date]” when provenance is available.

The sample week in the image is demonstration data only.

## Overview and services

Suggested introductory heading: **A local home for collectors.** Use owner-supplied plain-language copy explaining inventory, specialties, and the in-store experience. Keep the summary around 60–120 words before expansion when content is long.

Service chips are informational labels, not fake buttons. Render services only when confirmed. Suggested fields: buys cards, sells cards, trades cards, grading submissions, in-store play, tournaments, accessories, sealed product, and singles.

Grading submissions means the store facilitates submissions; it does not mean the shop grades or authenticates cards itself. Show named providers only when supplied and confirmed.

## Section navigation

Overview, Photos, Events, and Details are anchors into the same document, not necessarily separate routes. Indicate the current section and use scroll offsets so sticky elements do not cover headings. Preserve section URLs with fragments when useful.

Keep core content available without requiring tab interactions. Do not hide important address or hours content in an inaccessible tab panel. Mobile section navigation may scroll horizontally.

## Events

Show upcoming public events associated with the shop, sorted by start time, with real dates, timezone, title, short description, and View event link. Include admission/registration details when available.

- Use the shared red month badge and navy day number.
- Render weekday and date from the same timestamp.
- Exclude canceled events from the default list or display an explicit cancellation state.
- Include recurrence only with actual recurrence data.
- Show two or three events, then View all events.
- Empty state: “No upcoming events listed.” Keep the rest of the page useful.

Do not publish the generated dates, event names, or schedules as facts.

## Details, map, and trust

Details may include confirmed accessibility, parking, payment methods, public contact links, and amenities. Missing facts should be omitted or labeled unknown; never infer them from photos.

Show a map only for public, geocoded physical locations. Keep an address and directions alternative so the page works without the map. A map error should not hide contact information.

Claimed listing: eligible owner sees Manage listing after authorization. Unclaimed listing: Claim this shop leads to an ownership-verification flow. A public visitor cannot edit listing data merely by clicking the link.

Suggest an edit collects the proposed correction and source, then routes it for review. Rate-limit submissions and preserve an audit trail. Do not silently replace owner or directory data with anonymous input.

## Save and share

Save provides immediate state feedback. If sign-in is required for cross-device saves, preserve the intended save through authentication; local guest saves may be supported as a product choice.

Share uses the public canonical shop URL, not the visitor's precise location or search coordinates. Prefer the native share interface when available and provide Copy link as a fallback. Announce successful copying accessibly.

## Responsive behavior

At 1200px and above: flexible main column and approximately 320px visit panel, separated by 24px. At intermediate widths: reduce gallery complexity or move the panel below the identity area; do not compress buttons or text excessively.

Below 768px: identity, gallery, visit actions, section navigation, overview, services, events, details/hours, and footer in one column. Keep Save and Share discoverable even if they need a compact action row.

Mobile sticky bottom bar prioritizes Directions and Call for physical shops. Reserve its height plus safe-area padding. Do not show a second persistent Shops/Events/Saved bar at the same time. The full site navigation remains in the header menu.

## Suggested data contract

Suggested route: `/shops/{slug}` with a stable internal shop ID. Redirect old slugs to the current canonical URL.

Core fields:

- Identity: id, slug, name, description, shopType, specialties, services.
- Contact: publicAddress, city, region, postalCode, country, coordinates, phone, website.
- Hours: timezone, weeklyIntervals, datedExceptions, appointmentOnly, updatedAt.
- Media: id, URL, width, height, altText, caption, displayOrder, attribution.
- Ownership: claimStatus, claimApprovedAt, permitted management relationship.
- Events: event IDs with start/end timestamps, timezone, status, venue relationship.
- Metadata: listingUpdatedAt, contentSource, publicationStatus.

Separate public DTOs from ownership-verification documents and private account data. Only authorized owners/admins can change public fields. Validate external URLs and sanitize descriptive content.

## Loading and error states

Reserve gallery and panel dimensions during loading. Handle not found, unpublished listing, temporarily unavailable data, broken images, absent phone/site/address, no events, unknown hours, and failed save operations explicitly.

Do not hide the whole profile if an optional map, photo, or event feed fails. For moved or closed shops, display a clear status banner and update visit actions accordingly.

## Measurement and implementation checks

Track shop views, gallery opens, website clicks, phone clicks, directions clicks, saves, shares, event opens, and correction submissions using shop ID. Distinguish a click from a completed visit, call, or purchase.

- Preserve directory return state.
- Verify all opening-status logic against timezone and exception cases.
- Check long names, zero photos, zero events, and online-only shops.
- Keep 44px touch targets, visible keyboard focus, meaningful headings, and sufficient contrast.
- Check 320px width and 200% text zoom without hidden actions.
- Confirm sticky actions do not cover final content or footer links.
- Replace every illustrative fact before production.
- Map confirmed live navigation before launch.

## Developer directive

Build this page as reusable components aligned with the NTCA directory and homepage. Prioritize the shop identity and visit/contact actions, then gallery, specialties, services, and events. Use real text, semantic HTML, responsive images, and data-backed statuses. Treat the PNG as visual direction and this specification as the interaction contract. Do not reproduce generated sample data, map geography, or logo artifacts as production assets.
