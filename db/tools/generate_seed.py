#!/usr/bin/env python3
"""Assemble db/01_schema.sql and regenerate db/seed/SEED_*.sql from repo content.

Reads:
  - legacy-site/hobby-shop-directory.csv
  - legacy-site/*.html (shops, events, shows, bloggers, news, about, gallery)
  - db/tools/inputs/posts/*.md  (hand-authored posts, e.g. pokemon-trading-at-shows.md)
  - legacy-site New-to-Trading-Cards HTML (glossary + condition grades) when present
  - db/source/ntca_schema.original.sql + 01_schema_additions.sql

Does not invent shops, events, bios, glossary terms, condition grades, or copy.
Missing markdown / resource-page inputs are skipped with a warning so a later
drop-in file is picked up on the next run.
"""
from __future__ import annotations

import csv
import datetime as dt
import html
import re
import sys
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEGACY = ROOT / "legacy-site"
DB = ROOT / "db"
SOURCE = DB / "source"
SEED = DB / "seed"
INPUTS = Path(__file__).resolve().parent / "inputs"
POSTS_DIR = INPUTS / "posts"


def sql_str(value: str | None) -> str:
    if value is None:
        return "NULL"
    return "N'" + value.replace("'", "''") + "'"


def sql_chr(value: str | None, n: int = 2) -> str:
    if not value:
        return "NULL"
    return "'" + value[:n].replace("'", "''") + "'"


def sql_num(value) -> str:
    if value is None or value == "":
        return "NULL"
    return str(value)


def slugify(text: str, maxlen: int = 140) -> str:
    text = html.unescape(text or "").lower()
    text = re.sub(r"[^a-z0-9]+", "-", text).strip("-")
    return text[:maxlen].strip("-") or "item"


def unique_slug(base: str, used: set[str]) -> str:
    slug = base
    i = 2
    while slug in used:
        slug = f"{base}-{i}"
        i += 1
    used.add(slug)
    return slug


def unescape(text: str | None) -> str:
    return html.unescape(re.sub(r"\s+", " ", text or "")).strip()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


# ---------------------------------------------------------------------------
# HTML helpers
# ---------------------------------------------------------------------------

class _CardCollector(HTMLParser):
    """Collect inner text of elements with a given class."""

    def __init__(self, card_class: str):
        super().__init__()
        self.card_class = card_class
        self.cards: list[dict] = []
        self._depth = 0
        self._cur: dict | None = None
        self._capture_key: str | None = None
        self._buf: list[str] = []
        self._attr_stack: list[dict] = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        classes = attrs.get("class", "").split()
        if self.card_class in classes and self._depth == 0:
            self._cur = {"tag": tag, "attrs": attrs, "text": "", "html": "", "fields": {}}
            self._depth = 1
            self._attr_stack = [attrs]
            return
        if self._cur is not None:
            self._depth += 1
            self._attr_stack.append(attrs)
            for key in ("card-title", "card-body", "card-kicker", "card-meta",
                        "blog-name", "blog-desc", "evt-name", "evt-meta",
                        "founder-name", "founder-role", "founder-body",
                        "dir-wordmark"):
                if key in classes:
                    self._capture_key = key
                    self._buf = []
            if tag == "a" and attrs.get("href"):
                self._cur.setdefault("hrefs", []).append(attrs["href"])
            if tag == "img" and attrs.get("src"):
                self._cur.setdefault("imgs", []).append(
                    {"src": attrs["src"], "alt": attrs.get("alt", "")}
                )
            if tag == "image-slot":
                self._cur.setdefault("slots", []).append(dict(attrs))

    def handle_endtag(self, tag):
        if self._cur is None:
            return
        if self._capture_key:
            text = unescape("".join(self._buf))
            self._cur["fields"].setdefault(self._capture_key, [])
            if text:
                self._cur["fields"][self._capture_key].append(text)
            self._capture_key = None
            self._buf = []
        self._depth -= 1
        if self._depth <= 0:
            self._cur["text"] = unescape(self._cur.get("text", ""))
            self.cards.append(self._cur)
            self._cur = None
            self._depth = 0

    def handle_data(self, data):
        if self._cur is None:
            return
        self._cur["text"] = self._cur.get("text", "") + data
        if self._capture_key:
            self._buf.append(data)


def collect_cards(html_text: str, card_class: str) -> list[dict]:
    p = _CardCollector(card_class)
    p.feed(html_text)
    return p.cards


def first_field(card: dict, *keys: str) -> str:
    fields = card.get("fields") or {}
    for k in keys:
        vals = fields.get(k) or []
        if vals:
            return vals[0]
    return ""


def all_fields(card: dict, key: str) -> list[str]:
    return list((card.get("fields") or {}).get(key) or [])


# ---------------------------------------------------------------------------
# Shops
# ---------------------------------------------------------------------------

STATE_RE = re.compile(r"\b([A-Z]{2})\b")


def parse_state_country(raw: str) -> tuple[str, str]:
    raw = (raw or "").strip()
    if "canada" in raw.lower():
        m = STATE_RE.search(raw)
        return (m.group(1) if m else "ON"), "CA"
    m = STATE_RE.search(raw)
    return (m.group(1) if m else ""), "US"


