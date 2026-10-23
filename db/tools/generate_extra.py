"""Manufacturers (Wikipedia), CTCA import, and national calendar seeds."""
from __future__ import annotations

import csv
import datetime as dt
import re
from pathlib import Path
from urllib.parse import urlparse

from generate_seed import (
    INPUTS,
    LEGACY,
    slugify,
    sql_chr,
    sql_num,
    sql_str,
    unique_slug,
    unescape,
    parse_date_range,
)

WIKI_MD = INPUTS / "wikipedia_trading_card.md"
WIKI_CSV = Path(__file__).resolve().parent / "manufacturers_source.csv"
CTCA_CSV = INPUTS / "ctca_member_directory.csv"
SOURCE_SYSTEM_CTCA = "CTCA-2026-10-22"
WIKI_RETRIEVED = "2026-10-23"

SPORT_HEADERS = [
    "Assoc. football",
    "Austr. football",
    "Baseball",
    "Basketball",
    "Boxing",
    "Cricket",
    "Golf",
    "Gridiron football",
    "Ice hockey",
    "Racing",
    "Rugby",
    "Tennis",
]
NONSPORT_HEADERS = [
    "Comic book",
    "Historic events",
    "Humor",
    "Merchand./ Toys",
    "Music",
    "Movie/ Television",
    "Nature/ Animals",
]

MFR_ALIASES = {
    "leaf": "leaf-inc",
    "leaf inc": "leaf-inc",
    "leaf, inc": "leaf-inc",
    "leaf trading cards": "leaf-inc",
    "panini": "panini-group",
    "panini america": "panini-group",
    "panini group": "panini-group",
    "rittenhouse": "rittenhouse",
    "rittenhouse archives": "rittenhouse",
    "tristar": "tristar",
    "tristar productions": "tristar",
    "sa-ge collectibles inc": "sa-ge-collectibles-inc",
    "sa ge collectibles inc": "sa-ge-collectibles-inc",
    "sage collectibles": "sa-ge-collectibles-inc",
    "wizards of the coast": "wizards-of-the-coast",
    "topps": "topps",
    "upper deck": "upper-deck",
    "futera": "futera",
    "wild card": "wild-card",
}

# Explicit classification for the 99-row CTCA snapshot. Skip id 82.
CTCA_KIND = {
    "1": "marketplace",
    "2": "marketplace",
    "3": "hobby_shop",
    "4": "other",
    "5": "hobby_shop",
    "6": "hobby_shop",
    "7": "marketplace",
    "8": "marketplace",
    "9": "marketplace",
    "10": "marketplace",
    "11": "marketplace",
    "12": "hobby_shop",
    "13": "other",
    "14": "breaker",
    "15": "grader",
    "16": "other",
    "17": "hobby_shop",
    "18": "hobby_shop",
    "19": "other",  # show promoter
    "20": "hobby_shop",
    "21": "hobby_shop",
    "22": "hobby_shop",
    "23": "hobby_shop",
    "24": "marketplace",
    "25": "hobby_shop",
    "26": "hobby_shop",
    "27": "hobby_shop",
    "28": "grader",
    "29": "marketplace",
    "30": "marketplace",
    "31": "other",
    "32": "hobby_shop",
    "33": "hobby_shop",
    "34": "marketplace",
    "35": "media",
    "36": "hobby_shop",
    "37": "marketplace",
    "38": "hobby_shop",
    "39": "other",
    "40": "breaker",
    "41": "media",
    "42": "hobby_shop",
    "43": "marketplace",
    "44": "manufacturer",
    "45": "marketplace",
    "46": "breaker",
    "47": "other",
    "48": "marketplace",
    "49": "marketplace",
    "50": "hobby_shop",
    "51": "marketplace",
    "52": "manufacturer",
    "53": "hobby_shop",
    "54": "other",
    "55": "hobby_shop",
    "56": "hobby_shop",
    "57": "hobby_shop",
    "58": "hobby_shop",
    "59": "hobby_shop",
    "60": "hobby_shop",
    "61": "marketplace",
    "62": "other",
    "63": "other",
    "64": "media",
    "65": "hobby_shop",
    "66": "marketplace",
    "67": "marketplace",
    "68": "marketplace",
    "69": "grader",
    "70": "manufacturer",
    "71": "marketplace",
    "72": "hobby_shop",
    "73": "media",
    "74": "hobby_shop",
    "75": "other",
    "76": "hobby_shop",
    "77": "marketplace",
    "78": "marketplace",
    "79": "other",
    "80": "grader",
    "81": "hobby_shop",
    "83": "hobby_shop",
    "84": "marketplace",
    "85": "hobby_shop",
    "86": "marketplace",
    "87": "other",
    "88": "hobby_shop",
    "89": "hobby_shop",
    "90": "marketplace",
    "91": "media",
    "92": "hobby_shop",
    "93": "hobby_shop",
    "94": "hobby_shop",
    "95": "hobby_shop",
    "96": "hobby_shop",
    "97": "grader",
    "98": "marketplace",
    "99": "hobby_shop",
}

ORG_TYPE = {
    "grader": "GRADER",
    "marketplace": "MARKETPLACE",
    "breaker": "BREAKER",
    "media": "MEDIA",
    "other": "OTHER",
}

# Promoter-looking "other" rows
ORG_TYPE_OVERRIDE = {
    "19": "PROMOTER",
    "87": "PROMOTER",
    "63": "INSURANCE",
    "75": "INSURANCE",
    "13": "SERVICES",
    "16": "SERVICES",
    "47": "SERVICES",
    "4": "SERVICES",
    "31": "SERVICES",
    "39": "SERVICES",
    "62": "SERVICES",
    "79": "SERVICES",
}

