# Shop data review (manual research 2026-10-23)

Staff flags from the first-10-shop pass. **Do not change names, phones, or addresses** from this list until an owner or SADMIN confirms. The full research attachment did not land on this agent VM, so SEED_17 applies only items named in the task itself. Hours, lat/lng, social handles, and Beckett marketplace URLs are **not** invented here.

Run `db/ssms/SEED_17_shop_enrichment_manual_SSMS.sql` after `06_shop_social_SSMS.sql`. Re-run is safe.

## Applied (verified)

| Shop | Slug | What landed |
|---|---|---|
| GamesandCards.com | `gamesandcards-com-eagle-rock-ca` | Website `https://gacsuperstore.com/` on `dir.ShopSocial` (WEBSITE, Source=`manual`, Verified=1) and `dir.Shop.WebsiteUrl` if empty |

Boxseat Collectibles and Burbank Sportscards were listed for verified website/socials. Those URLs were **not** in the instruction text and are **not** seeded (no invented handles).

Birmingham Sports Cards, Bakersfield.cards, and Wonder Water were listed for Beckett marketplace as a secondary / only-site link. The marketplace URLs were **not** in the instruction text and are **not** seeded.

## Skipped (do-not-apply / unverified)

| Item | Why |
|---|---|
| Bakersfield GASC socials | Marked do-not-apply / unverified |
| Simi Sportscards | Marked do-not-apply / unverified (possible match for Simicards — see below) |
| Boxseat hours | Leave empty even if other shops later get hours |
| Google ratings / review counts | Do not store permanently in seed. Omit from `dir.GooglePlaceCache` until a batch refresh writes them with a 30-day expiry |

## Needs SADMIN review

| Shop | Slug | Flag |
|---|---|---|
| Birmingham Sports Cards | `birmingham-sports-cards-alabaster-al` | Google Maps may show **Sports Nuts Collectibles** at this listing, phone **205-406-5080**. Directory phone stays **205-369-1973** until confirmed. |
| Cards And Stuff | `cards-and-stuff-gilbert-az` | Review identity / whether the Gilbert row is still a live shop. |
| ChicagoCards.com | `chicagocards-com-pittsburg-ca` | No Google listing; site down; possibly closed. |
| Mojo Sportscards | `mojo-sportscards-thousand-oaks-ca` | No Google listing; site down; possibly closed. |
| Simicards | `simicards-simi-valley-ca` | PO Box listing. May be **Simi Sportscards** at a different address and phone. Do not merge until confirmed. |
| Bakersfield.cards | `bakersfield-cards-bakersfield-ca` | May be **Great American Sports Cards**. Do not apply GASC socials. |

`SEED_17` appends a `DATA_REVIEW 2026-10-23:` note to `dir.Shop.ImportNotes` for the six rows above. It does not rewrite name, phone, or address.

## Follow-up

Re-attach the 2026-10-23 research file (websites, socials, hours, coordinates, Beckett marketplace URLs) and extend `SEED_17` with only those verified cells.