def shops_from_csv() -> list[dict]:
    path = LEGACY / "hobby-shop-directory.csv"
    rows = []
    with path.open(newline="", encoding="utf-8") as f:
        for i, rec in enumerate(csv.DictReader(f), start=1):
            state, country = parse_state_country(rec.get("State") or "")
            phone = (rec.get("Phone") or "").strip()
            rows.append({
                "name": (rec.get("Shop Name") or "").strip(),
                "address1": (rec.get("Address") or "").strip() or None,
                "city": (rec.get("City") or "").strip(),
                "state": state,
                "postal": (rec.get("Zip") or "").strip() or None,
                "country": country,
                "phone": phone or None,
                "email": None,
                "specialty": None,
                "source": (rec.get("Source") or "Beckett Marketplace").strip(),
                "source_ref": f"csv:{i}",
                "rating": (rec.get("Dealer Rating") or "").strip() or None,
            })
    return rows


def shops_from_html() -> list[dict]:
    text = read_text(LEGACY / "shops.html")
    out = []
    for card in collect_cards(text, "dir-card"):
        name = first_field(card, "card-title")
        if not name:
            continue
        kicker = first_field(card, "card-kicker")
        city, state = "", ""
        if "," in kicker:
            city, state = [p.strip() for p in kicker.split(",", 1)]
            state = state[:2].upper()
        body_lines = [ln.strip() for ln in (first_field(card, "card-body") or "").split("<br") ]
        # card-body was flattened; recover from raw fields
        body = " ".join(all_fields(card, "card-body"))
        # Prefer explicit address lines from the original HTML snippet via regex on name
        block = re.search(
            rf'card-title">{re.escape(html.escape(name))}.*?</div>',
            text,
            re.I | re.S,
        )
        address1 = None
        postal = None
        email = None
        phone = None
        specialty = None
        raw_block = ""
        # find this card's HTML by title
        idx = text.lower().find(html.escape(name).lower())
        if idx < 0:
            idx = text.lower().find(name.lower())
        if idx >= 0:
            raw_block = text[max(0, idx - 200): idx + 900]
        tels = re.findall(r'href="tel:([^"]+)"', raw_block)
        mails = re.findall(r'href="mailto:([^"]+)"', raw_block)
        if tels:
            phone = re.sub(r"[^\d+()-]", "", html.unescape(tels[0]))
        if mails:
            email = html.unescape(mails[0])
        needle = html.escape(name, quote=False).lower()
        idx = text.lower().find(needle)
        if idx < 0:
            idx = text.lower().find(name.lower())
        if idx >= 0:
            raw_block = text[idx: idx + 1200]
        after_title = raw_block
        addrs = re.findall(r'<p class="card-body">(.*?)</p>', after_title, re.S)
        if addrs:
            parts = html.unescape(re.sub(r"<br\s*/?>", "\n", addrs[0], flags=re.I))
            lines = [ln.strip() for ln in parts.split("\n") if ln.strip()]
            if lines:
                last = lines[-1]
                mzip = re.search(r",\s*([A-Z]{2})\s+(\d{5}(?:-\d{4})?)$", last)
                if mzip:
                    state = state or mzip.group(1)
                    postal = mzip.group(2)
                    city = city or last[: mzip.start()].strip()
                    lines = lines[:-1]
                if lines:
                    # street-like line if present, else last remaining line
                    street = next((ln for ln in lines if re.search(r"\d", ln)), lines[-1])
                    address1 = street
        metas = re.findall(r'<p class="card-meta">(.*?)</p>', raw_block, re.S)
        for meta in metas:
            plain = unescape(re.sub(r"<[^>]+>", " ", meta))
            if "Sports" in plain or "Collectibles" in plain or "Hobby" in plain:
                if "Featured" not in plain and "Charter" not in plain and "star" not in plain.lower():
                    if "Cards" in plain or "Collectibles" in plain or "Toys" in plain:
                        specialty = "SPORTS"
        out.append({
            "name": name,
            "address1": address1,
            "city": city,
            "state": state,
            "postal": postal,
            "country": "US",
            "phone": phone,
            "email": email,
            "specialty": specialty,
            "source": "shops.html",
            "source_ref": f"html:{slugify(name)}",
            "rating": None,
        })
    return out


def merge_shops() -> list[dict]:
    by_key: dict[tuple, dict] = {}
    for row in shops_from_csv() + shops_from_html():
        key = (
            slugify(row["name"]),
            slugify(row.get("city") or ""),
            (row.get("state") or "").upper(),
        )
        if key in by_key:
            existing = by_key[key]
            for k, v in row.items():
                if v and not existing.get(k):
                    existing[k] = v
        else:
            by_key[key] = dict(row)
    used: set[str] = set()
    shops = []
    for row in by_key.values():
        if not row["name"] or not row.get("city") or not row.get("state"):
            continue
        row["slug"] = unique_slug(
            slugify(f"{row['name']}-{row['city']}-{row['state']}"), used
        )
        shops.append(row)
    shops.sort(key=lambda r: (r["state"], r["city"], r["name"]))
    return shops


