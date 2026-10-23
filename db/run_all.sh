#!/usr/bin/env bash
# Apply NTCA database scripts with sqlcmd.
# Usage:
#   ./run_all.sh -S localhost,1433 -U sa -P 'Your_sa_Password1' -C
# Defaults (DEV ONLY — change for production):
#   NtcaDatabase=wte_NtcaDatabase  NtcaAppLogin=ntca_app
#   NtcaAppPassword=NtcaAppDev2026x
# Optional: -v NtcaMigratorPassword=...  -v NtcaDatabase=OtherName
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

SQLCMD=(sqlcmd)
if command -v /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools18/bin/sqlcmd)
elif command -v /opt/mssql-tools/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools/bin/sqlcmd)
fi

# Defaults the scripts require. Caller's -v later on the command line wins in some
# sqlcmd builds; we also pass them explicitly so a bare run has a name.
# DEV ONLY — change NtcaAppPassword for production.
EXTRA=(-v NtcaDatabase=wte_NtcaDatabase -v NtcaAppLogin=ntca_app -v NtcaAppPassword=NtcaAppDev2026x -v NtcaMigratorLogin=ntca_migrator -v NtcaMigratorPassword=__SKIP__)
ARGS=("$@")

run() {
  local file="$1"
  echo "==== $file ===="
  "${SQLCMD[@]}" "${EXTRA[@]}" "${ARGS[@]}" -b -I -i "$file"
}

run "$DIR/00_create_database.sql"
run "$DIR/01_schema.sql"
run "$DIR/02_seed.sql"
run "$DIR/seed/SEED_00_static_pages.sql"
run "$DIR/seed/SEED_10_shops.sql"
run "$DIR/seed/SEED_15_ctca_members.sql"
run "$DIR/seed/SEED_16_top100_shops.sql"
run "$DIR/seed/SEED_20_events.sql"
run "$DIR/seed/SEED_30_blog_bloggers.sql"
run "$DIR/seed/SEED_40_about_bios.sql"
run "$DIR/seed/SEED_50_gallery_media.sql"
run "$DIR/seed/SEED_60_manufacturers.sql"
run "$DIR/seed/SEED_65_glossary_conditions.sql"
run "$DIR/seed/SEED_70_industry_figures.sql"
run "$DIR/03_app_login.sql"
echo "==== done ===="