TZ_BY_STATE = {
    "AL": "America/Chicago", "AK": "America/Anchorage", "AZ": "America/Phoenix",
    "AR": "America/Chicago", "CA": "America/Los_Angeles", "CO": "America/Denver",
    "CT": "America/New_York", "DC": "America/New_York", "DE": "America/New_York",
    "FL": "America/New_York", "GA": "America/New_York", "HI": "Pacific/Honolulu",
    "IA": "America/Chicago", "ID": "America/Boise", "IL": "America/Chicago",
    "IN": "America/Indiana/Indianapolis", "KS": "America/Chicago",
    "KY": "America/New_York", "LA": "America/Chicago", "MA": "America/New_York",
    "MD": "America/New_York", "ME": "America/New_York", "MI": "America/Detroit",
    "MN": "America/Chicago", "MO": "America/Chicago", "MS": "America/Chicago",
    "MT": "America/Denver", "NC": "America/New_York", "ND": "America/Chicago",
    "NE": "America/Chicago", "NH": "America/New_York", "NJ": "America/New_York",
    "NM": "America/Denver", "NV": "America/Los_Angeles", "NY": "America/New_York",
    "OH": "America/New_York", "OK": "America/Chicago", "OR": "America/Los_Angeles",
    "PA": "America/New_York", "RI": "America/New_York", "SC": "America/New_York",
    "SD": "America/Chicago", "TN": "America/Chicago", "TX": "America/Chicago",
    "UT": "America/Denver", "VA": "America/New_York", "VT": "America/New_York",
    "WA": "America/Los_Angeles", "WI": "America/Chicago", "WV": "America/New_York",
    "WY": "America/Denver", "ON": "America/Toronto", "QC": "America/Toronto",
}

# Host -> promoter identity for the 28-show calendar
PROMOTER_BY_HOST = {
    "universecon.com": ("ntx-universe", "NTX Universe"),
    "pittsburghcardshow.com": ("pittsburgh-card-show", "Pittsburgh Card Show"),
    "northeastcardexpo.com": ("northeast-sports-card-expo", "Northeast Sports Card Expo"),
    "frontrowcardshow.com": ("front-row-card-show", "Front Row Card Show"),
    "collectaconusa.com": ("collect-a-con", "Collect-A-Con"),
    "txcardshows.com": ("tx-card-shows", "TX Card Shows"),
    "mid-atlanticshow.com": ("mid-atlantic-sports-card-show", "Mid-Atlantic Sports Card Show"),
    "simpletix.com": ("palm-beach-card-show", "Palm Beach Card Show"),
}


def norm_name(text: str) -> str:
    text = unescape(text or "").lower()
    text = re.sub(r"[^a-z0-9]+", " ", text)
    text = re.sub(r"\b(the|inc|llc|ltd|co|company|group)\b", " ", text)
    return re.sub(r"\s+", " ", text).strip()


def host_of(url: str | None) -> str:
    if not url:
        return ""
    raw = url.strip()
    if not raw.startswith("http"):
        raw = "https://" + raw
    try:
        host = (urlparse(raw).hostname or "").lower()
    except ValueError:
        return ""
    if host.startswith("www."):
        host = host[4:]
    return host


def clean_wiki_name(name: str) -> str:
    name = re.sub(r"\[note \d+\]", "", name)
    name = re.sub(r"\[citation needed\]", "", name)
    name = re.sub(r"\[\d+\]", "", name)
    return unescape(name).strip()


def parse_wiki_table(md: str, heading: str) -> tuple[list[str], list[list[str]]]:
    idx = md.find(heading)
    if idx < 0:
        raise SystemExit(f"Missing heading {heading} in Wikipedia extract")
    chunk = md[idx:]
    next_h = re.search(r"\n## ", chunk[4:])
    if next_h:
        chunk = chunk[: next_h.start() + 4]
    rows = []
    headers: list[str] = []
    for line in chunk.splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells):
            continue
        if not headers:
            headers = cells
            continue
        rows.append(cells)
    return headers, rows


def manufacturers_from_wikipedia() -> list[dict]:
    md = WIKI_MD.read_text(encoding="utf-8")
    by_slug: dict[str, dict] = {}

    def upsert(raw_name: str, section: str, topics: list[tuple[str, str]]) -> dict:
        name = clean_wiki_name(raw_name)
        slug = MFR_ALIASES.get(norm_name(name), slugify(name))
        row = by_slug.get(slug)
        if row is None:
            row = {
                "slug": slug,
                "name": name,
                "normalized": norm_name(name),
                "sections": set(),
                "topics": [],
                "source_system": "WIKIPEDIA-2026-10-23",
            }
            by_slug[slug] = row
        row["sections"].add(section)
        for grp, topic in topics:
            if (grp, topic) not in row["topics"]:
                row["topics"].append((grp, topic))
        return row

    headers, rows = parse_wiki_table(md, "## Manufacturers")
    sport_idx = {h: headers.index(h) for h in SPORT_HEADERS if h in headers}
    for cells in rows:
        if len(cells) < 2:
            continue
        topics = [("SPORT", h) for h, i in sport_idx.items() if i < len(cells) and cells[i].lower() == "yes"]
        upsert(cells[0], "Manufacturers", topics)

    headers, rows = parse_wiki_table(md, "## Non-sports")
    ns_idx = {h: headers.index(h) for h in NONSPORT_HEADERS if h in headers}
    for cells in rows:
        if len(cells) < 2:
            continue
        topics = [("NONSPORT", h) for h, i in ns_idx.items() if i < len(cells) and cells[i].lower() == "yes"]
        upsert(cells[0], "Non-sports", topics)

    # Supersisters, Inc. — factual set row only
    ss = upsert("Supersisters, Inc.", "Non-sports", [("NONSPORT", "Historic events")])
    ss["products"] = [{
        "slug": "supersisters-1979",
        "name": "Supersisters",
        "year": 1979,
        "count": 72,
        "category": "NONSPORT",
        "section": "Non-sports",
    }]

    out = []
    for row in by_slug.values():
        cats = set()
        if any(g == "SPORT" for g, _ in row["topics"]):
            cats.add("SPORT")
        if any(g == "NONSPORT" for g, _ in row["topics"]) or "Non-sports" in row["sections"]:
            cats.add("NONSPORT")
        row["categories"] = sorted(cats)
        row["section"] = "Non-sports" if row["sections"] == {"Non-sports"} else (
            "Manufacturers" if "Manufacturers" in row["sections"] else next(iter(row["sections"]))
        )
        if "Manufacturers" in row["sections"] and "Non-sports" in row["sections"]:
            row["section"] = "Manufacturers"
        row.setdefault("products", [])
        out.append(row)
    out.sort(key=lambda r: r["name"].lower())
    return out