# ---------------------------------------------------------------------------
# Events
# ---------------------------------------------------------------------------

MONTHS = {
    "jan": 1, "january": 1, "feb": 2, "february": 2, "mar": 3, "march": 3,
    "apr": 4, "april": 4, "may": 5, "jun": 6, "june": 6, "jul": 7, "july": 7,
    "aug": 8, "august": 8, "sep": 9, "september": 9, "oct": 10, "october": 10,
    "nov": 11, "november": 11, "dec": 12, "december": 12,
}


def parse_date_range(text: str, default_year: int = 2026) -> tuple[str | None, str | None]:
    text = unescape(text)
    # "October 16-18, 2026" or "October 16–18, 2026" or "June 24-27, 2027"
    m = re.search(
        r"(January|February|March|April|May|June|July|August|September|October|November|December)\s+"
        r"(\d{1,2})(?:\s*[–-]\s*(\d{1,2}))?,?\s*(\d{4})",
        text,
        re.I,
    )
    if m:
        mon = MONTHS[m.group(1).lower()]
        y = int(m.group(4))
        start = dt.date(y, mon, int(m.group(2)))
        end = dt.date(y, mon, int(m.group(3))) if m.group(3) else start
        return start.isoformat(), end.isoformat()
    return None, None


def events_from_events_html() -> list[dict]:
    text = read_text(LEGACY / "events.html")
    events = []
    for m in re.finditer(
        r'<div class="evt-card">\s*<div class="evt-tile">'
        r'<span class="mo">(?P<mo>[^<]+)</span>'
        r'<span class="day">(?P<day>[^<]+)</span>'
        r'<span class="yr">(?P<yr>[^<]+)</span></div>'
        r'\s*<div><p class="evt-name">(?P<name>.*?)</p>'
        r'<p class="evt-meta">(?P<meta>.*?)</p>',
        text,
        re.S,
    ):
        name = unescape(re.sub(r"<[^>]+>", "", m.group("name")))
        meta_html = m.group("meta")
        meta = unescape(re.sub(r"<[^>]+>", " ", meta_html))
        mo = MONTHS.get(m.group("mo").strip().lower()[:3], 1)
        day = int(m.group("day"))
        year = int(m.group("yr"))
        start = dt.date(year, mo, day)
        end = start
        # convention Jun 24-27
        rng = re.search(r"June 24", meta, re.I)
        if rng and "27" in meta:
            end = dt.date(year, 6, 27)
        city, state = None, None
        loc = re.search(r"([A-Za-z .']+),\s*([A-Z]{2})\b", meta)
        if loc:
            city, state = loc.group(1).strip(), loc.group(2)
        etype = "OTHER"
        low = meta.lower() + " " + name.lower()
        if "convention" in low:
            etype = "CONVENTION"
        elif "trade" in low:
            etype = "TRADE_NIGHT"
        elif "education" in low or "webinar" in low:
            etype = "EDUCATION"
        elif "charity" in low:
            etype = "CHARITY"
        elif "advocacy" in low or "lobby" in low:
            etype = "ADVOCACY"
        elif "industry" in low:
            etype = "INDUSTRY"
        elif "community" in low or "kickoff" in low:
            etype = "COMMUNITY"
        elif "meetup" in low:
            etype = "MEETUP"
        events.append({
            "title": name,
            "description": meta,
            "event_type": etype,
            "starts": start.isoformat(),
            "ends": end.isoformat() if end != start else None,
            "venue": None,
            "city": city,
            "state": state,
            "source": "events.html",
        })
    return events


def events_from_shows_html() -> list[dict]:
    text = read_text(LEGACY / "shows.html")
    events = []
    for m in re.finditer(
        r'<p class="card-title">(?P<title>.*?)</p>\s*'
        r'<p class="card-body">(?P<body>.*?)</p>\s*'
        r'<ul class="dir-facts">(?P<facts>.*?)</ul>',
        text,
        re.S,
    ):
        title = unescape(re.sub(r"<[^>]+>", "", m.group("title")))
        body = unescape(re.sub(r"<[^>]+>", " ", m.group("body")))
        facts = m.group("facts")
        fact = {}
        for fm in re.finditer(r"<li><span>(?P<k>[^<]+)</span>(?P<v>.*?)</li>", facts, re.S):
            fact[unescape(fm.group("k")).lower()] = unescape(re.sub(r"<[^>]+>", "", fm.group("v")))
        loc = fact.get("location", "")
        city, state = None, None
        if "," in loc:
            city, st = [p.strip() for p in loc.split(",", 1)]
            city = city.title()
            state = st.strip()[:2].upper()
        start, end = parse_date_range(fact.get("date", ""))
        hrefs = re.findall(r'href="(https?://[^"]+)"', text[m.start(): m.end() + 400])
        events.append({
            "title": title,
            "description": body,
            "event_type": "CARD_SHOW",
            "starts": start,
            "ends": end,
            "venue": fact.get("venue"),
            "city": city,
            "state": state,
            "source": "shows.html",
            "ticket_url": hrefs[0] if hrefs else None,
        })
    return events


