# National Card Show Calendar

First-class calendar for NTCA. The static `legacy-site/shows.html` list (~28 shows) is the seed; the live site serves these routes from SQL Server (`mkt.ShopEvent` + `mkt.Venue` + `mkt.Promoter`).

Canonical host: `https://tradingcardsassociation.com`

`/calendar` and `/shows` are the same hub (month / list / map). `/events` stays the association/community calendar (`cms.Event`).

## Public routes

| Route | View | Source |
|---|---|---|
| `/calendar` | Hub: month, list, and map. Default upcoming | `mkt.vw_CalendarUpcoming` |
| `/shows` | Alias of `/calendar` | same |
| `/shows/{state}` | State hub (two-letter code, lowercased: `/shows/tx`) | `seo.vw_SitemapShowStates` |
| `/shows/{state}/{city}` | City hub | `seo.vw_SitemapShowCities` |
| `/shows/{state}/{city}/{slug}-{yyyy-mm}` | Per-show edition | `seo.vw_SitemapShows` / `vw_CalendarUpcoming.LocPath` |
| `/shows/series/{seriesKey}` | Recurring series (every edition) | `mkt.ShopEvent.RecurrenceSeries` |
| `/promoters/{slug}` | Promoter page | `seo.vw_SitemapPromoters` |
| `/calendar.ics` | National ICS subscription | published CARD_SHOW rows |
| `/shows/{state}.ics` | Per-state ICS | filter `StateCode` |
| `/calendar.ics?scope=&state=&city=&from=&to=&q=&featured=` | Filtered ICS | same filters as the hub |
| `/shows/{state}/{city}/{slug}.ics` | Single-show VEVENT | `vw_CalendarUpcoming.IcsPath` |

Hub query filters (HTML, JSON, and ICS):

| Param | Meaning |
|---|---|
| `view` | `month` \| `list` \| `map` |
| `state` | Two-letter code |
| `city` | City name |
| `zip` + `radius` | Miles from ZIP (app geocodes; SQL stores `Venue.Latitude/Longitude`, no geography type) |
| `near` | Browser geolocation (lat,lng) + radius |
| `from` / `to` | Date range on `StartsAt` / `EndsAt` |
| `scope` | `LOCAL` \| `REGIONAL` \| `NATIONAL` |
| `category` | `SPORTS` / `TCG` / `NONSPORT` / `POKEMON` / … (`FocusTags`) |
| `manufacturer` | Manufacturer slug (`mkt.EventManufacturer`) |
| `featured` | `1` = paid/featured placement (`IsFeatured` or `CALENDAR_FEATURED` slot) |

## ICS and “Add to calendar”

- **Subscribe:** `/calendar.ics` (all published/postponed shows). Per-state and filtered URLs above. Personal feed later: `/u/{token}/shows.ics` from `core.UserCalendarToken` + `mkt.EventFollow`.
- **Per show:** `.ics` download plus Google (`calendar.google.com/calendar/render?action=TEMPLATE&…`) and Outlook (`outlook.live.com/calendar/0/deeplink/compose?…`) links built from `StartsAt`, `EndsAt`, `TimeZoneId`, venue, and loc.
- `TimeZoneId` is stored on `mkt.Venue` and copied onto `mkt.ShopEvent`. Seeded editions use date-only local midnight / 23:59 because `shows.html` has no door times. `HoursText` is filled when a promoter supplies hours.

Canceled / postponed: `Status` is `CANCELED` or `POSTPONED` (`PostponedUntil`, `CanceledReason`). ICS `STATUS:CANCELLED` and Event JSON-LD `eventStatus` follow that column.

## JSON API (MAUI / web)

`GET /api/v1/calendar` — same filters as the hub.

Returns rows from `mkt.vw_CalendarUpcoming` (id, slug, title, start/end, tz, scope, status, featured, tags, tables, admission, hours, venue + lat/lng, promoter, loc, ics). Radius filtering happens in the API (haversine on venue lat/lng).

`GET /api/v1/calendar/{publicId}` — one edition.

`POST /api/v1/calendar/submissions` — native “Submit a show” (promoter or shop). Lands in `mkt.EventSubmission` as `PENDING_REVIEW`. SADMIN moderates; approval writes `mkt.ShopEvent` (`SourceType = SUBMIT`) and sets `EventSubmission.ShopEventId`.

Shop owners can also file `mkt.EventExhibitor` (“We’ll be at…”) against a published show.

## Event JSON-LD

On every edition page (`/shows/{state}/{city}/{slug}`):

- `@type: Event` — name, startDate/endDate (with offset from `TimeZoneId`), `eventStatus`, `eventAttendanceMode: OfflineEventAttendanceMode`
- `location: Place` from `mkt.Venue` (name, PostalAddress, geo)
- `organizer: Organization` from `mkt.Promoter`
- `offers` from `AdmissionMinCents` / `AdmissionText`
- `performer` from `mkt.EventGuest` when present
- State/city hubs: `ItemList` + `BreadcrumbList`

## Featured / paid placement

- `mkt.PromoterTier`: FREE / FEATURED / PREMIUM (prices TBD)
- `mkt.ShopEvent.IsFeatured`
- `feed.RotationSlot`: `HOME_UPCOMING_SHOWS`, `CALENDAR_FEATURED`, `STATE_HUB_FEATURED`
- `feed.ShowFeedEntry` reasons: `NEW_SHOW`, `THIS_WEEKEND`, `FEATURED`, `NATIONAL`

National-scope seeded shows start in `CALENDAR_FEATURED`.

## Schema (supporting)

`mkt.ShopEvent` (calendar row): `ShopId` **nullable** (national shows need not belong to a shop), `StartsAt`/`EndsAt`, `TimeZoneId`, `VenueId`, `PromoterId`, `Status` (`DRAFT`/`PENDING_REVIEW`/`PUBLISHED`/`CANCELED`/`POSTPONED`), `RecurrenceSeries`, `Scope`, `IsFeatured`, `SourceType` + `SourceSystem`. Owner check: shop **or** promoter.

Also: `mkt.Promoter`, `mkt.PromoterClaimRequest`, `mkt.Venue` (tz per venue), `mkt.EventExhibitor`, `mkt.EventGuest`, `mkt.EventManufacturer`, `mkt.EventFollow`, `mkt.EventSubmission`, `mkt.vw_CalendarUpcoming` (index `IX_ShopEvent_Calendar` on `StartsAt, StateCode`).

## SADMIN

Moderation queue: `mkt.EventSubmission` (`PENDING_REVIEW`) and promoter claims. Editors can cancel/postpone an edition, clone the next month of a `RecurrenceSeries`, confirm exhibitor claims, and toggle `IsFeatured`.

## Seed

`db/seed/SEED_20_events.sql` loads 7 association rows into `cms.Event` and the 28 `shows.html` listings into `mkt.ShopEvent` (23 promoters, 27 venues; 4 national / 14 regional / 10 local). Regenerated by `python3 db/tools/generate_seed.py`.