def write_manufacturers_csv(rows: list[dict]) -> Path:
    WIKI_CSV.parent.mkdir(parents=True, exist_ok=True)
    with WIKI_CSV.open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "slug", "name", "normalized_name", "source_section", "categories",
            "sport_topics", "nonsport_topics", "retrieved_on", "source_url",
        ])
        for r in rows:
            sports = "|".join(t for g, t in r["topics"] if g == "SPORT")
            ns = "|".join(t for g, t in r["topics"] if g == "NONSPORT")
            w.writerow([
                r["slug"], r["name"], r["normalized"], r["section"],
                "|".join(r["categories"]), sports, ns, WIKI_RETRIEVED,
                "https://en.wikipedia.org/wiki/Trading_card",
            ])
    return WIKI_CSV


def emit_manufacturers(rows: list[dict]) -> str:
    lines = [
        "MERGE dir.ManufacturerCategory AS t\n",
        "USING (VALUES\n",
        "    ('SPORT',    N'Sport',     CAST(1 AS tinyint)),\n",
        "    ('NONSPORT', N'Non-sport', CAST(2 AS tinyint)),\n",
        "    ('TCG',      N'TCG',       CAST(3 AS tinyint))\n",
        ") AS s (CategoryCode, Name, SortOrder)\n",
        "ON t.CategoryCode = s.CategoryCode\n",
        "WHEN MATCHED THEN UPDATE SET Name = s.Name, SortOrder = s.SortOrder\n",
        "WHEN NOT MATCHED THEN INSERT (CategoryCode, Name, SortOrder)\n",
        "VALUES (s.CategoryCode, s.Name, s.SortOrder);\n",
        "DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n",
    ]
    for r in rows:
        lines.append(f"""
MERGE dir.Manufacturer AS t
USING (SELECT {sql_str(r['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(r['name'])},
    NormalizedName = {sql_str(r['normalized'])},
    SourceSection = {sql_str(r['section'])},
    SourceSystem = {sql_str(r.get('source_system') or 'WIKIPEDIA-2026-10-23')},
    RetrievedOn = '{WIKI_RETRIEVED}',
    WikipediaTitle = N'Trading card',
    IsActive = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, NormalizedName, WikipediaTitle, SourceSection, SourceSystem, RetrievedOn, IsActive)
VALUES
    ({sql_str(r['slug'])}, {sql_str(r['name'])}, {sql_str(r['normalized'])},
     N'Trading card', {sql_str(r['section'])}, {sql_str(r.get('source_system') or 'WIKIPEDIA-2026-10-23')},
     '{WIKI_RETRIEVED}', 1);
""")
        for cat in r["categories"]:
            lines.append(f"""
IF NOT EXISTS (
    SELECT 1 FROM dir.ManufacturerCategoryMap m
    JOIN dir.Manufacturer x ON x.ManufacturerId = m.ManufacturerId
    WHERE x.Slug = {sql_str(r['slug'])} AND m.CategoryCode = {sql_str(cat)})
INSERT INTO dir.ManufacturerCategoryMap (ManufacturerId, CategoryCode)
SELECT ManufacturerId, {sql_str(cat)} FROM dir.Manufacturer WHERE Slug = {sql_str(r['slug'])};
""")
        for grp, topic in r["topics"]:
            lines.append(f"""
IF NOT EXISTS (
    SELECT 1 FROM dir.ManufacturerTopic t
    JOIN dir.Manufacturer x ON x.ManufacturerId = t.ManufacturerId
    WHERE x.Slug = {sql_str(r['slug'])} AND t.TopicGroup = {sql_str(grp)} AND t.TopicName = {sql_str(topic)})
INSERT INTO dir.ManufacturerTopic (ManufacturerId, TopicGroup, TopicName)
SELECT ManufacturerId, {sql_str(grp)}, {sql_str(topic)} FROM dir.Manufacturer WHERE Slug = {sql_str(r['slug'])};
""")
        lines.append(f"""
MERGE dir.ManufacturerLink AS t
USING (SELECT m.ManufacturerId, CAST('WIKIPEDIA' AS varchar(30)) AS Rel
       FROM dir.Manufacturer m WHERE m.Slug = {sql_str(r['slug'])}) AS s
ON t.ManufacturerId = s.ManufacturerId AND t.Rel = s.Rel
WHEN MATCHED THEN UPDATE SET Url = N'https://en.wikipedia.org/wiki/Trading_card'
WHEN NOT MATCHED THEN INSERT (ManufacturerId, Rel, Url)
VALUES (s.ManufacturerId, s.Rel, N'https://en.wikipedia.org/wiki/Trading_card');
""")
        for p in r.get("products") or []:
            lines.append(f"""
MERGE dir.ManufacturerProduct AS t
USING (SELECT {sql_str(p['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(p['name'])},
    ReleasedYear = {sql_num(p.get('year'))},
    CardCount = {sql_num(p.get('count'))},
    CategoryCode = {sql_str(p.get('category'))},
    SourceSection = {sql_str(p.get('section'))},
    ManufacturerId = (SELECT ManufacturerId FROM dir.Manufacturer WHERE Slug = {sql_str(r['slug'])})
WHEN NOT MATCHED THEN INSERT
    (ManufacturerId, Slug, Name, ReleasedYear, CardCount, CategoryCode, SourceSection)
VALUES
    ((SELECT ManufacturerId FROM dir.Manufacturer WHERE Slug = {sql_str(r['slug'])}),
     {sql_str(p['slug'])}, {sql_str(p['name'])}, {sql_num(p.get('year'))}, {sql_num(p.get('count'))},
     {sql_str(p.get('category'))}, {sql_str(p.get('section'))});
""")
    sport_n = sum(1 for r in rows if "SPORT" in r["categories"])
    ns_n = sum(1 for r in rows if "NONSPORT" in r["categories"])
    both = sum(1 for r in rows if "SPORT" in r["categories"] and "NONSPORT" in r["categories"])
    lines.append(
        f"PRINT 'SEED_60_manufacturers: {len(rows)} manufacturers "
        f"(sport={sport_n} nonsport={ns_n} both={both})';\n"
    )
    return "\n".join(lines)