def merge_events() -> list[dict]:
    used: set[str] = set()
    out = []
    seen = set()
    for ev in events_from_events_html() + events_from_shows_html():
        key = (slugify(ev["title"]), ev.get("starts"))
        if key in seen:
            continue
        seen.add(key)
        ev["slug"] = unique_slug(slugify(ev["title"]), used)
        if not ev.get("starts"):
            # keep only dated events; skip undated rather than invent a day
            continue
        out.append(ev)
    out.sort(key=lambda e: (e["starts"], e["title"]))
    return out


# ---------------------------------------------------------------------------
# Bloggers / news / authored posts
# ---------------------------------------------------------------------------

def bloggers_from_html() -> list[dict]:
    text = read_text(LEGACY / "bloggers.html")
    used: set[str] = set()
    out = []
    for i, m in enumerate(re.finditer(
        r'<article class="blog-card">\s*'
        r'<div class="blog-mark"><img src="(?P<img>[^"]+)"[^>]*></div>\s*'
        r'<h2 class="blog-name">(?P<name>.*?)</h2>\s*'
        r'<p class="blog-desc">(?P<desc>.*?)</p>\s*'
        r'<a class="blog-visit" href="(?P<url>[^"]+)"',
        text,
        re.S,
    ), start=1):
        name = unescape(m.group("name"))
        out.append({
            "slug": unique_slug(slugify(name), used),
            "name": name,
            "description": unescape(m.group("desc")),
            "url": m.group("url"),
            "logo": m.group("img").lstrip("./"),
            "order": i * 10,
        })
    return out


def news_from_html() -> list[dict]:
    text = read_text(LEGACY / "news.html")
    used: set[str] = set()
    out = []
    for m in re.finditer(
        r'<p class="evt-date">(?P<date>.*?)</p>\s*'
        r'<div><p class="evt-name">(?P<title>.*?)</p>'
        r'<p class="evt-meta">(?P<meta>.*?)</p>',
        text,
        re.S,
    ):
        title = unescape(re.sub(r"<[^>]+>", "", m.group("title")))
        excerpt = unescape(re.sub(r"<[^>]+>", " ", m.group("meta")))
        year = unescape(m.group("date"))
        published = f"{year}-01-01" if year.isdigit() else None
        out.append({
            "slug": unique_slug(slugify(title), used),
            "title": title,
            "excerpt": excerpt,
            "body": excerpt,
            "body_md": excerpt,
            "author": "NTCA",
            "author_org": "NTCA",
            "status": "PUBLISHED",
            "published_at": published,
            "source": "news.html",
        })
    return out


def parse_front_matter(text: str) -> tuple[dict, str]:
    if text.startswith("---"):
        end = text.find("\n---", 3)
        if end != -1:
            raw = text[3:end]
            body = text[end + 4 :].lstrip("\n")
            meta = {}
            for line in raw.splitlines():
                if ":" in line:
                    k, v = line.split(":", 1)
                    meta[k.strip().lower()] = v.strip().strip('"').strip("'")
            return meta, body
    return {}, text


def md_inline(text: str) -> str:
    """Escape HTML then restore **bold** as <strong>."""
    escaped = html.escape(text, quote=False)
    return re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", escaped)


def md_to_html(md: str) -> str:
    """Faithful subset converter for authored posts (headings, bold, lists)."""
    lines = md.replace("\r\n", "\n").split("\n")
    out: list[str] = []
    i = 0
    n = len(lines)

    def is_ul(s: str) -> bool:
        return bool(re.match(r"^[-*] ", s))

    def is_ol(s: str) -> bool:
        return bool(re.match(r"^\d+\. ", s))

    while i < n:
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.startswith("### "):
            out.append(f"<h3>{md_inline(line[4:])}</h3>")
            i += 1
            continue
        if line.startswith("## "):
            out.append(f"<h2>{md_inline(line[3:])}</h2>")
            i += 1
            continue
        if is_ul(line):
            items = []
            while i < n and is_ul(lines[i]):
                items.append(f"<li>{md_inline(lines[i][2:])}</li>")
                i += 1
            out.append("<ul>\n" + "\n".join(items) + "\n</ul>")
            continue
        if is_ol(line):
            items = []
            while i < n and is_ol(lines[i]):
                item = re.sub(r"^\d+\. ", "", lines[i])
                items.append(f"<li>{md_inline(item)}</li>")
                i += 1
            out.append("<ol>\n" + "\n".join(items) + "\n</ol>")
            continue
        para = [line]
        i += 1
        while i < n and lines[i].strip() and not lines[i].startswith("#") and not is_ul(lines[i]) and not is_ol(lines[i]):
            para.append(lines[i])
            i += 1
        out.append(f"<p>{md_inline(' '.join(para))}</p>")
    return "\n".join(out)


