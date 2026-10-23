/* Least-privilege SQL Server logins for the NTCA app (IIS / ASP.NET Core).
   Required:  -v NtcaAppPassword=...   -v NtcaDatabase=NTCA
   Optional:  -v NtcaMigratorPassword=...  (creates ntca_migrator with DDL)
              -v NtcaAppLogin=ntca_app
              -v NtcaMigratorLogin=ntca_migrator

   Passwords MUST NOT be committed. This script fails if NtcaAppPassword is empty.

   HAMMER (Windows auth DBA session):
     sqlcmd -S HAMMER -E -i 03_app_login.sql -v NtcaDatabase=NTCA -v NtcaAppPassword="***"

   CHECK_POLICY = ON. Login is db_datareader + db_datawriter + EXECUTE on app
   schemas. Not db_owner, no CREATE TABLE.
*/
:on error exit

IF LEN(N'$(NtcaDatabase)') = 0 OR N'$(NtcaDatabase)' = N'$' + N'(NtcaDatabase)'
BEGIN
    RAISERROR('NtcaDatabase was not supplied. Pass -v NtcaDatabase=NTCA.', 16, 1);
    RETURN;
END
IF LEN(N'$(NtcaAppPassword)') = 0 OR N'$(NtcaAppPassword)' = N'$' + N'(NtcaAppPassword)'
BEGIN
    RAISERROR('NtcaAppPassword was not supplied. Pass -v NtcaAppPassword=*** (do not commit it).', 16, 1);
    RETURN;
END
GO

IF LEN(N'$(NtcaAppLogin)') = 0 OR N'$(NtcaAppLogin)' = N'$' + N'(NtcaAppLogin)'
    RAISERROR('NtcaAppLogin missing — run_all.cmd/.sh sets ntca_app.', 16, 1);
GO

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(NtcaAppLogin)')
BEGIN
    DECLARE @create_app nvarchar(max) = N'CREATE LOGIN [' + REPLACE(N'$(NtcaAppLogin)', N']', N']]')
        + N'] WITH PASSWORD = N''' + REPLACE(N'$(NtcaAppPassword)', N'''', N'''''')
        + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ['
        + REPLACE(N'$(NtcaDatabase)', N']', N']]') + N']';
    EXEC (@create_app);
    PRINT 'Created login $(NtcaAppLogin)';
END
ELSE
BEGIN
    DECLARE @alter_app nvarchar(max) = N'ALTER LOGIN [' + REPLACE(N'$(NtcaAppLogin)', N']', N']]')
        + N'] WITH PASSWORD = N''' + REPLACE(N'$(NtcaAppPassword)', N'''', N'''''')
        + N''' UNLOCK, CHECK_POLICY = ON';
    EXEC (@alter_app);
    PRINT 'Updated password for existing login $(NtcaAppLogin)';
END
GO

USE [$(NtcaDatabase)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(NtcaAppLogin)')
    EXEC(N'CREATE USER [' + REPLACE(N'$(NtcaAppLogin)', N']', N']]') + N'] FOR LOGIN [' + REPLACE(N'$(NtcaAppLogin)', N']', N']]') + N']');
GO

ALTER ROLE db_datareader ADD MEMBER [$(NtcaAppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(NtcaAppLogin)];
GO

GRANT EXECUTE ON SCHEMA::core  TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::dir   TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::bill  TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::mkt   TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::feed  TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::ugc   TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::karma TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::crm   TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::cms   TO [$(NtcaAppLogin)];
GRANT EXECUTE ON SCHEMA::seo   TO [$(NtcaAppLogin)];
GO

PRINT 'Granted db_datareader, db_datawriter, EXECUTE on app schemas to $(NtcaAppLogin) (not db_owner)';
GO

IF LEN(N'$(NtcaMigratorPassword)') > 0
   AND N'$(NtcaMigratorPassword)' <> N'$' + N'(NtcaMigratorPassword)'
   AND LEN(N'$(NtcaMigratorLogin)') > 0
   AND N'$(NtcaMigratorLogin)' <> N'$' + N'(NtcaMigratorLogin)'
BEGIN
    USE [master];
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(NtcaMigratorLogin)')
    BEGIN
        DECLARE @create_mig nvarchar(max) = N'CREATE LOGIN [' + REPLACE(N'$(NtcaMigratorLogin)', N']', N']]')
            + N'] WITH PASSWORD = N''' + REPLACE(N'$(NtcaMigratorPassword)', N'''', N'''''')
            + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ['
            + REPLACE(N'$(NtcaDatabase)', N']', N']]') + N']';
        EXEC (@create_mig);
        PRINT 'Created login $(NtcaMigratorLogin)';
    END
    ELSE
    BEGIN
        DECLARE @alter_mig nvarchar(max) = N'ALTER LOGIN [' + REPLACE(N'$(NtcaMigratorLogin)', N']', N']]')
            + N'] WITH PASSWORD = N''' + REPLACE(N'$(NtcaMigratorPassword)', N'''', N'''''')
            + N''' UNLOCK, CHECK_POLICY = ON';
        EXEC (@alter_mig);
        PRINT 'Updated password for existing login $(NtcaMigratorLogin)';
    END

    USE [$(NtcaDatabase)];
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(NtcaMigratorLogin)')
        EXEC(N'CREATE USER [' + REPLACE(N'$(NtcaMigratorLogin)', N']', N']]') + N'] FOR LOGIN [' + REPLACE(N'$(NtcaMigratorLogin)', N']', N']]') + N']');

    ALTER ROLE db_ddladmin ADD MEMBER [$(NtcaMigratorLogin)];
    ALTER ROLE db_datareader ADD MEMBER [$(NtcaMigratorLogin)];
    ALTER ROLE db_datawriter ADD MEMBER [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::core  TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::dir   TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::bill  TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::mkt   TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::feed  TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::ugc   TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::karma TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::crm   TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::cms   TO [$(NtcaMigratorLogin)];
    GRANT EXECUTE ON SCHEMA::seo   TO [$(NtcaMigratorLogin)];
    PRINT 'Granted db_ddladmin + data read/write/exec to $(NtcaMigratorLogin) (not db_owner)';
END
ELSE
    PRINT 'NtcaMigratorPassword not supplied — skipped migrator login';
GO