def load_ctca_rows() -> list[dict]:
    rows = []
    with CTCA_CSV.open(newline="", encoding="utf-8") as f:
        for rec in csv.DictReader(f):
            name = (rec.get("name") or "").strip()
            if not name or name.lower() == "not displayed":
                continue
            cid = (rec.get("ctca_id") or "").strip()
            kind = CTCA_KIND.get(cid)
            if not kind:
                raise SystemExit(f"CTCA id {cid} ({name}) has no classification")
            country = (rec.get("country") or "USA").strip()
            country_code = "US"
            if country.lower() in {"canada"}:
                country_code = "CA"
            elif country.lower() in {"united kingdom", "uk"}:
                country_code = "GB"
            state = (rec.get("state") or "").strip().upper()
            if len(state) != 2:
                state = ""
            city = (rec.get("city") or "").strip()
            website = (rec.get("website_url") or "").strip() or None
            rows.append({
                "ctca_id": cid,
                "name": name,
                "kind": kind,
                "org_type": ORG_TYPE_OVERRIDE.get(cid, ORG_TYPE.get(kind, "OTHER")),
                "address1": (rec.get("address") or "").strip() or None,
                "city": city or None,
                "state": state or None,
                "postal": (rec.get("zip") or "").strip() or None,
                "country": country_code,
                "phone": (rec.get("phone") or "").strip() or None,
                "email": (rec.get("email") or "").strip() or None,
                "website": website,
                "host": host_of(website or rec.get("website")),
                "lat": (rec.get("lat") or "").strip() or None,
                "lng": (rec.get("lng") or "").strip() or None,
                "normalized": norm_name(name),
            })
    return rows


def classify_ctca(ctca_rows: list[dict], shops: list[dict], manufacturers: list[dict]) -> dict:
    shop_idx = {}
    for s in shops:
        shop_idx[(norm_name(s["name"]), (s.get("state") or "").upper())] = s
        if s.get("host"):
            shop_idx[("host", s["host"])] = s

    mfr_idx = {r["normalized"]: r for r in manufacturers}
    for alias, slug in MFR_ALIASES.items():
        match = next((m for m in manufacturers if m["slug"] == slug), None)
        if match:
            mfr_idx[alias] = match

    used_shop = {s["slug"] for s in shops}
    used_org: set[str] = set()
    used_mfr = {m["slug"] for m in manufacturers}

    result = {
        "shops_new": [],
        "shops_matched": [],
        "orgs": [],
        "mfrs_new": [],
        "mfrs_matched": [],
        "skipped": 1,  # Not Displayed
    }

    for rec in ctca_rows:
        # Manufacturer merge by normalized name (not by state)
        if rec["kind"] == "manufacturer" or rec["normalized"] in mfr_idx:
            mfr = mfr_idx.get(rec["normalized"])
            if mfr:
                result["mfrs_matched"].append({**rec, "slug": mfr["slug"]})
            else:
                slug = unique_slug(slugify(rec["name"]), used_mfr)
                rec = {**rec, "slug": slug, "normalized": rec["normalized"]}
                result["mfrs_new"].append(rec)
                mfr_idx[rec["normalized"]] = rec
            continue

        matched_shop = shop_idx.get((rec["normalized"], rec.get("state") or ""))
        if not matched_shop and rec["host"]:
            # website match only when names are the same normalized token set
            host_hit = shop_idx.get(("host", rec["host"]))
            if host_hit and host_hit.get("normalized", norm_name(host_hit["name"])) == rec["normalized"]:
                matched_shop = host_hit
        if matched_shop:
            result["shops_matched"].append({**rec, "slug": matched_shop["slug"]})
            continue

        if rec["kind"] == "hobby_shop" and rec.get("city") and rec.get("state"):
            slug = unique_slug(
                slugify(f"{rec['name']}-{rec['city']}-{rec['state']}"), used_shop
            )
            result["shops_new"].append({**rec, "slug": slug})
            continue

        # Online-only shops or non-shop types
        if rec["kind"] == "hobby_shop":
            rec = {**rec, "org_type": "OTHER"}
        slug = unique_slug(slugify(rec["name"]), used_org)
        if rec.get("state"):
            slug = unique_slug(slugify(f"{rec['name']}-{rec['state']}"), used_org)
        result["orgs"].append({**rec, "slug": slug})

    return result