def posts_from_inputs() -> list[dict]:
    out = []
    if not POSTS_DIR.exists():
        return out
    for path in sorted(POSTS_DIR.glob("*.md")):
        if path.name.lower().startswith("readme"):
            continue
        meta, body = parse_front_matter(read_text(path))
        title = meta.get("title") or path.stem.replace("-", " ").title()
        slug = meta.get("slug") or slugify(path.stem)
        published = meta.get("published_at") or meta.get("date")
        out.append({
            "slug": slug,
            "title": title,
            "excerpt": meta.get("excerpt") or (body.strip().split("\n\n")[0][:400] if body.strip() else None),
            "body": md_to_html(body),
            "body_md": body,
            "author": meta.get("author") or "NTCA",
            "author_org": meta.get("author_org") or meta.get("org") or "NTCA",
            "status": (meta.get("status") or "published").upper(),
            "published_at": published,
            "source": f"inputs/posts/{path.name}",
        })
    return out


def merge_posts() -> list[dict]:
    used: set[str] = set()
    out = []
    for p in posts_from_inputs() + news_from_html():
        p["slug"] = unique_slug(p["slug"], used)
        out.append(p)
    return out


# ---------------------------------------------------------------------------
# Bios / gallery / static pages
# ---------------------------------------------------------------------------

def bios_from_about() -> list[dict]:
    text = read_text(LEGACY / "about.html")
    used: set[str] = set()
    # Craig / Eric / Jay blocks: founder-name + founder-role + following founder-body paragraphs
    out = []
    for i, m in enumerate(re.finditer(
        r'<p class="founder-name">(?P<name>.*?)</p>\s*'
        r'<p class="founder-role">(?P<role>.*?)</p>',
        text,
        re.S,
    ), start=1):
        name = unescape(m.group("name"))
        role = unescape(m.group("role"))
        rest = text[m.end(): m.end() + 4000]
        paras = [unescape(p) for p in re.findall(r'<p class="sec-sub"[^>]*>(.*?)</p>', rest, re.S)]
        # stop at next founder-name
        bio = "\n\n".join(paras[:6])
        quote_m = re.search(r'<blockquote class="cb-quote">(.*?)</blockquote>', rest, re.S)
        quote = unescape(quote_m.group(1)) if quote_m else None
        img_m = re.search(r'<img src="([^"]+)"[^>]*alt="[^"]*' + re.escape(name.split()[0]), text)
        # simpler: nearest officer photo before this name
        before = text[: m.start()]
        imgs = re.findall(r'<div class="founder-mark officer-photo">\s*<img src="([^"]+)"', before)
        photo = imgs[-1].lstrip("./") if imgs else None
        out.append({
            "slug": unique_slug(slugify(name), used),
            "name": name,
            "role": role,
            "bio": bio,
            "quote": quote,
            "photo": photo,
            "order": i * 10,
        })
    return out


def gallery_from_html() -> list[dict]:
    text = read_text(LEGACY / "gallery.html")
    used: set[str] = set()
    out = []
    for i, m in enumerate(re.finditer(
        r'<image-slot[^>]+src="(?P<src>[^"]+)"[^>]*aria-label="(?P<alt>[^"]*)"[^>]*>'
        r'.*?</image-slot>.*?<figcaption>(?P<cap>.*?)</figcaption>',
        text,
        re.S,
    ), start=1):
        src = m.group("src").lstrip("./")
        cap = unescape(re.sub(r"<[^>]+>", "", m.group("cap")))
        alt = unescape(m.group("alt"))
        slug = unique_slug(slugify(Path(src).stem), used)
        out.append({
            "slug": slug,
            "caption": cap,
            "alt": alt,
            "path": src,
            "order": i * 10,
        })
    return out


SKIP_STATIC = {
    "email-autoresponder.html",  # autoresponder template, not a public page
    "landing.html",              # redirect stub
}


def static_pages() -> list[dict]:
    pages = []
    used: set[str] = set()
    for path in sorted(LEGACY.glob("*.html")):
        if path.name in SKIP_STATIC:
            continue
        raw = read_text(path)
        title_m = re.search(r"<title>(.*?)</title>", raw, re.S)
        title = unescape(re.sub(r"<[^>]+>", "", title_m.group(1))) if title_m else path.stem
        slug = path.stem
        loc = "/" if slug == "index" else f"/{slug}"
        pages.append({
            "slug": unique_slug(slug, used),
            "loc": loc,
            "title": title,
            "source": f"legacy-site/{path.name}",
            "indexed": 0 if slug in {"members-area", "submit-event"} else 1,
        })
    return pages


# ---------------------------------------------------------------------------
# SQL writers
# ---------------------------------------------------------------------------

HEADER = """/* Auto-generated by db/tools/generate_seed.py. Do not edit by hand.
   Re-run:  python3 db/tools/generate_seed.py
   Idempotent MERGE on slug / natural key. Safe to re-run.
*/
:on error exit
USE [$(NtcaDatabase)];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
"""


def write_seed(name: str, body: str) -> Path:
    SEED.mkdir(parents=True, exist_ok=True)
    path = SEED / name
    path.write_text(HEADER + body + "\n", encoding="utf-8")
    return path


