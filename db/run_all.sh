#!/usr/bin/env bash
# Apply NTCA database scripts with sqlcmd. Files are plain T-SQL (no -v variables).
# Usage:
#   ./run_all.sh -S localhost,1433 -U sa -P 'Your_sa_Password1' -C
#   ./run_all.sh -S HAMMER -E
# Database and app login are hardcoded: wte_NtcaDatabase / ntca_app / NtcaAppDev2026x
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

SQLCMD=(sqlcmd)
if command -v /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools18/bin/sqlcmd)
elif command -v /opt/mssql-tools/bin/sqlcmd >/dev/null 2>&1; then
  SQLCMD=(/opt/mssql-tools/bin/sqlcmd)
fi

ARGS=("$@")

run() {
  local file="$1"
  echo "==== $file ===="
  "${SQLCMD[@]}" "${ARGS[@]}" -b -I -i "$file"
}

run "$DIR/00_create_database.sql"
run "$DIR/01_schema.sql"
run "$DIR/04_onboarding_funnel.sql"
run "$DIR/05_google_places.sql"
run "$DIR/06_shop_social.sql"
run "$DIR/07_manufacturer_detail.sql"
run "$DIR/08_owner_workspace.sql"
run "$DIR/09_places_enrich.sql"
run "$DIR/02_seed.sql"
run "$DIR/seed/SEED_00_static_pages.sql"
run "$DIR/seed/SEED_10_shops.sql"
run "$DIR/seed/SEED_15_ctca_members.sql"
run "$DIR/seed/SEED_16_top100_shops.sql"
run "$DIR/seed/SEED_17_shop_enrichment_manual.sql"
run "$DIR/seed/SEED_20_events.sql"
run "$DIR/seed/SEED_30_blog_bloggers.sql"
run "$DIR/seed/SEED_40_about_bios.sql"
run "$DIR/seed/SEED_50_gallery_media.sql"
run "$DIR/seed/SEED_18_legacy_content_fix.sql"
run "$DIR/seed/SEED_19_bio_fix.sql"
run "$DIR/seed/SEED_60_manufacturers.sql"
run "$DIR/seed/SEED_65_glossary_conditions.sql"
run "$DIR/seed/SEED_70_industry_figures.sql"
run "$DIR/03_app_login.sql"
echo "==== done ===="
