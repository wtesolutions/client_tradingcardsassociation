# Plain T-SQL SSMS scripts

These copies have **no** SQLCMD syntax (`:on error`, `:setvar`, `:r`, `$(vars)`). Open in SSMS and click Execute — SQLCMD Mode is not required.

| File | Use |
|---|---|
| `NTCA_FULL_INSTALL_SSMS.sql` | Full apply: create `wte_NtcaDatabase`, schema, seeds, `ntca_app` login |
| `00_create_database_SSMS.sql` | Create `wte_NtcaDatabase` + options |
| `01_schema_SSMS.sql` | Schema only (`USE [wte_NtcaDatabase]`) |
| `02_seed_SSMS.sql` | Lookup seed |
| `SEED_*_SSMS.sql` | Content seeds |
| `03_app_login_SSMS.sql` | `ntca_app` login |

Edit `DECLARE @AppPassword` at the top of the full install or `03_app_login_SSMS.sql` (placeholder `CHANGE_ME`). `CHECK_POLICY` is ON, so set a real password before Execute.

Regenerate after changing `db/*.sql`:

```bash
python3 db/tools/generate_ssms.py
```

Verify with plain sqlcmd (no `-v`):

```bash
# replace CHANGE_ME first, or:
sed "s/N'CHANGE_ME'/N'YourAppPasswordHere'/" db/ssms/NTCA_FULL_INSTALL_SSMS.sql \
  | sqlcmd -S localhost,1433 -U sa -P '...' -C -I -b
```

A second run is idempotent.
