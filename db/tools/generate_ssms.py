#!/usr/bin/env python3
"""Copy canonical db/*.sql files to db/ssms/*_SSMS.sql (identical content).

Canonical scripts in db/ and db/seed/ are already plain T-SQL. This keeps
Eric's existing db/ssms/*_SSMS.sql filenames working.
"""
from __future__ import annotations

import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
from sql_order import FILES, VERIFY_FILES  # noqa: E402

DB = TOOLS.parent
SSMS = DB / "ssms"


def leftover_hits(sql: str) -> list[str]:
    hits: list[str] = []
    for i, line in enumerate(sql.splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith(":"):
            hits.append(f"{i}: colon-directive {line.strip()[:80]}")
        if "$(" in line:
            hits.append(f"{i}: dollar-paren {line.strip()[:80]}")
    return hits


def ssms_name(rel: str) -> str:
    name = Path(rel).name
    return name[:-4] + "_SSMS.sql" if name.endswith(".sql") else name


def main() -> int:
    SSMS.mkdir(parents=True, exist_ok=True)
    written = 0
    for rel in [*FILES, *VERIFY_FILES]:
        src = DB / rel
        text = src.read_text(encoding="utf-8")
        leftover = leftover_hits(text)
        if leftover:
            raise SystemExit(f"SQLCMD leftovers in {rel}: {leftover[:5]}")
        dest = SSMS / ssms_name(rel)
        dest.write_text(text, encoding="utf-8")
        written += 1

    full = (DB / "NTCA_FULL_INSTALL.sql").read_text(encoding="utf-8")
    leftover = leftover_hits(full)
    if leftover:
        raise SystemExit(f"SQLCMD leftovers in NTCA_FULL_INSTALL.sql: {leftover[:8]}")
    (SSMS / "NTCA_FULL_INSTALL_SSMS.sql").write_text(full, encoding="utf-8")
    print(f"Wrote {written} identical db/ssms/*_SSMS.sql copies plus NTCA_FULL_INSTALL_SSMS.sql")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