def emit_shops(shops: list[dict]) -> str:
    lines = [
        "DECLARE @FreeTier TINYINT = (SELECT TierId FROM dir.ShopTier WHERE TierCode = 'FREE');\n",
        "IF @FreeTier IS NULL THROW 50001, 'dir.ShopTier FREE must be seeded first (02_seed.sql).', 1;\n",
        "DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n",
    ]
    for s in shops:
        lines.append(f"""
MERGE dir.Shop AS t
USING (SELECT {sql_str(s['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(s['name'])},
    AddressLine1 = {sql_str(s.get('address1'))},
    City = {sql_str(s['city'])},
    StateCode = {sql_chr(s['state'])},
    PostalCode = {sql_str(s.get('postal'))},
    CountryCode = {sql_chr(s.get('country') or 'US')},
    Phone = {sql_str(s.get('phone'))},
    PublicEmail = {sql_str(s.get('email'))},
    PrimarySpecialty = {sql_str(s.get('specialty'))},
    Source = {sql_str(s.get('source'))},
    SourceRef = {sql_str(s.get('source_ref'))},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, AddressLine1, City, StateCode, PostalCode, CountryCode, Phone, PublicEmail,
     PrimarySpecialty, CurrentTierId, ClaimStatus, ListingStatus, Source, SourceRef, PublishedAt)
VALUES
    ({sql_str(s['slug'])}, {sql_str(s['name'])}, {sql_str(s.get('address1'))}, {sql_str(s['city'])},
     {sql_chr(s['state'])}, {sql_str(s.get('postal'))}, {sql_chr(s.get('country') or 'US')},
     {sql_str(s.get('phone'))}, {sql_str(s.get('email'))}, {sql_str(s.get('specialty'))},
     @FreeTier, 'UNCLAIMED', 'ACTIVE', {sql_str(s.get('source'))}, {sql_str(s.get('source_ref'))}, @Now);
""")
    lines.append(f"PRINT 'SEED_10_shops: {len(shops)} shops merged';\n")
    return "\n".join(lines)


def emit_events(events: list[dict]) -> str:
    lines = ["DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n"]
    for e in events:
        starts = f"'{e['starts']}T14:00:00'"
        ends = f"'{e['ends']}T22:00:00'" if e.get("ends") else "NULL"
        lines.append(f"""
MERGE cms.Event AS t
USING (SELECT {sql_str(e['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Title = {sql_str(e['title'])},
    Description = {sql_str(e.get('description'))},
    EventType = {sql_str(e['event_type'])},
    StartsAt = {starts},
    EndsAt = {ends},
    VenueName = {sql_str(e.get('venue'))},
    City = {sql_str(e.get('city'))},
    StateCode = {sql_chr(e.get('state'))},
    TicketUrl = {sql_str(e.get('ticket_url'))},
    Status = 'PUBLISHED',
    SourcePage = {sql_str(e.get('source'))},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Title, Description, EventType, StartsAt, EndsAt, VenueName, City, StateCode, TicketUrl, Status, SourcePage)
VALUES
    ({sql_str(e['slug'])}, {sql_str(e['title'])}, {sql_str(e.get('description'))}, {sql_str(e['event_type'])},
     {starts}, {ends}, {sql_str(e.get('venue'))}, {sql_str(e.get('city'))}, {sql_chr(e.get('state'))},
     {sql_str(e.get('ticket_url'))}, 'PUBLISHED', {sql_str(e.get('source'))});
""")
    lines.append(f"PRINT 'SEED_20_events: {len(events)} association cms.Event rows merged';\n")
    return "\n".join(lines)


def emit_blog(bloggers: list[dict], posts: list[dict]) -> str:
    lines = ["DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n"]
    for b in bloggers:
        lines.append(f"""
MERGE cms.Blogger AS t
USING (SELECT {sql_str(b['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Name = {sql_str(b['name'])},
    Description = {sql_str(b.get('description'))},
    WebsiteUrl = {sql_str(b['url'])},
    LogoLegacyPath = {sql_str(b.get('logo'))},
    DisplayOrder = {b['order']},
    IsActive = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Name, Description, WebsiteUrl, LogoLegacyPath, DisplayOrder, IsActive)
VALUES
    ({sql_str(b['slug'])}, {sql_str(b['name'])}, {sql_str(b.get('description'))},
     {sql_str(b['url'])}, {sql_str(b.get('logo'))}, {b['order']}, 1);
""")
    for p in posts:
        pub = f"'{p['published_at']}'" if p.get("published_at") else "NULL"
        lines.append(f"""
MERGE cms.Post AS t
USING (SELECT {sql_str(p['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Title = {sql_str(p['title'])},
    Excerpt = {sql_str(p.get('excerpt'))},
    Body = {sql_str(p.get('body'))},
    BodyMarkdown = {sql_str(p.get('body_md'))},
    AuthorName = {sql_str(p.get('author'))},
    AuthorOrg = {sql_str(p.get('author_org'))},
    Status = {sql_str(p.get('status') or 'PUBLISHED')},
    PublishedAt = {pub},
    SourcePage = {sql_str(p.get('source'))},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Title, Excerpt, Body, BodyMarkdown, AuthorName, AuthorOrg, Status, PublishedAt, SourcePage)
VALUES
    ({sql_str(p['slug'])}, {sql_str(p['title'])}, {sql_str(p.get('excerpt'))},
     {sql_str(p.get('body'))}, {sql_str(p.get('body_md'))}, {sql_str(p.get('author'))},
     {sql_str(p.get('author_org'))}, {sql_str(p.get('status') or 'PUBLISHED')}, {pub}, {sql_str(p.get('source'))});
""")
    lines.append(
        f"PRINT 'SEED_30_blog_bloggers: {len(bloggers)} bloggers, {len(posts)} posts merged';\n"
    )
    return "\n".join(lines)


