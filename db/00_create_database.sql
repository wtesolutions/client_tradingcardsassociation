/* Create the NTCA database if it does not exist.
   Required sqlcmd variable: NtcaDatabase (run_all passes NTCA).
   Optional: NtcaRecovery = FULL (default) or SIMPLE for local Docker.

   sqlcmd -S HAMMER -E -i 00_create_database.sql -v NtcaDatabase=NTCA
*/
:on error exit

IF LEN(N'$(NtcaDatabase)') = 0
   OR N'$(NtcaDatabase)' = N'$' + N'(NtcaDatabase)'
BEGIN
    RAISERROR('NtcaDatabase was not supplied. Pass -v NtcaDatabase=NTCA (or NTCADirectory).', 16, 1);
    RETURN;
END
GO

IF DB_ID(N'$(NtcaDatabase)') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(N'$(NtcaDatabase)', N']', N']]') + N']';
    EXEC (@sql);
    PRINT 'Created database $(NtcaDatabase)';
END
ELSE
    PRINT 'Database $(NtcaDatabase) already exists';
GO

ALTER DATABASE [$(NtcaDatabase)] SET RECOVERY FULL;
ALTER DATABASE [$(NtcaDatabase)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$(NtcaDatabase)] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [$(NtcaDatabase)] SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE [$(NtcaDatabase)] SET AUTO_UPDATE_STATISTICS ON;
GO

-- Collation is the SQL Server instance default (not overridden).
PRINT '$(NtcaDatabase) options: RECOVERY FULL, READ_COMMITTED_SNAPSHOT ON, ALLOW_SNAPSHOT_ISOLATION ON';
GO
