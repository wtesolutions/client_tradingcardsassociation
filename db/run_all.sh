#!/usr/bin/env bash
# Apply NTCA database scripts with sqlcmd.
# Usage:
#   ./run_all.sh -S localhost,1433 -U sa -P 'Your_sa_Password1' \
#       -v NtcaAppPassword='App_Password_Here_1'
# Optional extra -v NtcaMigratorPassword=...  -v NtcaDatabase=NTCADirectory
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

if [[ " $* " != *" NtcaAppPassword="* && " $* " != *" NtcaAppPassword = "* ]]; then
  echo "ERROR: pass -v NtcaAppPassword=... (do not commit the password)." >&2
  echo "Example: $0 -S localhost,1433 -U sa -P '...' -C -v NtcaAppPassword='...'" >&2
  exit 1
fi

SQLCMD=(sqlcmd)
if command -v /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools18/bin/sqlcmd)
elif command -v /opt/mssql-tools/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools/bin/sqlcmd)
fi

# Defaults the scripts require. Caller's -v later on the command line wins in some
# sqlcmd builds; we also pass them explicitly so a bare run has a name.
EXTRA=(-v NtcaDatabase=NTCA -v NtcaAppLogin=ntca_app -v NtcaMigratorLogin=ntca_migrator)
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
