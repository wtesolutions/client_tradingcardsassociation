# Shop data review (manual research 2026-10-23)

Staff flags from the first-10-shop pass. **Do not change names, phones, or addresses** from this list until an owner or SADMIN confirms.

The original research spreadsheet did not land on the agent VM. `SEED_17` applies only cells verified from official public pages and published maps listings on **2026-10-23**. Re-run is safe.

Run `db/ssms/SEED_17_shop_enrichment_manual_SSMS.sql` after `06_shop_social_SSMS.sql`.

SADMIN flags live in `dir.Shop.ImportNotes` (`DATA_REVIEW 2026-10-23:`, `HOURS 2026-10-23:`, `GEO 2026-10-23:`). There is no separate review table.

## Applied (verified)

| Shop | Slug | What landed |
|---|---|---|
| Boxseat Collectibles | `boxseat-collectibles-phoenix-az` | Website `https://boxseatcollectibles.com`; Facebook, Instagram `@boxseataz`, YouTube channel, X `@boxseataz`, Pinterest. Short homepage description if empty. Lat/lng `33.331530,-111.978202` (`GEO Source=manual-maps-2026-10-23`). **Hours left empty.** |
| Burbank Sportscards | `burbank-sportscards-burbank-ca` | Website `https://www.burbankcards.com`; Facebook, Instagram `@burbankcards`, TikTok, YouTube `/c/BurbankSportscards`, X `@BurbankCards`, email `info@burbankcards.com`. Hours Mon–Sat 10–6, Sun 11–4 from the official help page (`Source=manual`; owner-confirm). Lat/lng `34.180800,-118.309000`. Existing top-100 description is not overwritten. |
| GamesandCards.com | `gamesandcards-com-eagle-rock-ca` | Website `https://gacsuperstore.com`; Facebook + Instagram `@gacsuperstore`; email `info@gamesandcards.com`. Lat/lng `34.139093,-118.204263`. Hours skipped (listings disagree). |
| Bakersfield.cards | `bakersfield-cards-bakersfield-ca` | Beckett marketplace `https://marketplace.beckett.com/Bakersfield_636` as the only-site **Website** (Beckett is not an eBay host). Hours M–F 10–6, Sat 10–3, Sun closed (`Source=manual`; owner-confirm). Lat/lng `35.377086,-119.015543`. GASC socials not applied. |
| Wonder Water | `wonder-water-sports-cards-comics-gaming-clearwater-fl` | Beckett marketplace `https://marketplace.beckett.com/wonderwater_173` as the only-site **Website**. Short description `Strange Name...Cool collectibles!` if empty. Lat/lng `28.036522,-82.737538`. Hours skipped (listings disagree: 11–7 vs 12–7 weekdays). |

`dir.ShopSocial` rows use `Source='manual'` and `Verified=1`. Legacy `WebsiteUrl` / `FacebookUrl` / `InstagramHandle` fill only when empty. Beckett marketplace URLs are stored as `WEBSITE` because there is one website slot and `EBAY` only allows `ebay.com`.

`dir.ShopHours` has no Source column; the `HOURS 2026-10-23: Source=manual; needs owner confirmation.` token is on `ImportNotes`.

## Skipped (do-not-apply / unverified)

| Item | Why |
|---|---|
| Bakersfield GASC socials | Marked do-not-apply / unverified |
| Simi Sportscards | Marked do-not-apply / unverified (possible match for Simicards — see below) |
| Boxseat hours | Leave empty even though the About page lists hours |
| GamesandCards hours | Official site has none; third-party hours conflict |
| Wonder Water hours | Third-party hours conflict |
| Birmingham Beckett URL | `marketplace.beckett.com/cfal_1295` is **Sports Nuts Collectibles**, not Birmingham Sports Cards |
| Google ratings / review counts | Do not store permanently in seed. Omitted from `dir.GooglePlaceCache` (batch refresh writes them with a 30-day expiry) |

## Needs SADMIN review

| Shop | Slug | Flag |
|---|---|---|
| Birmingham Sports Cards | `birmingham-sports-cards-alabaster-al` | Google Maps may show **Sports Nuts Collectibles** at this listing, phone **205-406-5080**. Directory phone stays **205-369-1973**. Do not apply the Sports Nuts Beckett URL. |
| Cards And Stuff | `cards-and-stuff-gilbert-az` | Review identity / whether the Gilbert row is still a live shop. |
| ChicagoCards.com | `chicagocards-com-pittsburg-ca` | No Google listing; site down; possibly closed. |
| Mojo Sportscards | `mojo-sportscards-thousand-oaks-ca` | No Google listing; site down; possibly closed. |
| Simicards | `simicards-simi-valley-ca` | PO Box listing. May be **Simi Sportscards** at a different address and phone. Do not merge until confirmed. |
| Bakersfield.cards | `bakersfield-cards-bakersfield-ca` | May be **Great American Sports Cards**. Do not apply GASC socials. Hours need owner confirmation. |

`SEED_17` appends these notes to `dir.Shop.ImportNotes`. It does not rewrite name, phone, or address.