def emit_ctca(classified: dict) -> str:
    lines = [
        "DECLARE @FreeTier TINYINT = (SELECT TierId FROM dir.ShopTier WHERE TierCode = 'FREE');\n",
        "IF @FreeTier IS NULL THROW 50001, 'dir.ShopTier FREE must be seeded first (02_seed.sql).', 1;\n",
        "DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n",
        f"DECLARE @Src NVARCHAR(40) = N'{SOURCE_SYSTEM_CTCA}';\n",
    ]
    for s in classified["shops_new"]:
        lines.append(f"""
MERGE dir.Shop AS t
USING (SELECT {sql_str(s['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(s['name'])},
    AddressLine1 = COALESCE({sql_str(s.get('address1'))}, t.AddressLine1),
    City = {sql_str(s['city'])},
    StateCode = {sql_chr(s['state'])},
    PostalCode = COALESCE({sql_str(s.get('postal'))}, t.PostalCode),
    CountryCode = {sql_chr(s.get('country') or 'US')},
    Phone = COALESCE({sql_str(s.get('phone'))}, t.Phone),
    PublicEmail = COALESCE({sql_str(s.get('email'))}, t.PublicEmail),
    WebsiteUrl = COALESCE({sql_str(s.get('website'))}, t.WebsiteUrl),
    Latitude = COALESCE({sql_num(s.get('lat'))}, t.Latitude),
    Longitude = COALESCE({sql_num(s.get('lng'))}, t.Longitude),
    SourceSystem = @Src,
    ImportNotes = N'Directory import 2026-10-22',
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, AddressLine1, City, StateCode, PostalCode, CountryCode, Phone, PublicEmail,
     WebsiteUrl, Latitude, Longitude, CurrentTierId, ClaimStatus, ListingStatus,
     Source, SourceRef, SourceSystem, ImportNotes, PublishedAt)
VALUES
    ({sql_str(s['slug'])}, {sql_str(s['name'])}, {sql_str(s.get('address1'))}, {sql_str(s['city'])},
     {sql_chr(s['state'])}, {sql_str(s.get('postal'))}, {sql_chr(s.get('country') or 'US')},
     {sql_str(s.get('phone'))}, {sql_str(s.get('email'))}, {sql_str(s.get('website'))},
     {sql_num(s.get('lat'))}, {sql_num(s.get('lng'))},
     @FreeTier, 'UNCLAIMED', 'ACTIVE', N'directory-import', {sql_str('ctca-id:' + s['ctca_id'])},
     @Src, N'Directory import 2026-10-22', @Now);
""")
    for s in classified["shops_matched"]:
        lines.append(f"""
UPDATE dir.Shop
SET WebsiteUrl = COALESCE(WebsiteUrl, {sql_str(s.get('website'))}),
    Latitude = COALESCE(Latitude, {sql_num(s.get('lat'))}),
    Longitude = COALESCE(Longitude, {sql_num(s.get('lng'))}),
    Phone = COALESCE(Phone, {sql_str(s.get('phone'))}),
    PublicEmail = COALESCE(PublicEmail, {sql_str(s.get('email'))}),
    SourceSystem = @Src,
    ImportNotes = COALESCE(ImportNotes, N'Directory import 2026-10-22 (matched existing shop)'),
    UpdatedAt = @Now
WHERE Slug = {sql_str(s['slug'])};
""")
    for o in classified["orgs"]:
        lines.append(f"""
MERGE dir.Organization AS t
USING (SELECT {sql_str(o['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(o['name'])},
    NormalizedName = {sql_str(o['normalized'])},
    OrgType = {sql_str(o['org_type'])},
    AddressLine1 = {sql_str(o.get('address1'))},
    City = {sql_str(o.get('city'))},
    StateCode = {sql_chr(o.get('state'))},
    PostalCode = {sql_str(o.get('postal'))},
    CountryCode = {sql_chr(o.get('country') or 'US')},
    Latitude = {sql_num(o.get('lat'))},
    Longitude = {sql_num(o.get('lng'))},
    Phone = {sql_str(o.get('phone'))},
    WebsiteUrl = {sql_str(o.get('website'))},
    PublicEmail = {sql_str(o.get('email'))},
    SourceSystem = @Src,
    SourceRef = {sql_str('id:' + o['ctca_id'])},
    ImportNotes = N'Directory import 2026-10-22',
    ListingStatus = 'ACTIVE',
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, NormalizedName, OrgType, AddressLine1, City, StateCode, PostalCode, CountryCode,
     Latitude, Longitude, Phone, WebsiteUrl, PublicEmail, Source, SourceRef, SourceSystem, ImportNotes, ListingStatus)
VALUES
    ({sql_str(o['slug'])}, {sql_str(o['name'])}, {sql_str(o['normalized'])}, {sql_str(o['org_type'])},
     {sql_str(o.get('address1'))}, {sql_str(o.get('city'))}, {sql_chr(o.get('state'))}, {sql_str(o.get('postal'))},
     {sql_chr(o.get('country') or 'US')}, {sql_num(o.get('lat'))}, {sql_num(o.get('lng'))},
     {sql_str(o.get('phone'))}, {sql_str(o.get('website'))}, {sql_str(o.get('email'))},
     N'directory-import', {sql_str('id:' + o['ctca_id'])}, @Src, N'Directory import 2026-10-22', 'ACTIVE');
""")
    for m in classified["mfrs_matched"]:
        lines.append(f"""
MERGE dir.Manufacturer AS t
USING (SELECT {sql_str(m['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    WebsiteUrl = COALESCE(t.WebsiteUrl, {sql_str(m.get('website'))}),
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, NormalizedName, WebsiteUrl, SourceSection, SourceSystem, RetrievedOn, IsActive)
VALUES
    ({sql_str(m['slug'])}, {sql_str(m['name'])}, {sql_str(m['normalized'])}, {sql_str(m.get('website'))},
     'Import', @Src, '2026-10-22', 1);
""")
    for m in classified["mfrs_new"]:
        lines.append(f"""
MERGE dir.Manufacturer AS t
USING (SELECT {sql_str(m['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(m['name'])},
    NormalizedName = {sql_str(m['normalized'])},
    WebsiteUrl = COALESCE({sql_str(m.get('website'))}, t.WebsiteUrl),
    SourceSection = 'Import',
    SourceSystem = @Src,
    IsActive = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, NormalizedName, WebsiteUrl, SourceSection, SourceSystem, RetrievedOn, IsActive)
VALUES
    ({sql_str(m['slug'])}, {sql_str(m['name'])}, {sql_str(m['normalized'])}, {sql_str(m.get('website'))},
     'Import', @Src, '2026-10-22', 1);
""")
        lines.append(f"""
IF NOT EXISTS (
    SELECT 1 FROM dir.ManufacturerCategoryMap m
    JOIN dir.Manufacturer x ON x.ManufacturerId = m.ManufacturerId
    WHERE x.Slug = {sql_str(m['slug'])} AND m.CategoryCode = 'SPORT')
INSERT INTO dir.ManufacturerCategoryMap (ManufacturerId, CategoryCode)
SELECT ManufacturerId, 'SPORT' FROM dir.Manufacturer WHERE Slug = {sql_str(m['slug'])};
""")
    lines.append(
        "PRINT 'SEED_15_ctca_members: "
        f"shops_new={len(classified['shops_new'])} shops_matched={len(classified['shops_matched'])} "
        f"orgs={len(classified['orgs'])} mfrs_new={len(classified['mfrs_new'])} "
        f"mfrs_matched={len(classified['mfrs_matched'])} skipped={classified['skipped']}';\n"
    )
    return "\n".join(lines)


