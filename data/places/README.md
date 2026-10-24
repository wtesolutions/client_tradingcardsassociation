# Places city populations

`uscities.tsv` is the [SimpleMaps US Cities Database](https://simplemaps.com/data/us-cities) (Basic).

License: [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/).

Used only to look up **metro `population`** by `city` + `state_id` for `places enrich --top` ranking. NTCA has no city-population table in SQL.

Do not invent populations. A shop whose city+state is missing from this file scores population `0`.