def emit_bios(bios: list[dict]) -> str:
    lines = ["DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n"]
    for b in bios:
        lines.append(f"""
MERGE cms.PersonBio AS t
USING (SELECT {sql_str(b['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    FullName = {sql_str(b['name'])},
    RoleTitle = {sql_str(b['role'])},
    BioText = {sql_str(b['bio'])},
    Quote = {sql_str(b.get('quote'))},
    PhotoLegacyPath = {sql_str(b.get('photo'))},
    DisplayOrder = {b['order']},
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
VALUES
    ({sql_str(b['slug'])}, {sql_str(b['name'])}, {sql_str(b['role'])}, {sql_str(b['bio'])},
     {sql_str(b.get('quote'))}, {sql_str(b.get('photo'))}, {b['order']}, 1);
""")
    lines.append(f"PRINT 'SEED_40_about_bios: {len(bios)} bios merged';\n")
    return "\n".join(lines)


def emit_gallery(items: list[dict]) -> str:
    lines = ["DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n"]
    for g in items:
        key = f"legacy/{g['path']}"
        lines.append(f"""
MERGE core.MediaAsset AS t
USING (SELECT {sql_str(key)} AS ObjectKey) AS s
ON t.ObjectKey = s.ObjectKey
WHEN MATCHED THEN UPDATE SET
    LegacyPath = {sql_str(g['path'])},
    Visibility = 'PUBLIC',
    Status = 'READY',
    ContentType = 'image/jpeg',
    AltText = {sql_str(g.get('alt'))},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ObjectKey, LegacyPath, Visibility, ContentType, Status, AltText)
VALUES
    ({sql_str(key)}, {sql_str(g['path'])}, 'PUBLIC', 'image/jpeg', 'READY', {sql_str(g.get('alt'))});

MERGE cms.GalleryItem AS t
USING (SELECT {sql_str(g['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Caption = {sql_str(g['caption'])},
    AltText = {sql_str(g.get('alt'))},
    LegacyPath = {sql_str(g['path'])},
    MediaAssetId = (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = {sql_str(key)}),
    DisplayOrder = {g['order']},
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Caption, AltText, LegacyPath, MediaAssetId, DisplayOrder, IsPublished)
VALUES
    ({sql_str(g['slug'])}, {sql_str(g['caption'])}, {sql_str(g.get('alt'))}, {sql_str(g['path'])},
     (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = {sql_str(key)}), {g['order']}, 1);
""")
    lines.append(
        f"PRINT 'SEED_50_gallery_media: {len(items)} gallery items + media assets merged';\n"
    )
    return "\n".join(lines)


def emit_static(pages: list[dict]) -> str:
    lines = [
        "/* Static marketing URLs derived from legacy-site HTML filenames.\n",
        "   Lookup/reference data (roles, tiers, karma) lives in 02_seed.sql. */\n",
        "DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();\n",
    ]
    for p in pages:
        lines.append(f"""
MERGE cms.StaticPage AS t
USING (SELECT {sql_str(p['slug'])} AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    LocPath = {sql_str(p['loc'])},
    Title = {sql_str(p['title'])},
    SourceFile = {sql_str(p['source'])},
    IsIndexed = {p['indexed']},
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT (Slug, LocPath, Title, SourceFile, IsIndexed)
VALUES ({sql_str(p['slug'])}, {sql_str(p['loc'])}, {sql_str(p['title'])}, {sql_str(p['source'])}, {p['indexed']});
""")
    lines.append(f"PRINT 'SEED_00_static_pages: {len(pages)} pages merged';\n")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Schema assemble
# ---------------------------------------------------------------------------

