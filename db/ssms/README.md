# Plain T-SQL SSMS scripts

These files are **identical copies** of the canonical scripts in `db/` and `db/seed/`. Every `.sql` file in the repo is plain T-SQL — open any of them in SSMS and click Execute. SQLCMD Mode is not required (and must stay off).

| File | Use |
|---|---|
| `NTCA_FULL_INSTALL_SSMS.sql` | Full apply: create `wte_NtcaDatabase`, schema, seeds, `ntca_app` login |
| `00_create_database_SSMS.sql` | Create `wte_NtcaDatabase` + options |
| `01_schema_SSMS.sql` | Schema only |
| `04_onboarding_funnel_SSMS.sql` | Incremental: two-stage signup funnel columns |
| `05_google_places_SSMS.sql` | Incremental: Google Places match / cache / photo / call-log |
| `06_shop_social_SSMS.sql` | Incremental: social catalog + `dir.ShopSocial` |
| `07_manufacturer_detail_SSMS.sql` | Incremental: `DescriptionHtml` + `LogoUrl` on `dir.Manufacturer` |
| `08_owner_workspace_SSMS.sql` | Incremental: editorial Featured, owner locks/logo, photo columns |
| `09_places_enrich_SSMS.sql` | Incremental: rich Place Details, Google reviews, field sources, enrich resume |
| `02_seed_SSMS.sql` | Lookup seed |
| `SEED_*_SSMS.sql` | Content seeds (`SEED_17` enrichment, `SEED_18` bios/gallery) |
| `03_app_login_SSMS.sql` | `ntca_app` / `NtcaAppDev2026x` (DEV ONLY). Optional migrator: edit `@MigratorPassword` at the top |
| `99_verify_SSMS.sql` | Read-only PASS/FAIL/WARN grid after the incremental scripts |

**Existing QA database (already has shops):** run in this order — `04`, then `05`, then `06`, then `SEED_17`, then `SEED_18`, then `SEED_19_bio_fix` if bios still leak, then `07_manufacturer_detail`, then `SEED_61_skybox_logo`, then `08_owner_workspace`, then `09_places_enrich`, then `SEED_21` if needed. Then run `99_verify_SSMS.sql` to cross-check.

Regenerate the copies after changing `db/*.sql`:

```bash
python3 db/tools/generate_combined.py
python3 db/tools/generate_ssms.py
```

`run_all.sh` / `run_all.cmd` call sqlcmd on the same plain files (no `-v` variables). A second run is idempotent.