def parse_show_date(text: str) -> tuple[str | None, str | None]:
    text = unescape(text)
    months = (
        "January|February|March|April|May|June|July|August|September|October|November|December"
    )
    m = re.search(
        rf"({months})\s+(\d{{1,2}})(?:\s*[–-]\s*({months})\s+)?(?:\s*[–-]\s*)?(\d{{1,2}})?,?\s*(\d{{4}})",
        text,
        re.I,
    )
    if not m:
        return parse_date_range(text)
    mon_map = {
        "jan": 1, "january": 1, "feb": 2, "february": 2, "mar": 3, "march": 3,
        "apr": 4, "april": 4, "may": 5, "jun": 6, "june": 6, "jul": 7, "july": 7,
        "aug": 8, "august": 8, "sep": 9, "september": 9, "oct": 10, "october": 10,
        "nov": 11, "november": 11, "dec": 12, "december": 12,
    }
    start_mon = mon_map[m.group(1).lower()]
    start_day = int(m.group(2))
    year = int(m.group(5))
    if m.group(3):
        end_mon = mon_map[m.group(3).lower()]
        end_day = int(m.group(4) or m.group(2))
    elif m.group(4):
        end_mon = start_mon
        end_day = int(m.group(4))
    else:
        end_mon, end_day = start_mon, start_day
    start = dt.date(year, start_mon, start_day)
    end = dt.date(year, end_mon, end_day)
    return start.isoformat(), end.isoformat()


def infer_scope(title: str, body: str, table_count: int | None) -> str:
    blob = f"{title} {body}".lower()
    if table_count and table_count >= 500:
        return "NATIONAL"
    if "convention-scale" in blob:
        return "NATIONAL"
    if table_count and table_count >= 400 and ("convention" in blob or "showcase" in blob):
        return "NATIONAL"
    if "regional" in blob or "expo" in blob or (table_count and table_count >= 150):
        return "REGIONAL"
    return "LOCAL"


def infer_focus(focus: str, body: str) -> str:
    blob = f"{focus} {body}".lower()
    tags = []
    if "sport" in blob:
        tags.append("SPORTS")
    if "pok" in blob:
        tags.append("POKEMON")
    if "tcg" in blob or "magic" in blob or "one piece" in blob:
        tags.append("TCG")
    if "non-sport" in blob or "nonsport" in blob:
        tags.append("NONSPORT")
    if "comic" in blob:
        tags.append("COMICS")
    if "memorabilia" in blob or "collectible" in blob:
        tags.append("MEMORABILIA")
    return ",".join(tags) if tags else None


def parse_table_count(body: str) -> int | None:
    m = re.search(r"(\d{1,3}(?:,\d{3})+|\d+)\+?\s*-?\s*tables", body, re.I)
    if m:
        return int(m.group(1).replace(",", ""))
    m = re.search(r"(\d{2,4})-table", body, re.I)
    if m:
        return int(m.group(1))
    m = re.search(r"(\d{2,3})-(\d{2,3}) tables", body, re.I)
    if m:
        return int(m.group(2))
    return None


def parse_admission(body: str) -> tuple[str | None, int | None]:
    if re.search(r"free admission", body, re.I):
        return "Free", 0
    return None, None


