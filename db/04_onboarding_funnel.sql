/* SSMS: enable Query > SQLCMD Mode before running.
   ':' directives and $(vars) fail if SQLCMD Mode is off.
   Plain T-SQL (no SQLCMD Mode): db/ssms/04_onboarding_funnel_SSMS.sql
   Idempotent: safe to run twice on an existing wte_NtcaDatabase.
*/
:setvar NtcaDatabase wte_NtcaDatabase
:on error exit

USE [$(NtcaDatabase)];
GO

/* Two-stage Preview funnel: simple register, then skippable multi-role onboarding. */

IF COL_LENGTH(N'crm.SignupFunnel', N'Stage') IS NULL
    ALTER TABLE crm.SignupFunnel ADD Stage VARCHAR(20) NOT NULL
        CONSTRAINT DF_SignupFunnel_Stage DEFAULT 'REGISTER';
IF COL_LENGTH(N'crm.SignupFunnel', N'RolesJson') IS NULL
    ALTER TABLE crm.SignupFunnel ADD RolesJson NVARCHAR(400) NULL;
IF COL_LENGTH(N'crm.SignupFunnel', N'OnboardingStatus') IS NULL
    ALTER TABLE crm.SignupFunnel ADD OnboardingStatus VARCHAR(20) NOT NULL
        CONSTRAINT DF_SignupFunnel_Onboard DEFAULT 'NOT_STARTED';
IF COL_LENGTH(N'crm.SignupFunnel', N'RegisteredAt') IS NULL
    ALTER TABLE crm.SignupFunnel ADD RegisteredAt DATETIME2(3) NULL;
IF COL_LENGTH(N'crm.SignupFunnel', N'ConfirmedAt') IS NULL
    ALTER TABLE crm.SignupFunnel ADD ConfirmedAt DATETIME2(3) NULL;
IF COL_LENGTH(N'crm.SignupFunnel', N'OnboardingStartedAt') IS NULL
    ALTER TABLE crm.SignupFunnel ADD OnboardingStartedAt DATETIME2(3) NULL;
IF COL_LENGTH(N'crm.SignupFunnel', N'OnboardingCompletedAt') IS NULL
    ALTER TABLE crm.SignupFunnel ADD OnboardingCompletedAt DATETIME2(3) NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SignupFunnel_Status' AND parent_object_id = OBJECT_ID(N'crm.SignupFunnel'))
    ALTER TABLE crm.SignupFunnel DROP CONSTRAINT CK_SignupFunnel_Status;
