/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Create wte_NtcaDatabase if it does not exist.
   RECOVERY FULL, READ_COMMITTED_SNAPSHOT ON, instance default collation.
*/
SET XACT_ABORT ON;
GO

IF DB_ID(N'wte_NtcaDatabase') IS NULL
BEGIN
    CREATE DATABASE [wte_NtcaDatabase];
    PRINT 'Created database wte_NtcaDatabase';
END
ELSE
    PRINT 'Database wte_NtcaDatabase already exists';
GO

ALTER DATABASE [wte_NtcaDatabase] SET RECOVERY FULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.databases
    WHERE name = N'wte_NtcaDatabase' AND is_read_committed_snapshot_on = 1)
    ALTER DATABASE [wte_NtcaDatabase] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [wte_NtcaDatabase] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [wte_NtcaDatabase] SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE [wte_NtcaDatabase] SET AUTO_UPDATE_STATISTICS ON;
GO

PRINT 'wte_NtcaDatabase options: RECOVERY FULL, READ_COMMITTED_SNAPSHOT ON, ALLOW_SNAPSHOT_ISOLATION ON';
GO
USE [wte_NtcaDatabase];
GO