def calendar_shows() -> list[dict]:
    text = (LEGACY / "shows.html").read_text(encoding="utf-8")
    used_event: set[str] = set()
    used_venue: set[str] = set()
    used_promoter: set[str] = set()
    shows = []
    for m in re.finditer(
        r'<p class="card-title">(?P<title>.*?)</p>\s*'
        r'<p class="card-body">(?P<body>.*?)</p>\s*'
        r'<ul class="dir-facts">(?P<facts>.*?)</ul>',
        text,
        re.S,
    ):
        title = unescape(re.sub(r"<[^>]+>", "", m.group("title")))
        body = unescape(re.sub(r"<[^>]+>", " ", m.group("body")))
        facts = {}
        for fm in re.finditer(r"<li><span>(?P<k>[^<]+)</span>(?P<v>.*?)</li>", m.group("facts"), re.S):
            facts[unescape(fm.group("k")).lower()] = unescape(re.sub(r"<[^>]+>", "", fm.group("v")))
        loc = facts.get("location", "")
        city = state = None
        if "," in loc:
            city, st = [p.strip() for p in loc.split(",", 1)]
            city = city.title()
            state = st.strip()[:2].upper()
        start, end = parse_show_date(facts.get("date", ""))
        if not start:
            continue
        hrefs = re.findall(r'href="(https?://[^"]+)"', text[m.start(): m.end() + 400])
        url = hrefs[0] if hrefs else None
        host = host_of(url)
        venue_name = facts.get("venue")
        tables = parse_table_count(body)
        admission, admit_cents = parse_admission(body)
        parking = "Free parking" if re.search(r"free parking", body, re.I) else None
        scope = infer_scope(title, body, tables)
        focus = infer_focus(facts.get("focus", ""), body)
        start_dt = dt.date.fromisoformat(start)
        slug = unique_slug(f"{slugify(title)}-{start_dt:%Y-%m}", used_event)
        series = None
        if "pittsburgh" in title.lower():
            series = "pittsburgh-card-show"
        elif "collect-a-con" in title.lower():
            series = "collect-a-con"
        elif host == "txcardshows.com":
            series = "tx-card-shows"
        if host in PROMOTER_BY_HOST:
            pslug, pname = PROMOTER_BY_HOST[host]
            used_promoter.add(pslug)
        else:
            pname = title.split("—")[0].split(" at ")[0].strip()
            pslug = unique_slug(slugify(pname), used_promoter)
        vkey = slugify(f"{venue_name or title}-{city or ''}-{state or ''}")
        if vkey in used_venue:
            vslug = vkey
        else:
            vslug = unique_slug(vkey, used_venue)
        tz = TZ_BY_STATE.get(state or "", "America/New_York")
        if (city or "").lower() == "el paso" and state == "TX":
            tz = "America/Denver"
        shows.append({
            "slug": slug,
            "title": title,
            "description": body,
            "starts": start,
            "ends": end,
            "city": city,
            "state": state,
            "venue": venue_name,
            "venue_slug": vslug,
            "promoter_slug": pslug,
            "promoter_name": pname,
            "organizer_url": url,
            "scope": scope,
            "table_count": tables,
            "admission": admission,
            "admission_cents": admit_cents,
            "parking": parking,
            "focus": focus,
            "series": series,
            "tz": tz,
            "featured": 1 if scope == "NATIONAL" else 0,
        })
    return shows