def assemble_schema() -> Path:
    original = read_text(SOURCE / "ntca_schema.original.sql")
    additions = read_text(SOURCE / "01_schema_additions.sql")
    calendar = read_text(SOURCE / "01_schema_directory_calendar.sql")

    original = original.replace(
        "CONSTRAINT CK_UserRole_Code CHECK (RoleCode IN ('ADMIN','MODERATOR','EDITOR'))",
        "CONSTRAINT CK_UserRole_Code CHECK (RoleCode IN ('SUPERADMIN','ADMIN','MODERATOR','EDITOR'))",
    )
    # Slug + UpdatedAt on shop events (SEO)
    original = original.replace(
        "    Title           NVARCHAR(200)  NOT NULL,\n    Description     NVARCHAR(4000) NULL,",
        "    Title           NVARCHAR(200)  NOT NULL,\n"
        "    Slug            NVARCHAR(160)  NOT NULL,\n"
        "    Description     NVARCHAR(4000) NULL,",
    )
    original = original.replace(
        "    ShopId          BIGINT         NOT NULL CONSTRAINT FK_ShopEvent_Shop REFERENCES dir.Shop(ShopId),",
        "    ShopId          BIGINT         NULL CONSTRAINT FK_ShopEvent_Shop REFERENCES dir.Shop(ShopId),",
    )
    original = original.replace(
        "    CONSTRAINT CK_ShopEvent_Status CHECK (Status IN ('DRAFT','PENDING_REVIEW','PUBLISHED','CANCELED')),",
        "    CONSTRAINT CK_ShopEvent_Status CHECK (Status IN ('DRAFT','PENDING_REVIEW','PUBLISHED','CANCELED','POSTPONED')),",
    )
    original = original.replace(
        "    CONSTRAINT CK_ShopEvent_Source CHECK (SourceType IN ('MANUAL','MAILBOX','FACEBOOK','INSTAGRAM','IMPORT')),",
        "    CONSTRAINT CK_ShopEvent_Source CHECK (SourceType IN ('MANUAL','MAILBOX','FACEBOOK','INSTAGRAM','IMPORT','SUBMIT')),",
    )
    original = original.replace(
        "    CreatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEvent_CreatedAt DEFAULT SYSUTCDATETIME(),\n"
        "    CONSTRAINT CK_ShopEvent_Type",
        "    CreatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEvent_CreatedAt DEFAULT SYSUTCDATETIME(),\n"
        "    UpdatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEvent_UpdatedAt DEFAULT SYSUTCDATETIME(),\n"
        "    CONSTRAINT CK_ShopEvent_Type",
    )

    marker = "/* =====================================================================\n   13. REFERENCE DATA"
    idx = original.find(marker)
    if idx < 0:
        raise SystemExit("Could not find reference-data section in original schema")
    schema = original[:idx].rstrip() + "\nGO\n\n"
    schema += (
        "/* Reference / lookup INSERTs from the v1 draft were moved to 02_seed.sql "
        "(idempotent MERGE). */\n\n"
    )
    schema += additions
    schema += "\n"
    schema += calendar
    out = DB / "01_schema.sql"
    header = (
        "/* Assembled by db/tools/generate_seed.py from\n"
        "   db/source/ntca_schema.original.sql + db/source/01_schema_additions.sql\n"
        "   + db/source/01_schema_directory_calendar.sql\n"
        "   Do not edit 01_schema.sql by hand; change the source files and re-run.\n*/\n"
        ":on error exit\n"
        "USE [$(NtcaDatabase)];\n"
        "GO\n"
    )
    out.write_text(header + schema, encoding="utf-8")
    return out


def main() -> int:
    POSTS_DIR.mkdir(parents=True, exist_ok=True)
    expected = POSTS_DIR / "pokemon-trading-at-shows.md"
    if not expected.exists():
        print(
            f"WARNING: {expected.relative_to(ROOT)} is missing. "
            "SEED_30 will omit that authored post until the file is present.",
            file=sys.stderr,
        )

    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from generate_extra import (
        association_events,
        calendar_shows,
        classify_ctca,
        emit_calendar,
        emit_ctca,
        emit_glossary_todo,
        emit_manufacturers,
        load_ctca_rows,
        manufacturers_from_wikipedia,
        write_manufacturers_csv,
    )

    shops = merge_shops()
    events = association_events(merge_events())
    shows = calendar_shows()
    manufacturers = manufacturers_from_wikipedia()
    write_manufacturers_csv(manufacturers)
    ctca = classify_ctca(load_ctca_rows(), shops, manufacturers)
    bloggers = bloggers_from_html()
    posts = merge_posts()
    bios = bios_from_about()
    gallery = gallery_from_html()
    pages = static_pages()
    if not any(p["slug"] == "calendar" for p in pages):
        pages.append({
            "slug": "calendar",
            "loc": "/calendar",
            "title": "National Card Show Calendar · NTCA",
            "source": "app-route:/calendar",
            "indexed": 1,
        })

    schema = assemble_schema()
    files = [
        write_seed("SEED_00_static_pages.sql", emit_static(pages)),
        write_seed("SEED_10_shops.sql", emit_shops(shops)),
        write_seed("SEED_15_ctca_members.sql", emit_ctca(ctca)),
        write_seed("SEED_20_events.sql", emit_events(events) + "\nGO\n" + emit_calendar(shows)),
        write_seed("SEED_30_blog_bloggers.sql", emit_blog(bloggers, posts)),
        write_seed("SEED_40_about_bios.sql", emit_bios(bios)),
        write_seed("SEED_50_gallery_media.sql", emit_gallery(gallery)),
        write_seed("SEED_60_manufacturers.sql", emit_manufacturers(manufacturers)),
        write_seed("SEED_65_glossary_conditions.sql", emit_glossary_todo()),
    ]

    print(f"Wrote {schema.relative_to(ROOT)}")
    print(
        f"  shops={len(shops)} cms_events={len(events)} calendar_shows={len(shows)} "
        f"manufacturers={len(manufacturers)} "
        f"ctca_shops_new={len(ctca['shops_new'])} ctca_orgs={len(ctca['orgs'])} "
        f"bloggers={len(bloggers)} posts={len(posts)} bios={len(bios)} "
        f"gallery={len(gallery)} static={len(pages)}"
    )
    for f in files:
        print(f"  {f.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
