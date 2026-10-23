/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Smoke / count checks moved to db/99_verify.sql
   (identical copy: db/ssms/99_verify_SSMS.sql).
   Open that file in SSMS and Execute — one PASS/FAIL/WARN grid, read-only.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
PRINT 'Use db/99_verify.sql (or db/ssms/99_verify_SSMS.sql) for the full verify grid.';
