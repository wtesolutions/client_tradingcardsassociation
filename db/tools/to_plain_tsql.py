#!/usr/bin/env python3
"""Convert existing db/*.sql and db/seed/*.sql to plain T-SQL (no SQLCMD)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

DB = Path(__file__).resolve().parents[1]
ROOT = DB.parent

SQLCMD_LINE = re.compile(
    r"^[ \t]*:(on error exit|r\s|setvar\s|!!|out\s|error\s|reset\s|list\s|quit\b)",
    re.I,
)
SQLCMD_BANNER = re.compile(
    r"/\*\s*SSMS: enable Query > SQLCMD Mode before running\..*?\*/\s*",
    re.S | re.I,
)
PLAIN_BANNER = re.compile(
    r"/\*\s*Plain T-SQL for SSMS[^*]*?(?:\n[^*].*?)*?\*/\s*",
    re.S,
)
DEAD_VAR_CHECK = re.compile(
    r"IF LEN\(N'(?:wte_NtcaDatabase|ntca_app|ntca_migrator|__SKIP__|CHANGE_ME|NtcaAppDev2026x)'\).*?"
    r"(?:RAISERROR\([^)]+\);\s*(?:RETURN;)?\s*)+",
    re.S,
)
# After subst, leftover "IF LEN(N'wte...') = 0 OR N'wte...' = N'$' + N'(NtcaDatabase)'"
DEAD_UNSUBST_CHECK = re.compile(
    r"IF LEN\(N'[^']+'\)\s*=\s*0\s*\n?\s*OR N'[^']+' = N'\$' \+ N'\([^']+\)'.*?"
    r"(?:RAISERROR\([^)]+\);\s*(?:RETURN;)?\s*)+",
    re.S,
)

HEADER = """/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
*/
"""


def leftover_hits(sql: str) -> list[str]:
    hits: list[str] = []
    for i, line in enumerate(sql.splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith(":"):
            hits.append(f"{i}: colon-directive {line.strip()[:80]}")
        if "$(" in line:
            hits.append(f"{i}: dollar-paren {line.strip()[:80]}")
    return hits


def subst(sql: str) -> str:
    sql = sql.replace("$(NtcaDatabase)", "wte_NtcaDatabase")
    sql = sql.replace("$(NtcaAppLogin)", "ntca_app")
    sql = sql.replace("$(NtcaMigratorLogin)", "ntca_migrator")
    sql = sql.replace("N'$(NtcaAppPassword)'", "N'NtcaAppDev2026x'")
    sql = sql.replace("N'$(NtcaMigratorPassword)'", "N'__SKIP__'")
    sql = sql.replace("$(NtcaAppPassword)", "NtcaAppDev2026x")
    sql = sql.replace("$(NtcaMigratorPassword)", "__SKIP__")
    sql = sql.replace("$(vars)", "sqlcmd variables")
    sql = sql.replace("$(var)", "sqlcmd variable")
    sql = re.sub(r"\$\([A-Za-z0-9_]+\)", "REMOVED_SQLCMD_VAR", sql)
    return sql


def ensure_xact_abort(sql: str) -> str:
    head = "\n".join(sql.splitlines()[:40])
    if re.search(r"(?i)SET\s+XACT_ABORT\s+ON", head):
        return sql
    m = re.search(r"USE \[wte_NtcaDatabase\];\s*\n", sql)
    if m:
        insert_at = m.end()
        extra = ""
        rest = sql[insert_at:]
        if not re.match(r"SET NOCOUNT ON;\s*\n", rest, re.I):
            extra += "SET NOCOUNT ON;\n"
        extra += "SET XACT_ABORT ON;\n"
        return sql[:insert_at] + extra + rest
    if re.search(r"(?i)SET\s+XACT_ABORT\s+ON", sql):
        return sql
    return "SET XACT_ABORT ON;\nGO\n" + sql


def convert(sql: str) -> str:
    sql = SQLCMD_BANNER.sub("", sql)
    kept: list[str] = []
    for line in sql.splitlines(keepends=True):
        if SQLCMD_LINE.match(line):
            continue
        kept.append(line)
    sql = "".join(kept)
    sql = subst(sql)
    sql = DEAD_VAR_CHECK.sub("", sql)
    sql = DEAD_UNSUBST_CHECK.sub("", sql)
    sql = re.sub(r"\n{3,}", "\n\n", sql)
    sql = ensure_xact_abort(sql)
    stripped = sql.lstrip()
    if not stripped.startswith("/* Plain T-SQL"):
        sql = HEADER + stripped
    if not sql.endswith("\n"):
        sql += "\n"
    return sql


def targets() -> list[Path]:
    skip_names = {
        "00_create_database.sql",
        "03_app_login.sql",
        "NTCA_FULL_INSTALL.sql",
        "run_all.sql",
    }
    out: list[Path] = []
    for path in sorted(DB.rglob("*.sql")):
        rel = path.relative_to(DB)
        if rel.parts[0] == "ssms":
            continue
        if rel.parts[0] == "source":
            continue
        if rel.parts[0] == "tools" and path.name != "validate.sql":
            continue
        if path.name in skip_names:
            continue
        out.append(path)
    return out


def main() -> int:
    converted = 0
    for path in targets():
        original = path.read_text(encoding="utf-8")
        updated = convert(original)
        leftover = leftover_hits(updated)
        if leftover:
            raise SystemExit(f"SQLCMD leftovers after convert {path}: {leftover[:8]}")
        path.write_text(updated, encoding="utf-8")
        converted += 1
        print(f"converted {path.relative_to(ROOT)}")
    print(f"converted {converted} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
