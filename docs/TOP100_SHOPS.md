# Top-100 destination shops (SEED_16)

Source: `db/tools/inputs/top_us_destination_card_shops.csv` (research current **2026-10-22**). CSV fields only — no invented addresses, phones, or websites. `SourceSystem = TOP100-2026-10-22`. Free / unclaimed. Destination tag is `ImportNotes` beginning with `Destination shop` (no extra schema).

Match against SEED_10 + CTCA SEED_15 shops: website domain **and** city+state, or 10+ phone digits, or stem+city+state, or normalized name+state. Website-only matches across different cities are skipped so multi-location brands stay separate.

- shops_new = 94
- shops_matched = 6
- total CSV rows used = 100
- after apply: `dir.Shop` total = **186** (58 SEED_10 + 34 CTCA new + 94 top-100 new)
- destination tag (`ImportNotes` contains `Destination shop`) = **100**
- `SourceSystem = TOP100-2026-10-22` = **97** (94 new + 3 previously-unmarked matches). The other 3 matches already had `CTCA-2026-10-22` and were left unchanged.
- second pass of `02_seed` + all `SEED_*`: same counts, no duplicate slugs.

| Rank | CSV name | Matched to | Reason | Slug |
|---|---|---|---|---|
| 1 | Burbank Sportscards | Burbank Sportscards | stem+city+state | `burbank-sportscards-burbank-ca` |
| 26 | Graybo's Sports Cards | Graybo's Sports Cards | stem+city+state | `graybo-s-sports-cards-richmond-va` |
| 27 | Baseball Card Connection | Baseball Card Connection | stem+city+state | `baseball-card-connection-effingham-il` |
| 71 | Three Stars Sportscards | Three Stars Sportscards | stem+city+state | `three-stars-sportscards-bloomington-mn` |
| 83 | 1,000,000 Baseball Cards | 1,000,000 Baseball Cards | name+state | `1-000-000-baseball-cards-manchester-mo` |
| 84 | RBICru7 | RBICru7 | stem+city+state | `rbicru7-st-peters-mo` |