ALTER TABLE crm.SignupFunnel ADD CONSTRAINT CK_SignupFunnel_Status CHECK
    (Status IN ('IN_PROGRESS','REGISTERED','CONFIRMED','ONBOARDING','COMPLETED','SKIPPED','ABANDONED'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SignupFunnel_Role' AND parent_object_id = OBJECT_ID(N'crm.SignupFunnel'))
    ALTER TABLE crm.SignupFunnel DROP CONSTRAINT CK_SignupFunnel_Role;
ALTER TABLE crm.SignupFunnel ADD CONSTRAINT CK_SignupFunnel_Role CHECK
    (HumanRole IS NULL OR HumanRole IN ('collector','parent','shop_owner','promoter','dealer','media','manufacturer'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SignupFunnel_Stage' AND parent_object_id = OBJECT_ID(N'crm.SignupFunnel'))
    ALTER TABLE crm.SignupFunnel DROP CONSTRAINT CK_SignupFunnel_Stage;
ALTER TABLE crm.SignupFunnel ADD CONSTRAINT CK_SignupFunnel_Stage CHECK
    (Stage IN ('REGISTER','ONBOARDING'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SignupFunnel_Onboard' AND parent_object_id = OBJECT_ID(N'crm.SignupFunnel'))
    ALTER TABLE crm.SignupFunnel DROP CONSTRAINT CK_SignupFunnel_Onboard;
ALTER TABLE crm.SignupFunnel ADD CONSTRAINT CK_SignupFunnel_Onboard CHECK
    (OnboardingStatus IN ('NOT_STARTED','IN_PROGRESS','COMPLETED','SKIPPED'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SignupFunnel_RolesJson' AND parent_object_id = OBJECT_ID(N'crm.SignupFunnel'))
    ALTER TABLE crm.SignupFunnel DROP CONSTRAINT CK_SignupFunnel_RolesJson;
ALTER TABLE crm.SignupFunnel ADD CONSTRAINT CK_SignupFunnel_RolesJson CHECK
    (RolesJson IS NULL OR ISJSON(RolesJson) = 1);
GO

IF COL_LENGTH(N'crm.SignupStepEvent', N'Stage') IS NULL
    ALTER TABLE crm.SignupStepEvent ADD Stage VARCHAR(20) NULL;
IF COL_LENGTH(N'crm.SignupStepEvent', N'RoleKey') IS NULL
    ALTER TABLE crm.SignupStepEvent ADD RoleKey VARCHAR(40) NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'crm.SignupStepEvent') AND name = N'StepKey' AND max_length < 60)
    ALTER TABLE crm.SignupStepEvent ALTER COLUMN StepKey VARCHAR(60) NOT NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'crm.SignupFunnel') AND name = N'CurrentStep' AND max_length < 60)
    ALTER TABLE crm.SignupFunnel ALTER COLUMN CurrentStep VARCHAR(60) NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AgentInquiry_Role' AND parent_object_id = OBJECT_ID(N'analytics.AgentInquiry'))
    ALTER TABLE analytics.AgentInquiry DROP CONSTRAINT CK_AgentInquiry_Role;
ALTER TABLE analytics.AgentInquiry ADD CONSTRAINT CK_AgentInquiry_Role CHECK
    (HumanRole IS NULL OR HumanRole IN ('collector','parent','shop_owner','promoter','dealer','media','manufacturer'));
GO

IF OBJECT_ID(N'crm.OnboardingSubmission', N'U') IS NULL
BEGIN
CREATE TABLE crm.OnboardingSubmission (
    OnboardingSubmissionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OnboardingSubmission PRIMARY KEY,
    PublicId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OnboardSub_Public DEFAULT NEWSEQUENTIALID(),
    SignupFunnelId  BIGINT        NULL CONSTRAINT FK_OnboardSub_Funnel REFERENCES crm.SignupFunnel(SignupFunnelId),
    SignupId        BIGINT        NULL CONSTRAINT FK_OnboardSub_Signup REFERENCES crm.Signup(SignupId),
    Kind            VARCHAR(20)   NOT NULL,
    Mode            VARCHAR(20)   NOT NULL,
    TargetId        BIGINT        NULL,
    Name            NVARCHAR(200) NOT NULL,
    City            NVARCHAR(100) NULL,
    StateCode       CHAR(2)       NULL,
    PostalCode      VARCHAR(20)   NULL,
    ExtraJson       NVARCHAR(MAX) NULL,
    SubmitterName   NVARCHAR(120) NOT NULL CONSTRAINT DF_OnboardSub_Name DEFAULT N'',
    SubmitterEmail  NVARCHAR(320) NOT NULL CONSTRAINT DF_OnboardSub_Email DEFAULT N'',
    Status          VARCHAR(20)   NOT NULL CONSTRAINT DF_OnboardSub_Status DEFAULT 'PENDING_REVIEW',
    ReviewedByUserId BIGINT       NULL,
    ReviewedAt      DATETIME2(3)  NULL,
    DecisionReason  NVARCHAR(500) NULL,
    CreatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_OnboardSub_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_OnboardSub_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_OnboardSub_Kind CHECK (Kind IN ('SHOP','SHOW','PROMOTER','MANUFACTURER')),
    CONSTRAINT CK_OnboardSub_Mode CHECK (Mode IN ('ADD','CLAIM')),
    CONSTRAINT CK_OnboardSub_Status CHECK (Status IN ('PENDING_REVIEW','APPROVED','REJECTED','WITHDRAWN')),
    CONSTRAINT CK_OnboardSub_Json CHECK (ExtraJson IS NULL OR ISJSON(ExtraJson) = 1)
);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnboardSub_Queue' AND object_id = OBJECT_ID(N'crm.OnboardingSubmission'))
    CREATE INDEX IX_OnboardSub_Queue ON crm.OnboardingSubmission (Status, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OnboardSub_Funnel' AND object_id = OBJECT_ID(N'crm.OnboardingSubmission'))
    CREATE INDEX IX_OnboardSub_Funnel ON crm.OnboardingSubmission (SignupFunnelId);
GO

PRINT '04_onboarding_funnel: two-stage funnel columns + crm.OnboardingSubmission ready.';
GO
