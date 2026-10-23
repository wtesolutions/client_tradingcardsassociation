"""Canonical apply order for NTCA SQL scripts (plain T-SQL)."""
from __future__ import annotations

FILES = [
    "00_create_database.sql",
    "01_schema.sql",
    "04_onboarding_funnel.sql",
    "05_google_places.sql",
    "06_shop_social.sql",
    "02_seed.sql",
    "seed/SEED_00_static_pages.sql",
    "seed/SEED_10_shops.sql",
    "seed/SEED_15_ctca_members.sql",
    "seed/SEED_16_top100_shops.sql",
    "seed/SEED_17_shop_enrichment_manual.sql",
    "seed/SEED_20_events.sql",
    "seed/SEED_30_blog_bloggers.sql",
    "seed/SEED_40_about_bios.sql",
    "seed/SEED_50_gallery_media.sql",
    "seed/SEED_18_legacy_content_fix.sql",
    "seed/SEED_60_manufacturers.sql",
    "seed/SEED_65_glossary_conditions.sql",
    "seed/SEED_70_industry_figures.sql",
    "03_app_login.sql",
]

# Read-only checks — copied to db/ssms/ but not concatenated into the installer.
VERIFY_FILES = [
    "99_verify.sql",
]