def emit_calendar(shows: list[dict]) -> str:
    lines = [
        "MERGE mkt.PromoterTier AS t\n",
        "USING (VALUES\n",
        "    (CAST(1 AS tinyint), 'FREE',     N'Free listing', NULL, CAST(1 AS tinyint), CAST(1 AS bit)),\n",
        "    (CAST(2 AS tinyint), 'FEATURED', N'Featured',     NULL, CAST(2 AS tinyint), CAST(1 AS bit)),\n",
        "    (CAST(3 AS tinyint), 'PREMIUM',  N'Premium',      NULL, CAST(3 AS tinyint), CAST(1 AS bit))\n",
        ") AS s (PromoterTierId, TierCode, Name, MonthlyPriceCents, SortOrder, IsActive)\n",
        "ON t.PromoterTierId = s.PromoterTierId\n",
        "WHEN MATCHED THEN UPDATE SET Name = s.Name, TierCode = s.TierCode, IsActive = s.IsActive\n",
        "WHEN NOT MATCHED THEN INSERT (PromoterTierId, TierCode, Name, MonthlyPriceCents, SortOrder, IsActive)\n",
        "VALUES (s.PromoterTierId, s.TierCode, s.Name, s.MonthlyPriceCents, s.SortOrder, s.IsActive);\n",
        "DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n",
        "DECLARE @FreeProm TINYINT = 1;\n",
    ]
    promoters = {}
    venues = {}
    for s in shows:
        promoters[s["promoter_slug"]] = s
        venues[s["venue_slug"]] = s
    for slug, s in sorted(promoters.items()):
        lines.append(f"""
MERGE mkt.Promoter AS t
USING (SELECT {sql_str(slug)} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(s['promoter_name'])},
    WebsiteUrl = COALESCE({sql_str(s.get('organizer_url'))}, t.WebsiteUrl),
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT (Slug, Name, WebsiteUrl, CurrentTierId, IsVerified, ClaimStatus)
VALUES ({sql_str(slug)}, {sql_str(s['promoter_name'])}, {sql_str(s.get('organizer_url'))}, @FreeProm, 0, 'UNCLAIMED');
""")
    for slug, s in sorted(venues.items()):
        lines.append(f"""
MERGE mkt.Venue AS t
USING (SELECT {sql_str(slug)} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(s.get('venue') or s['title'])},
    City = {sql_str(s.get('city'))},
    StateCode = {sql_chr(s.get('state'))},
    TimeZoneId = {sql_str(s['tz'])},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT (Slug, Name, City, StateCode, CountryCode, TimeZoneId)
VALUES ({sql_str(slug)}, {sql_str(s.get('venue') or s['title'])}, {sql_str(s.get('city'))},
        {sql_chr(s.get('state'))}, 'US', {sql_str(s['tz'])});
""")
    for s in shows:
        starts = f"'{s['starts']}T00:00:00'"
        ends = f"'{s['ends']}T23:59:00'" if s.get("ends") else "NULL"
        lines.append(f"""
MERGE mkt.ShopEvent AS t
USING (SELECT {sql_str(s['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Title = {sql_str(s['title'])},
    Description = {sql_str(s.get('description'))},
    EventType = 'CARD_SHOW',
    StartsAt = {starts},
    EndsAt = {ends},
    TimeZoneId = {sql_str(s['tz'])},
    VenueName = {sql_str(s.get('venue'))},
    TicketUrl = {sql_str(s.get('organizer_url'))},
    OrganizerUrl = {sql_str(s.get('organizer_url'))},
    Status = 'PUBLISHED',
    SourceType = 'IMPORT',
    PromoterId = (SELECT PromoterId FROM mkt.Promoter WHERE Slug = {sql_str(s['promoter_slug'])}),
    VenueId = (SELECT VenueId FROM mkt.Venue WHERE Slug = {sql_str(s['venue_slug'])}),
    RecurrenceSeries = {sql_str(s.get('series'))},
    Scope = {sql_str(s['scope'])},
    TableCount = {sql_num(s.get('table_count'))},
    AdmissionText = {sql_str(s.get('admission'))},
    AdmissionMinCents = {sql_num(s.get('admission_cents'))},
    ParkingText = {sql_str(s.get('parking'))},
    FocusTags = {sql_str(s.get('focus'))},
    IsFeatured = {s['featured']},
    StateCode = {sql_chr(s.get('state'))},
    City = {sql_str(s.get('city'))},
    PublishedAt = COALESCE(t.PublishedAt, @Now),
    LastVerifiedAt = @Now,
    SourceSystem = N'NTCA-SHOWS-HTML',
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ShopId, Title, Slug, Description, EventType, StartsAt, EndsAt, TimeZoneId, VenueName, TicketUrl,
     Status, SourceType, PromoterId, VenueId, RecurrenceSeries, Scope, TableCount, AdmissionText,
     AdmissionMinCents, ParkingText, FocusTags, OrganizerUrl, IsFeatured, StateCode, City,
     PublishedAt, LastVerifiedAt, SourceSystem)
VALUES
    (NULL, {sql_str(s['title'])}, {sql_str(s['slug'])}, {sql_str(s.get('description'))}, 'CARD_SHOW',
     {starts}, {ends}, {sql_str(s['tz'])}, {sql_str(s.get('venue'))}, {sql_str(s.get('organizer_url'))},
     'PUBLISHED', 'IMPORT',
     (SELECT PromoterId FROM mkt.Promoter WHERE Slug = {sql_str(s['promoter_slug'])}),
     (SELECT VenueId FROM mkt.Venue WHERE Slug = {sql_str(s['venue_slug'])}),
     {sql_str(s.get('series'))}, {sql_str(s['scope'])}, {sql_num(s.get('table_count'))},
     {sql_str(s.get('admission'))}, {sql_num(s.get('admission_cents'))}, {sql_str(s.get('parking'))},
     {sql_str(s.get('focus'))}, {sql_str(s.get('organizer_url'))}, {s['featured']},
     {sql_chr(s.get('state'))}, {sql_str(s.get('city'))}, @Now, @Now, N'NTCA-SHOWS-HTML');
""")
        if s["featured"]:
            lines.append(f"""
IF NOT EXISTS (
    SELECT 1 FROM feed.ShowFeedEntry f
    JOIN mkt.ShopEvent e ON e.ShopEventId = f.ShopEventId
    WHERE e.Slug = {sql_str(s['slug'])} AND f.SlotCode = 'CALENDAR_FEATURED' AND f.IsActive = 1)
INSERT INTO feed.ShowFeedEntry (SlotCode, ShopEventId, Reason, BaseWeight, IsPinned, EligibleFrom, EligibleUntil)
SELECT 'CALENDAR_FEATURED', e.ShopEventId, 'FEATURED', 2.00, 0, @Now, DATEADD(day, 45, e.StartsAt)
FROM mkt.ShopEvent e WHERE e.Slug = {sql_str(s['slug'])};
""")
    n_nat = sum(1 for s in shows if s["scope"] == "NATIONAL")
    n_reg = sum(1 for s in shows if s["scope"] == "REGIONAL")
    n_loc = sum(1 for s in shows if s["scope"] == "LOCAL")
    lines.append(
        f"PRINT 'SEED_20_events calendar: {len(shows)} shows "
        f"(national={n_nat} regional={n_reg} local={n_loc}) "
        f"promoters={len(promoters)} venues={len(venues)}';\n"
    )
    return "\n".join(lines)


def emit_glossary_todo() -> str:
    candidates = [
        LEGACY / "new-to-trading-cards.html",
        LEGACY / "new-to-trading-cards.htm",
        LEGACY / "glossary.html",
    ]
    found = next((p for p in candidates if p.exists()), None)
    if found:
        return (
            f"PRINT 'SEED_65_glossary_conditions: {found.name} is present but "
            "a structured extractor has not been wired; leaving tables empty.';\n"
        )
    return (
        "PRINT 'SEED_65_glossary_conditions: TODO — New to Trading Cards page "
        "not on main yet. cms.GlossaryTerm / cms.ConditionGrade stay empty.';\n"
    )


def association_events(all_events: list[dict]) -> list[dict]:
    return [e for e in all_events if e.get("source") != "shows.html"]
