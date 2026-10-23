/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Least-privilege login ntca_app for the NTCA app (IIS / ASP.NET Core).
   DEV ONLY password NtcaAppDev2026x — change for production.
   Optional migrator: edit @MigratorPassword below (same batch). Default skips it.
   CHECK_POLICY = ON. Login is db_datareader + db_datawriter + EXECUTE on app
   schemas. Not db_owner, no CREATE TABLE.
*/
SET XACT_ABORT ON;

DECLARE @MigratorPassword NVARCHAR(128) = N'__SKIP__';

USE [master];

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ntca_app')
    CREATE LOGIN [ntca_app] WITH PASSWORD = N'NtcaAppDev2026x', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [wte_NtcaDatabase];
ELSE
    ALTER LOGIN [ntca_app] WITH PASSWORD = N'NtcaAppDev2026x' UNLOCK, CHECK_POLICY = ON;
PRINT 'Login ntca_app ready (DEV ONLY password NtcaAppDev2026x — change for production)';

USE [wte_NtcaDatabase];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ntca_app')
    CREATE USER [ntca_app] FOR LOGIN [ntca_app];

ALTER ROLE db_datareader ADD MEMBER [ntca_app];
ALTER ROLE db_datawriter ADD MEMBER [ntca_app];

GRANT EXECUTE ON SCHEMA::core  TO [ntca_app];
GRANT EXECUTE ON SCHEMA::dir   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::bill  TO [ntca_app];
GRANT EXECUTE ON SCHEMA::mkt   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::feed  TO [ntca_app];
GRANT EXECUTE ON SCHEMA::ugc   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::karma TO [ntca_app];
GRANT EXECUTE ON SCHEMA::crm   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::cms   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::seo   TO [ntca_app];
GRANT EXECUTE ON SCHEMA::analytics TO [ntca_app];

PRINT 'Granted db_datareader, db_datawriter, EXECUTE on app schemas to ntca_app (not db_owner)';

IF @MigratorPassword IS NOT NULL
   AND LEN(@MigratorPassword) > 0
   AND @MigratorPassword <> N'__SKIP__'
BEGIN
    USE [master];
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ntca_migrator')
    BEGIN
        DECLARE @create_mig nvarchar(max) = N'CREATE LOGIN [ntca_migrator] WITH PASSWORD = N'''
            + REPLACE(@MigratorPassword, N'''', N'''''')
            + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [wte_NtcaDatabase]';
        EXEC (@create_mig);
        PRINT 'Created login ntca_migrator';
    END
    ELSE
    BEGIN
        DECLARE @alter_mig nvarchar(max) = N'ALTER LOGIN [ntca_migrator] WITH PASSWORD = N'''
            + REPLACE(@MigratorPassword, N'''', N'''''')
            + N''' UNLOCK, CHECK_POLICY = ON';
        EXEC (@alter_mig);
        PRINT 'Updated password for existing login ntca_migrator';
    END

    USE [wte_NtcaDatabase];
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ntca_migrator')
        CREATE USER [ntca_migrator] FOR LOGIN [ntca_migrator];

    ALTER ROLE db_ddladmin ADD MEMBER [ntca_migrator];
    ALTER ROLE db_datareader ADD MEMBER [ntca_migrator];
    ALTER ROLE db_datawriter ADD MEMBER [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::core  TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::dir   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::bill  TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::mkt   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::feed  TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::ugc   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::karma TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::crm   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::cms   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::seo   TO [ntca_migrator];
    GRANT EXECUTE ON SCHEMA::analytics TO [ntca_migrator];
    PRINT 'Granted db_ddladmin + data read/write/exec to ntca_migrator (not db_owner)';
END
ELSE
    PRINT 'Migrator password is __SKIP__ — skipped migrator login';
GO
