/* =====================================================================
   NTCA schema additions (architecture plan §8 / §12 + SEO discovery)
   Applied after the v1 36-table script. Draft 2026-10-23.
   Schemas added: crm (signup/preview), cms (public content), seo (sitemap views)
   ===================================================================== */

CREATE SCHEMA crm;
GO
CREATE SCHEMA cms;
GO
CREATE SCHEMA seo;
GO

/* ---------------------------------------------------------------------
   Roles catalog (UserRole.RoleCode stays the grant table)
   --------------------------------------------------------------------- */
CREATE TABLE core.Role
(
    RoleCode     VARCHAR(20)   NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
    Name         NVARCHAR(80)  NOT NULL,
    Description  NVARCHAR(300) NULL,
    SortOrder    TINYINT       NOT NULL,
    CONSTRAINT CK_Role_Code CHECK (RoleCode IN ('SUPERADMIN','ADMIN','MODERATOR','EDITOR'))
);
GO

ALTER TABLE core.UserRole
    ADD CONSTRAINT FK_UserRole_Role FOREIGN KEY (RoleCode) REFERENCES core.Role(RoleCode);
GO

/* ---------------------------------------------------------------------
   Auth tokens (plan §5 / §8.1–8.2)
   core.VerificationCode = short hashed codes (login / claim).
   crm.EmailVerification = emailed links + codes (signup confirm, etc.).
   Both exist: different lifetimes and callers.
   --------------------------------------------------------------------- */
CREATE TABLE core.VerificationCode
(
    VerificationCodeId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VerificationCode PRIMARY KEY,
    UserId             BIGINT         NULL CONSTRAINT FK_VerificationCode_User REFERENCES core.AppUser(UserId),
    Email              NVARCHAR(320)  NOT NULL,
    Purpose            VARCHAR(30)    NOT NULL,
    CodeHash           VARBINARY(32)  NOT NULL,
    ExpiresAt          DATETIME2(3)   NOT NULL,
    AttemptCount       TINYINT        NOT NULL CONSTRAINT DF_VerificationCode_Attempts DEFAULT 0,
    UsedAt             DATETIME2(3)   NULL,
    UsedFromIp         VARCHAR(45)    NULL,
    RevokedAt          DATETIME2(3)   NULL,
    CreatedAt          DATETIME2(3)   NOT NULL CONSTRAINT DF_VerificationCode_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_VerificationCode_Purpose CHECK (Purpose IN ('LOGIN','SADMIN_LOGIN','CLAIM','EMAIL_CHANGE'))
);
GO
CREATE INDEX IX_VerificationCode_Email ON core.VerificationCode (Email, Purpose, ExpiresAt);
GO

CREATE TABLE core.RefreshToken
(
    RefreshTokenId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
    UserId             BIGINT         NOT NULL CONSTRAINT FK_RefreshToken_User REFERENCES core.AppUser(UserId),
    TokenHash          VARBINARY(32)  NOT NULL,
    DeviceId           NVARCHAR(80)   NULL,
    DeviceName         NVARCHAR(120)  NULL,
    Platform           VARCHAR(20)    NULL,
    CreatedAt          DATETIME2(3)   NOT NULL CONSTRAINT DF_RefreshToken_CreatedAt DEFAULT SYSUTCDATETIME(),
    ExpiresAt          DATETIME2(3)   NOT NULL,
    RevokedAt          DATETIME2(3)   NULL,
    ReplacedByTokenId  BIGINT         NULL CONSTRAINT FK_RefreshToken_Replaced REFERENCES core.RefreshToken(RefreshTokenId),
    CONSTRAINT UQ_RefreshToken_Hash UNIQUE (TokenHash)
);
GO
CREATE INDEX IX_RefreshToken_User ON core.RefreshToken (UserId, ExpiresAt) WHERE RevokedAt IS NULL;
GO

CREATE TABLE core.UserDevice
(
    UserDeviceId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserDevice PRIMARY KEY,
    UserId           BIGINT        NOT NULL CONSTRAINT FK_UserDevice_User REFERENCES core.AppUser(UserId),
    Platform         VARCHAR(20)   NOT NULL,
    PushTokenHash    VARBINARY(32) NULL,
    DeviceName       NVARCHAR(120) NULL,
    LastSeenAt       DATETIME2(3)  NULL,
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_UserDevice_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_UserDevice_Platform CHECK (Platform IN ('IOS','ANDROID','WEB'))
);
GO

/* ---------------------------------------------------------------------
   Media (plan §6 / §8.5)
   --------------------------------------------------------------------- */
CREATE TABLE core.MediaAsset
(
    MediaAssetId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MediaAsset PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_MediaAsset_PublicId DEFAULT NEWSEQUENTIALID(),
    Bucket           NVARCHAR(100)  NULL,
    ObjectKey        NVARCHAR(500)  NOT NULL,
    LegacyPath       NVARCHAR(500)  NULL,
    Visibility       VARCHAR(20)    NOT NULL CONSTRAINT DF_MediaAsset_Vis DEFAULT 'PRIVATE',
    ContentType      NVARCHAR(100)  NULL,
    ByteSize         BIGINT         NULL,
    Sha256           BINARY(32)     NULL,
    WidthPx          INT            NULL,
    HeightPx         INT            NULL,
    Status           VARCHAR(20)    NOT NULL CONSTRAINT DF_MediaAsset_Status DEFAULT 'PENDING_UPLOAD',
    AltText          NVARCHAR(300)  NULL,
    UploadedByUserId BIGINT         NULL CONSTRAINT FK_MediaAsset_User REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_MediaAsset_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_MediaAsset_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_MediaAsset_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_MediaAsset_ObjectKey UNIQUE (ObjectKey),
    CONSTRAINT CK_MediaAsset_Vis CHECK (Visibility IN ('PRIVATE','PUBLIC')),
    CONSTRAINT CK_MediaAsset_Status CHECK (Status IN ('PENDING_UPLOAD','READY','REJECTED','DELETED'))
);
GO
CREATE INDEX IX_MediaAsset_Legacy ON core.MediaAsset (LegacyPath) WHERE LegacyPath IS NOT NULL;
GO

ALTER TABLE core.UserProfile ADD
    AvatarMediaId    BIGINT        NULL CONSTRAINT FK_UserProfile_Avatar REFERENCES core.MediaAsset(MediaAssetId),
    IsPublicProfile  BIT           NOT NULL CONSTRAINT DF_UserProfile_IsPublic DEFAULT 0;
GO

ALTER TABLE ugc.ShopPhoto ADD
    MediaAssetId BIGINT        NULL CONSTRAINT FK_ShopPhoto_Media REFERENCES core.MediaAsset(MediaAssetId),
    UpdatedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_ShopPhoto_UpdatedAt DEFAULT SYSUTCDATETIME();
GO

ALTER TABLE dir.Shop ADD
    LogoMediaId      BIGINT NULL CONSTRAINT FK_Shop_LogoMedia REFERENCES core.MediaAsset(MediaAssetId),
    UpdatedByUserId  BIGINT NULL CONSTRAINT FK_Shop_UpdatedBy REFERENCES core.AppUser(UserId);
GO

ALTER TABLE karma.BadgeDefinition ADD
    IconMediaId BIGINT NULL CONSTRAINT FK_Badge_IconMedia REFERENCES core.MediaAsset(MediaAssetId);
GO

ALTER TABLE dir.ShopClaimRequest ADD
    EvidenceMediaId BIGINT NULL CONSTRAINT FK_Claim_EvidenceMedia REFERENCES core.MediaAsset(MediaAssetId);
GO

/* ShopEvent.Slug + UpdatedAt are patched into the v1 CREATE in assemble.
   CoverPhoto-style FKs that needed MediaAsset wait until here. */

/* ---------------------------------------------------------------------
   Owner analytics / hours / leads (plan §8.7–8.9)
   --------------------------------------------------------------------- */
CREATE TABLE dir.ShopHours
(
    ShopHoursId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopHours PRIMARY KEY,
    ShopId       BIGINT        NOT NULL CONSTRAINT FK_ShopHours_Shop REFERENCES dir.Shop(ShopId),
    DayOfWeek    TINYINT       NOT NULL,
    OpensAt      TIME(0)       NULL,
    ClosesAt     TIME(0)       NULL,
    IsClosed     BIT           NOT NULL CONSTRAINT DF_ShopHours_Closed DEFAULT 0,
    CONSTRAINT UQ_ShopHours_Day UNIQUE (ShopId, DayOfWeek),
    CONSTRAINT CK_ShopHours_Dow CHECK (DayOfWeek BETWEEN 0 AND 6)
);
GO

CREATE TABLE dir.ShopHoursException
(
    ShopHoursExceptionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopHoursException PRIMARY KEY,
    ShopId               BIGINT        NOT NULL CONSTRAINT FK_ShopHoursEx_Shop REFERENCES dir.Shop(ShopId),
    ExceptionDate        DATE          NOT NULL,
    OpensAt              TIME(0)       NULL,
    ClosesAt             TIME(0)       NULL,
    IsClosed             BIT           NOT NULL CONSTRAINT DF_ShopHoursEx_Closed DEFAULT 1,
    Note                 NVARCHAR(200) NULL,
    CONSTRAINT UQ_ShopHoursEx UNIQUE (ShopId, ExceptionDate)
);
GO

CREATE TABLE dir.ShopContactRequest
(
    ShopContactRequestId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopContactRequest PRIMARY KEY,
    ShopId               BIGINT         NOT NULL CONSTRAINT FK_ShopContact_Shop REFERENCES dir.Shop(ShopId),
    FromUserId           BIGINT         NULL CONSTRAINT FK_ShopContact_User REFERENCES core.AppUser(UserId),
    FromName             NVARCHAR(120)  NOT NULL,
    FromEmail            NVARCHAR(320)  NOT NULL,
    FromPhone            VARCHAR(25)    NULL,
    Message              NVARCHAR(2000) NOT NULL,
    Status               VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopContact_Status DEFAULT 'NEW',
    CreatedAt            DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopContact_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ShopContact_Status CHECK (Status IN ('NEW','READ','REPLIED','SPAM','CLOSED'))
);
GO

CREATE TABLE dir.ShopDailyStat
(
    ShopId            BIGINT        NOT NULL CONSTRAINT FK_ShopDailyStat_Shop REFERENCES dir.Shop(ShopId),
    StatDate          DATE          NOT NULL,
    ListingViews      INT           NOT NULL CONSTRAINT DF_ShopDailyStat_Views DEFAULT 0,
    WebsiteClicks     INT           NOT NULL CONSTRAINT DF_ShopDailyStat_Web DEFAULT 0,
    PhoneClicks       INT           NOT NULL CONSTRAINT DF_ShopDailyStat_Phone DEFAULT 0,
    DirectionsClicks  INT           NOT NULL CONSTRAINT DF_ShopDailyStat_Dir DEFAULT 0,
    CONSTRAINT PK_ShopDailyStat PRIMARY KEY (ShopId, StatDate)
);
GO

/* ---------------------------------------------------------------------
   Logging (plan §8.4 — NCVetBiz Activity / Audit / Search / Email)
   --------------------------------------------------------------------- */
CREATE TABLE core.ActivityLog
(
    ActivityLogId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityLog PRIMARY KEY,
    UserId           BIGINT         NULL CONSTRAINT FK_ActivityLog_User REFERENCES core.AppUser(UserId),
    ActivityType     VARCHAR(40)    NOT NULL,
    EntityType       VARCHAR(40)    NULL,
    EntityId         BIGINT         NULL,
    Detail           NVARCHAR(1000) NULL,
    IpHash           BINARY(32)     NULL,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ActivityLog_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
CREATE INDEX IX_ActivityLog_Time ON core.ActivityLog (CreatedAt DESC);
CREATE INDEX IX_ActivityLog_User ON core.ActivityLog (UserId, CreatedAt DESC);
GO

CREATE TABLE core.AuditLog
(
    AuditLogId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    EntityType          VARCHAR(40)    NOT NULL,
    EntityId            BIGINT         NOT NULL,
    Action              VARCHAR(30)    NOT NULL,
    UserId              BIGINT         NULL CONSTRAINT FK_AuditLog_User REFERENCES core.AppUser(UserId),
    ImpersonatorUserId  BIGINT         NULL CONSTRAINT FK_AuditLog_Impersonator REFERENCES core.AppUser(UserId),
    ChangesJson         NVARCHAR(MAX)  NULL,
    IpHash              BINARY(32)     NULL,
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_AuditLog_Json CHECK (ChangesJson IS NULL OR ISJSON(ChangesJson) = 1)
);
GO
CREATE INDEX IX_AuditLog_Entity ON core.AuditLog (EntityType, EntityId, CreatedAt DESC);
GO

CREATE TABLE core.EmailLog
(
    EmailLogId       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailLog PRIMARY KEY,
    TemplateKey      VARCHAR(60)    NOT NULL,
    ToEmail          NVARCHAR(320)  NOT NULL,
    Subject          NVARCHAR(300)  NULL,
    Provider         VARCHAR(20)    NOT NULL CONSTRAINT DF_EmailLog_Provider DEFAULT 'SOCKETLABS',
    ProviderMessageId NVARCHAR(200) NULL,
    Status           VARCHAR(20)    NOT NULL,
    ErrorDetail      NVARCHAR(1000) NULL,
    SignupId         BIGINT         NULL,
    UserId           BIGINT         NULL CONSTRAINT FK_EmailLog_User REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_EmailLog_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_EmailLog_Status CHECK (Status IN ('QUEUED','SENT','FAILED','BOUNCED','SUPPRESSED'))
);
GO
CREATE INDEX IX_EmailLog_To ON core.EmailLog (ToEmail, CreatedAt DESC);
GO

CREATE TABLE dir.SearchLog
(
    SearchLogId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SearchLog PRIMARY KEY,
    UserId        BIGINT         NULL CONSTRAINT FK_SearchLog_User REFERENCES core.AppUser(UserId),
    QueryText     NVARCHAR(200)  NOT NULL,
    StateCode     CHAR(2)        NULL,
    ResultCount   INT            NULL,
    SessionHash   BINARY(32)     NULL,
    CreatedAt     DATETIME2(3)   NOT NULL CONSTRAINT DF_SearchLog_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE core.SiteSetting
(
    SettingKey       VARCHAR(80)    NOT NULL CONSTRAINT PK_SiteSetting PRIMARY KEY,
    SettingValue     NVARCHAR(MAX)  NOT NULL,
    ValueType        VARCHAR(20)    NOT NULL CONSTRAINT DF_SiteSetting_Type DEFAULT 'STRING',
    Description      NVARCHAR(300)  NULL,
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_SiteSetting_UpdatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedByUserId  BIGINT         NULL CONSTRAINT FK_SiteSetting_User REFERENCES core.AppUser(UserId),
    CONSTRAINT CK_SiteSetting_Type CHECK (ValueType IN ('STRING','INT','BOOL','JSON'))
);
GO

/* ---------------------------------------------------------------------
   Native signup / Preview (plan §12.8)
   --------------------------------------------------------------------- */
CREATE TABLE crm.Signup
(
    SignupId             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Signup PRIMARY KEY,
    PublicId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Signup_PublicId DEFAULT NEWSEQUENTIALID(),
    FormKey              VARCHAR(20)    NOT NULL,
    Email                NVARCHAR(320)  NOT NULL,
    EmailNormalized      NVARCHAR(320)  NOT NULL,
    FirstName            NVARCHAR(80)   NULL,
    LastName             NVARCHAR(80)   NULL,
    Phone                VARCHAR(25)    NULL,
    HobbySegment         VARCHAR(40)    NULL,
    SocialHandle         NVARCHAR(120)  NULL,
    Message              NVARCHAR(2000) NULL,
    ExtraJson            NVARCHAR(MAX)  NULL,
    Status               VARCHAR(20)    NOT NULL CONSTRAINT DF_Signup_Status DEFAULT 'PENDING_CONFIRM',
    ConfirmedAt          DATETIME2(3)   NULL,
    ConvertedUserId      BIGINT         NULL CONSTRAINT FK_Signup_User REFERENCES core.AppUser(UserId),
    MergedIntoSignupId   BIGINT         NULL CONSTRAINT FK_Signup_Merged REFERENCES crm.Signup(SignupId),
    Channel              VARCHAR(20)    NOT NULL CONSTRAINT DF_Signup_Channel DEFAULT 'WEB',
    ExternalSource       VARCHAR(40)    NULL,
    ExternalId           VARCHAR(80)    NULL,
    UtmSource            NVARCHAR(120)  NULL,
    UtmMedium            NVARCHAR(120)  NULL,
    UtmCampaign          NVARCHAR(120)  NULL,
    UtmTerm              NVARCHAR(120)  NULL,
    UtmContent           NVARCHAR(120)  NULL,
    ClickId              NVARCHAR(120)  NULL,
    Referrer             NVARCHAR(500)  NULL,
    LandingPath          NVARCHAR(200)  NULL,
    InviteCodeUsedId     BIGINT         NULL,
    ReferredByUserId     BIGINT         NULL CONSTRAINT FK_Signup_ReferredBy REFERENCES core.AppUser(UserId),
    IpHash               BINARY(32)     NULL,
    UserAgent            NVARCHAR(400)  NULL,
    CreatedAt            DATETIME2(3)   NOT NULL CONSTRAINT DF_Signup_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2(3)   NOT NULL CONSTRAINT DF_Signup_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Signup_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_Signup_Form CHECK (FormKey IN ('PREVIEW','INTEREST','FOUNDING','CONTACT')),
    CONSTRAINT CK_Signup_Status CHECK (Status IN ('PENDING_CONFIRM','CONFIRMED','UNSUBSCRIBED','BOUNCED','SPAM','MERGED','DELETED')),
    CONSTRAINT CK_Signup_Channel CHECK (Channel IN ('WEB','MAUI','IMPORT_JOTFORM','SADMIN')),
    CONSTRAINT CK_Signup_Hobby CHECK (HobbySegment IS NULL OR HobbySegment IN
        ('Collector','Local Card Shop','Dealer','Breaker','Content Creator','Grading / Authentication','Manufacturer','Other')),
    CONSTRAINT CK_Signup_Json CHECK (ExtraJson IS NULL OR ISJSON(ExtraJson) = 1)
);
GO
CREATE UNIQUE INDEX UX_Signup_EmailForm_Live ON crm.Signup (EmailNormalized, FormKey)
    WHERE Status <> 'MERGED' AND Status <> 'DELETED';
CREATE UNIQUE INDEX UX_Signup_External ON crm.Signup (ExternalSource, ExternalId)
    WHERE ExternalId IS NOT NULL;
GO

CREATE TABLE crm.EmailVerification
(
    EmailVerificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailVerification PRIMARY KEY,
    TokenHash           VARBINARY(32)  NOT NULL,
    Kind                VARCHAR(10)    NOT NULL,
    Purpose             VARCHAR(30)    NOT NULL,
    SignupId            BIGINT         NULL CONSTRAINT FK_EmailVer_Signup REFERENCES crm.Signup(SignupId),
    UserId              BIGINT         NULL CONSTRAINT FK_EmailVer_User REFERENCES core.AppUser(UserId),
    Email               NVARCHAR(320)  NOT NULL,
    ExpiresAt           DATETIME2(3)   NOT NULL,
    UsedAt              DATETIME2(3)   NULL,
    AttemptCount        TINYINT        NOT NULL CONSTRAINT DF_EmailVer_Attempts DEFAULT 0,
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_EmailVer_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_EmailVer_Token UNIQUE (TokenHash),
    CONSTRAINT CK_EmailVer_Kind CHECK (Kind IN ('LINK','CODE')),
    CONSTRAINT CK_EmailVer_Purpose CHECK (Purpose IN ('CONFIRM_SIGNUP','VERIFY_EMAIL','LOGIN','CLAIM','UNSUBSCRIBE'))
);
GO

CREATE TABLE crm.ConsentRecord
(
    ConsentRecordId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConsentRecord PRIMARY KEY,
    SignupId            BIGINT         NULL CONSTRAINT FK_Consent_Signup REFERENCES crm.Signup(SignupId),
    UserId              BIGINT         NULL CONSTRAINT FK_Consent_User REFERENCES core.AppUser(UserId),
    Email               NVARCHAR(320)  NOT NULL,
    ConsentType         VARCHAR(30)    NOT NULL,
    Granted             BIT            NOT NULL,
    ConsentTextVersion  VARCHAR(40)    NULL,
    ConsentTextHash     VARBINARY(32)  NULL,
    Source              VARCHAR(20)    NOT NULL,
    IpHash              BINARY(32)     NULL,
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_Consent_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Consent_Type CHECK (ConsentType IN ('MARKETING_EMAIL','SMS','TERMS','PRIVACY')),
    CONSTRAINT CK_Consent_Source CHECK (Source IN ('WEB','MAUI','JOTFORM_IMPORT','SADMIN'))
);
GO

CREATE TABLE crm.EmailSuppression
(
    EmailNormalized  NVARCHAR(320) NOT NULL CONSTRAINT PK_EmailSuppression PRIMARY KEY,
    Reason           VARCHAR(20)   NOT NULL,
    Source           VARCHAR(40)   NULL,
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_EmailSuppression_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByUserId  BIGINT        NULL CONSTRAINT FK_EmailSuppression_User REFERENCES core.AppUser(UserId),
    CONSTRAINT CK_EmailSuppression_Reason CHECK (Reason IN ('UNSUBSCRIBE','HARD_BOUNCE','COMPLAINT','MANUAL','SPAM'))
);
GO

CREATE TABLE crm.InviteCode
(
    InviteCodeId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InviteCode PRIMARY KEY,
    CodeHash          VARBINARY(32)  NOT NULL,
    CodeHint          VARCHAR(12)    NOT NULL,
    Kind              VARCHAR(10)    NOT NULL,
    Campaign          NVARCHAR(80)   NULL,
    IssuedToSignupId  BIGINT         NULL CONSTRAINT FK_Invite_Signup REFERENCES crm.Signup(SignupId),
    MaxUses           INT            NULL,
    UseCount          INT            NOT NULL CONSTRAINT DF_Invite_UseCount DEFAULT 0,
    ExpiresAt         DATETIME2(3)   NULL,
    RevokedAt         DATETIME2(3)   NULL,
    CreatedByUserId   BIGINT         NULL CONSTRAINT FK_Invite_CreatedBy REFERENCES core.AppUser(UserId),
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_Invite_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Invite_Hash UNIQUE (CodeHash),
    CONSTRAINT CK_Invite_Kind CHECK (Kind IN ('SHARED','PERSONAL'))
);
GO

ALTER TABLE crm.Signup ADD CONSTRAINT FK_Signup_InviteUsed
    FOREIGN KEY (InviteCodeUsedId) REFERENCES crm.InviteCode(InviteCodeId);
GO

CREATE TABLE crm.InviteRedemption
(
    InviteRedemptionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InviteRedemption PRIMARY KEY,
    InviteCodeId       BIGINT        NOT NULL CONSTRAINT FK_InviteRed_Code REFERENCES crm.InviteCode(InviteCodeId),
    SignupId           BIGINT        NULL CONSTRAINT FK_InviteRed_Signup REFERENCES crm.Signup(SignupId),
    UserId             BIGINT        NULL CONSTRAINT FK_InviteRed_User REFERENCES core.AppUser(UserId),
    IpHash             BINARY(32)    NULL,
    Channel            VARCHAR(20)   NOT NULL CONSTRAINT DF_InviteRed_Channel DEFAULT 'WEB',
    RedeemedAt         DATETIME2(3)  NOT NULL CONSTRAINT DF_InviteRed_At DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE crm.ContactMessage
(
    ContactMessageId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContactMessage PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ContactMessage_PublicId DEFAULT NEWSEQUENTIALID(),
    SignupId         BIGINT         NULL CONSTRAINT FK_ContactMessage_Signup REFERENCES crm.Signup(SignupId),
    Name             NVARCHAR(120)  NOT NULL,
    Email            NVARCHAR(320)  NOT NULL,
    Message          NVARCHAR(4000) NOT NULL,
    Status           VARCHAR(20)    NOT NULL CONSTRAINT DF_ContactMessage_Status DEFAULT 'NEW',
    AssignedToUserId BIGINT         NULL CONSTRAINT FK_ContactMessage_Assignee REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ContactMessage_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ContactMessage_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ContactMessage_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_ContactMessage_Status CHECK (Status IN ('NEW','ANSWERED','CLOSED','SPAM'))
);
GO

/* ---------------------------------------------------------------------
   Public CMS (events, news/posts, bloggers, bios, gallery, static pages)
   Used by content seeds and seo sitemap views. Slugs are unique.
   --------------------------------------------------------------------- */
CREATE TABLE cms.StaticPage
(
    PageId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaticPage PRIMARY KEY,
    Slug         NVARCHAR(120)  NOT NULL,
    LocPath      NVARCHAR(200)  NOT NULL,
    Title        NVARCHAR(200)  NOT NULL,
    SourceFile   NVARCHAR(200)  NULL,
    IsIndexed    BIT            NOT NULL CONSTRAINT DF_StaticPage_Indexed DEFAULT 1,
    UpdatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_StaticPage_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_StaticPage_Slug UNIQUE (Slug),
    CONSTRAINT UQ_StaticPage_Loc UNIQUE (LocPath)
);
GO

CREATE TABLE cms.Event
(
    EventId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CmsEvent PRIMARY KEY,
    PublicId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CmsEvent_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug         NVARCHAR(160)  NOT NULL,
    Title        NVARCHAR(200)  NOT NULL,
    Description  NVARCHAR(4000) NULL,
    EventType    VARCHAR(20)    NOT NULL,
    StartsAt     DATETIME2(3)   NOT NULL,
    EndsAt       DATETIME2(3)   NULL,
    VenueName    NVARCHAR(200)  NULL,
    City         NVARCHAR(100)  NULL,
    StateCode    CHAR(2)        NULL,
    CountryCode  CHAR(2)        NOT NULL CONSTRAINT DF_CmsEvent_Country DEFAULT 'US',
    TicketUrl    NVARCHAR(500)  NULL,
    Status       VARCHAR(20)    NOT NULL CONSTRAINT DF_CmsEvent_Status DEFAULT 'PUBLISHED',
    SourcePage   NVARCHAR(80)   NULL,
    CreatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_CmsEvent_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_CmsEvent_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_CmsEvent_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_CmsEvent_Slug UNIQUE (Slug),
    CONSTRAINT CK_CmsEvent_Type CHECK (EventType IN ('CARD_SHOW','RELEASE_DAY','TRADE_NIGHT','TOURNAMENT','SIGNING','BREAK','SALE','MEETUP','CONVENTION','EDUCATION','CHARITY','ADVOCACY','INDUSTRY','COMMUNITY','OTHER')),
    CONSTRAINT CK_CmsEvent_Status CHECK (Status IN ('DRAFT','PENDING_REVIEW','PUBLISHED','CANCELED'))
);
GO
CREATE INDEX IX_CmsEvent_Starts ON cms.Event (StartsAt) WHERE Status = 'PUBLISHED';
GO

CREATE TABLE cms.Post
(
    PostId         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CmsPost PRIMARY KEY,
    PublicId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CmsPost_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug           NVARCHAR(160)  NOT NULL,
    Title          NVARCHAR(200)  NOT NULL,
    Excerpt        NVARCHAR(1000) NULL,
    Body           NVARCHAR(MAX)  NULL,   -- HTML when rendered
    BodyMarkdown   NVARCHAR(MAX)  NULL,   -- source markdown (verbatim from inputs/posts)
    AuthorName     NVARCHAR(120)  NULL,
    AuthorOrg      NVARCHAR(80)   NULL,
    Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_CmsPost_Status DEFAULT 'PUBLISHED',
    PublishedAt    DATETIME2(3)   NULL,
    SourcePage     NVARCHAR(80)   NULL,
    CreatedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_CmsPost_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_CmsPost_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_CmsPost_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_CmsPost_Slug UNIQUE (Slug),
    CONSTRAINT CK_CmsPost_Status CHECK (Status IN ('DRAFT','PUBLISHED','ARCHIVED'))
);
GO

CREATE TABLE cms.Blogger
(
    BloggerId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Blogger PRIMARY KEY,
    Slug         NVARCHAR(160)  NOT NULL,
    Name         NVARCHAR(200)  NOT NULL,
    Description  NVARCHAR(1000) NULL,
    WebsiteUrl   NVARCHAR(500)  NOT NULL,
    LogoLegacyPath NVARCHAR(300) NULL,
    LogoMediaId  BIGINT         NULL CONSTRAINT FK_Blogger_Logo REFERENCES core.MediaAsset(MediaAssetId),
    DisplayOrder SMALLINT       NOT NULL CONSTRAINT DF_Blogger_Order DEFAULT 100,
    IsActive     BIT            NOT NULL CONSTRAINT DF_Blogger_Active DEFAULT 1,
    CreatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_Blogger_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_Blogger_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Blogger_Slug UNIQUE (Slug)
);
GO

CREATE TABLE cms.PersonBio
(
    PersonBioId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PersonBio PRIMARY KEY,
    Slug         NVARCHAR(160)  NOT NULL,
    FullName     NVARCHAR(120)  NOT NULL,
    RoleTitle    NVARCHAR(160)  NOT NULL,
    BioText      NVARCHAR(MAX)  NOT NULL,
    Quote        NVARCHAR(1000) NULL,
    PhotoLegacyPath NVARCHAR(300) NULL,
    PhotoMediaId BIGINT         NULL CONSTRAINT FK_PersonBio_Photo REFERENCES core.MediaAsset(MediaAssetId),
    DisplayOrder SMALLINT       NOT NULL CONSTRAINT DF_PersonBio_Order DEFAULT 100,
    IsPublished  BIT            NOT NULL CONSTRAINT DF_PersonBio_Pub DEFAULT 1,
    CreatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_PersonBio_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_PersonBio_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_PersonBio_Slug UNIQUE (Slug)
);
GO

CREATE TABLE cms.GalleryItem
(
    GalleryItemId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GalleryItem PRIMARY KEY,
    Slug          NVARCHAR(160)  NOT NULL,
    Caption       NVARCHAR(300)  NOT NULL,
    AltText       NVARCHAR(300)  NULL,
    LegacyPath    NVARCHAR(300)  NOT NULL,
    MediaAssetId  BIGINT         NULL CONSTRAINT FK_GalleryItem_Media REFERENCES core.MediaAsset(MediaAssetId),
    DisplayOrder  SMALLINT       NOT NULL,
    IsPublished   BIT            NOT NULL CONSTRAINT DF_GalleryItem_Pub DEFAULT 1,
    CreatedAt     DATETIME2(3)   NOT NULL CONSTRAINT DF_GalleryItem_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(3)   NOT NULL CONSTRAINT DF_GalleryItem_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_GalleryItem_Slug UNIQUE (Slug)
);
GO

/* ---------------------------------------------------------------------
   Beginner resource: trading-card lingo + raw-card condition scale.
   Filled from the "New to Trading Cards" page on main once it lands
   (legacy-site/new-to-trading-cards.html or similar). Lives in cms
   (same public-content schema as posts/pages), not a separate content
   schema. Do not invent terms or grades — generate_seed reads the page.
   --------------------------------------------------------------------- */
CREATE TABLE cms.GlossaryTerm
(
    GlossaryTermId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GlossaryTerm PRIMARY KEY,
    Slug           NVARCHAR(160)  NOT NULL,
    Term           NVARCHAR(120)  NOT NULL,
    Definition     NVARCHAR(2000) NOT NULL,
    SortOrder      SMALLINT       NOT NULL CONSTRAINT DF_GlossaryTerm_Sort DEFAULT 100,
    SourceUrl      NVARCHAR(500)  NULL,
    SourcePage     NVARCHAR(120)  NULL,
    IsPublished    BIT            NOT NULL CONSTRAINT DF_GlossaryTerm_Pub DEFAULT 1,
    CreatedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_GlossaryTerm_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_GlossaryTerm_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_GlossaryTerm_Slug UNIQUE (Slug)
);
GO
CREATE INDEX IX_GlossaryTerm_Sort ON cms.GlossaryTerm (SortOrder, Term);
GO

CREATE TABLE cms.ConditionGrade
(
    ConditionGradeId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConditionGrade PRIMARY KEY,
    Slug             NVARCHAR(80)   NOT NULL,
    Name             NVARCHAR(80)   NOT NULL,
    Rank             TINYINT        NOT NULL,  -- 1 = best (Pristine), higher = worse (Poor)
    Description      NVARCHAR(2000) NOT NULL,
    SourcePage       NVARCHAR(120)  NULL,
    IsPublished      BIT            NOT NULL CONSTRAINT DF_ConditionGrade_Pub DEFAULT 1,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ConditionGrade_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_ConditionGrade_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ConditionGrade_Slug UNIQUE (Slug),
    CONSTRAINT UQ_ConditionGrade_Rank UNIQUE (Rank),
    CONSTRAINT UQ_ConditionGrade_Name UNIQUE (Name)
);
GO

/* ---------------------------------------------------------------------
   SEO sitemap views — loc path + lastmod, public content only
   --------------------------------------------------------------------- */
CREATE VIEW seo.vw_SitemapStatic
AS
SELECT  p.LocPath,
        p.UpdatedAt AS LastModUtc,
        p.Title,
        p.Slug
FROM    cms.StaticPage AS p
WHERE   p.IsIndexed = 1;
GO

CREATE VIEW seo.vw_SitemapShops
AS
SELECT  CONCAT(N'/shops/', LOWER(s.StateCode), N'/',
               LOWER(REPLACE(REPLACE(s.City, N' ', N'-'), N'.', N'')), N'/',
               s.Slug) AS LocPath,
        s.UpdatedAt AS LastModUtc,
        s.StateCode,
        s.Slug,
        s.Name,
        s.City
FROM    dir.Shop AS s
WHERE   s.ListingStatus = 'ACTIVE'
  AND   s.Slug IS NOT NULL;
GO

CREATE VIEW seo.vw_SitemapEvents
AS
SELECT  CONCAT(N'/events/', e.Slug) AS LocPath,
        e.UpdatedAt AS LastModUtc,
        e.StateCode,
        e.Slug,
        e.Title,
        e.StartsAt
FROM    cms.Event AS e
WHERE   e.Status = 'PUBLISHED'
UNION ALL
SELECT  CONCAT(N'/shops/', LOWER(s.StateCode), N'/',
               LOWER(REPLACE(REPLACE(s.City, N' ', N'-'), N'.', N'')), N'/',
               s.Slug, N'/events/', ev.Slug) AS LocPath,
        ev.UpdatedAt AS LastModUtc,
        s.StateCode,
        ev.Slug,
        ev.Title,
        ev.StartsAt
FROM    mkt.ShopEvent AS ev
JOIN    dir.Shop AS s ON s.ShopId = ev.ShopId
WHERE   ev.Status = 'PUBLISHED'
  AND   s.ListingStatus = 'ACTIVE'
  AND   ev.Slug IS NOT NULL;
GO

CREATE VIEW seo.vw_SitemapPosts
AS
SELECT  CONCAT(N'/news/', p.Slug) AS LocPath,
        p.UpdatedAt AS LastModUtc,
        p.Slug,
        p.Title
FROM    cms.Post AS p
WHERE   p.Status = 'PUBLISHED'
  AND   p.PublishedAt IS NOT NULL;
GO

CREATE VIEW seo.vw_SitemapBloggers
AS
SELECT  CONCAT(N'/bloggers/', b.Slug) AS LocPath,
        b.UpdatedAt AS LastModUtc,
        b.Slug,
        b.Name
FROM    cms.Blogger AS b
WHERE   b.IsActive = 1;
GO

CREATE VIEW seo.vw_SitemapProfiles
AS
SELECT  CONCAT(N'/members/', u.Username) AS LocPath,
        p.UpdatedAt AS LastModUtc,
        u.Username,
        u.DisplayName
FROM    core.UserProfile AS p
JOIN    core.AppUser AS u ON u.UserId = p.UserId
WHERE   p.IsPublicProfile = 1
  AND   u.AccountStatus = 'ACTIVE'
  AND   (p.ShowKarma = 1 OR p.ShowBadges = 1);
GO

CREATE VIEW seo.vw_SitemapImages
AS
SELECT  CONCAT(N'/shops/', LOWER(s.StateCode), N'/',
               LOWER(REPLACE(REPLACE(s.City, N' ', N'-'), N'.', N'')), N'/',
               s.Slug) AS PageLocPath,
        COALESCE(m.LegacyPath, m.ObjectKey) AS ImageLocPath,
        COALESCE(ph.UpdatedAt, m.UpdatedAt) AS LastModUtc,
        s.Name AS ImageTitle,
        CAST(N'shop' AS VARCHAR(20)) AS ImageKind
FROM    ugc.ShopPhoto AS ph
JOIN    dir.Shop AS s ON s.ShopId = ph.ShopId
LEFT JOIN core.MediaAsset AS m ON m.MediaAssetId = ph.MediaAssetId
WHERE   ph.ModerationStatus = 'APPROVED'
  AND   s.ListingStatus = 'ACTIVE'
UNION ALL
SELECT  CONCAT(N'/gallery#', g.Slug) AS PageLocPath,
        COALESCE(m.LegacyPath, g.LegacyPath) AS ImageLocPath,
        g.UpdatedAt AS LastModUtc,
        g.Caption AS ImageTitle,
        CAST(N'gallery' AS VARCHAR(20)) AS ImageKind
FROM    cms.GalleryItem AS g
LEFT JOIN core.MediaAsset AS m ON m.MediaAssetId = g.MediaAssetId
WHERE   g.IsPublished = 1;
GO

/* ---------------------------------------------------------------------
   Owner listing edits that SADMIN must approve (plan /SADMIN queues)
   --------------------------------------------------------------------- */
CREATE TABLE dir.ShopEditRequest
(
    ShopEditRequestId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopEditRequest PRIMARY KEY,
    PublicId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ShopEdit_PublicId DEFAULT NEWSEQUENTIALID(),
    ShopId            BIGINT         NOT NULL CONSTRAINT FK_ShopEdit_Shop REFERENCES dir.Shop(ShopId),
    RequestedByUserId BIGINT         NOT NULL CONSTRAINT FK_ShopEdit_User REFERENCES core.AppUser(UserId),
    ProposedJson      NVARCHAR(MAX)  NOT NULL,
    Status            VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopEdit_Status DEFAULT 'PENDING_REVIEW',
    ReviewedByUserId  BIGINT         NULL CONSTRAINT FK_ShopEdit_ReviewedBy REFERENCES core.AppUser(UserId),
    ReviewedAt        DATETIME2(3)   NULL,
    DecisionReason    NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEdit_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEdit_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ShopEdit_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_ShopEdit_Status CHECK (Status IN ('PENDING_REVIEW','APPROVED','REJECTED','WITHDRAWN')),
    CONSTRAINT CK_ShopEdit_Json CHECK (ISJSON(ProposedJson) = 1)
);
GO
CREATE INDEX IX_ShopEdit_Queue ON dir.ShopEditRequest (Status, CreatedAt);
GO

/* ---------------------------------------------------------------------
   Hobby / industry figures (influencers, executives) for SADMIN review.
   Public pages only show IsPublished = 1. Staff drafts stay off the site.
   --------------------------------------------------------------------- */
CREATE TABLE cms.IndustryFigure
(
    IndustryFigureId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IndustryFigure PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_IndustryFigure_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug             NVARCHAR(160)  NOT NULL,
    FullName         NVARCHAR(160)  NOT NULL,
    AlsoKnownAs      NVARCHAR(80)   NULL,
    RoleTitle        NVARCHAR(200)  NULL,
    Summary          NVARCHAR(2000) NULL,
    WebsiteUrl       NVARCHAR(500)  NULL,
    SourceNotes      NVARCHAR(1000) NULL,
    IsPublished      BIT            NOT NULL CONSTRAINT DF_IndustryFigure_Pub DEFAULT 0,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_IndustryFigure_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_IndustryFigure_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_IndustryFigure_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_IndustryFigure_Slug UNIQUE (Slug)
);
GO

CREATE TABLE cms.IndustryFigureSource
(
    IndustryFigureSourceId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IndustryFigureSource PRIMARY KEY,
    IndustryFigureId       BIGINT         NOT NULL CONSTRAINT FK_FigureSource_Figure REFERENCES cms.IndustryFigure(IndustryFigureId),
    SortOrder              SMALLINT       NOT NULL,
    Label                  NVARCHAR(300)  NOT NULL,
    Url                    NVARCHAR(500)  NOT NULL,
    RetrievedOn            DATE           NULL
);
GO
CREATE INDEX IX_FigureSource_Figure ON cms.IndustryFigureSource (IndustryFigureId, SortOrder);
GO

ALTER TABLE cms.Post ADD
    IndustryFigureId BIGINT NULL CONSTRAINT FK_CmsPost_Figure REFERENCES cms.IndustryFigure(IndustryFigureId);
GO

/* ---------------------------------------------------------------------
   Shop impression analytics (all tiers, including Free / unclaimed).
   Raw events live here. feed.FeedImpression stays rotation-feed-only
   (FK to NewShopFeedEntry). dir.ShopDailyStat is refreshed by the rollup.
   No seed rows — counts come from live traffic only.
   --------------------------------------------------------------------- */
CREATE SCHEMA analytics;
GO

CREATE TABLE analytics.SessionSalt
(
    SaltDate  DATE          NOT NULL CONSTRAINT PK_SessionSalt PRIMARY KEY,
    Salt      VARBINARY(32) NOT NULL,
    CreatedAt DATETIME2(3)  NOT NULL CONSTRAINT DF_SessionSalt_Created DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE analytics.ShopImpression
(
    ImpressionId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopImpression PRIMARY KEY,
    ShopId        BIGINT         NOT NULL CONSTRAINT FK_ShopImpression_Shop REFERENCES dir.Shop(ShopId),
    Surface       VARCHAR(40)    NOT NULL,
    Position      SMALLINT       NULL,
    PageUrl       NVARCHAR(400)  NULL,
    SessionHash   BINARY(32)     NULL,
    IsBot         BIT            NOT NULL CONSTRAINT DF_ShopImpression_Bot DEFAULT 0,
    CreatedAt     DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopImpression_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ShopImpression_Surface CHECK (Surface IN (
        'DIRECTORY_LIST','DIRECTORY_SEARCH','DIRECTORY_STATE',
        'HOME_ROTATION','FEATURED','SHOW_EXHIBITOR','MAP_PIN',
        'API_APP','DETAIL',
        'CLICK_WEBSITE','CLICK_PHONE','CLICK_DIRECTIONS','CLICK_SOCIAL'))
);
GO
CREATE INDEX IX_ShopImpression_ShopTime ON analytics.ShopImpression (ShopId, CreatedAt) INCLUDE (Surface, IsBot, Position);
CREATE INDEX IX_ShopImpression_Time ON analytics.ShopImpression (CreatedAt) INCLUDE (ShopId, Surface, IsBot);
GO

CREATE TABLE analytics.ShopDailyStats
(
    ShopId        BIGINT        NOT NULL CONSTRAINT FK_ShopDailyStats_Shop REFERENCES dir.Shop(ShopId),
    StatDate      DATE          NOT NULL,
    Surface       VARCHAR(40)   NOT NULL,
    Impressions   INT           NOT NULL CONSTRAINT DF_ShopDailyStats_Imp DEFAULT 0,
    Views         INT           NOT NULL CONSTRAINT DF_ShopDailyStats_Views DEFAULT 0,
    Clicks        INT           NOT NULL CONSTRAINT DF_ShopDailyStats_Clicks DEFAULT 0,
    BotEvents     INT           NOT NULL CONSTRAINT DF_ShopDailyStats_Bot DEFAULT 0,
    RolledUpAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_ShopDailyStats_Roll DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ShopDailyStats PRIMARY KEY (ShopId, StatDate, Surface)
);
GO
CREATE INDEX IX_ShopDailyStats_Date ON analytics.ShopDailyStats (StatDate, Surface) INCLUDE (Impressions, Views, Clicks);
GO

CREATE PROCEDURE analytics.usp_RollupShopDailyStats
    @FromUtc DATETIME2(3) = NULL,
    @ToUtc   DATETIME2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FromUtc IS NULL SET @FromUtc = DATEADD(DAY, -2, SYSUTCDATETIME());
    IF @ToUtc   IS NULL SET @ToUtc   = DATEADD(DAY, 1, SYSUTCDATETIME());

    DECLARE @FromDate DATE = CAST(@FromUtc AS DATE);
    DECLARE @ToDate   DATE = CAST(@ToUtc AS DATE);

    ;WITH src AS (
        SELECT  i.ShopId,
                CAST(i.CreatedAt AS DATE) AS StatDate,
                i.Surface,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface NOT LIKE 'CLICK_%' AND i.Surface <> 'DETAIL' THEN 1 ELSE 0 END) AS Impressions,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface = 'DETAIL' THEN 1 ELSE 0 END) AS Views,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface LIKE 'CLICK_%' THEN 1 ELSE 0 END) AS Clicks,
                SUM(CASE WHEN i.IsBot = 1 THEN 1 ELSE 0 END) AS BotEvents
        FROM    analytics.ShopImpression AS i
        WHERE   CAST(i.CreatedAt AS DATE) >= @FromDate
          AND   CAST(i.CreatedAt AS DATE) <= @ToDate
        GROUP BY i.ShopId, CAST(i.CreatedAt AS DATE), i.Surface
    )
    MERGE analytics.ShopDailyStats AS t
    USING src AS s
    ON t.ShopId = s.ShopId AND t.StatDate = s.StatDate AND t.Surface = s.Surface
    WHEN MATCHED THEN UPDATE SET
        Impressions = s.Impressions,
        Views       = s.Views,
        Clicks      = s.Clicks,
        BotEvents   = s.BotEvents,
        RolledUpAt  = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (ShopId, StatDate, Surface, Impressions, Views, Clicks, BotEvents)
    VALUES (s.ShopId, s.StatDate, s.Surface, s.Impressions, s.Views, s.Clicks, s.BotEvents);

    ;WITH daytot AS (
        SELECT  i.ShopId,
                CAST(i.CreatedAt AS DATE) AS StatDate,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface = 'DETAIL' THEN 1 ELSE 0 END) AS ListingViews,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface = 'CLICK_WEBSITE' THEN 1 ELSE 0 END) AS WebsiteClicks,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface = 'CLICK_PHONE' THEN 1 ELSE 0 END) AS PhoneClicks,
                SUM(CASE WHEN i.IsBot = 0 AND i.Surface = 'CLICK_DIRECTIONS' THEN 1 ELSE 0 END) AS DirectionsClicks
        FROM    analytics.ShopImpression AS i
        WHERE   CAST(i.CreatedAt AS DATE) >= @FromDate
          AND   CAST(i.CreatedAt AS DATE) <= @ToDate
        GROUP BY i.ShopId, CAST(i.CreatedAt AS DATE)
    )
    MERGE dir.ShopDailyStat AS t
    USING daytot AS s
    ON t.ShopId = s.ShopId AND t.StatDate = s.StatDate
    WHEN MATCHED THEN UPDATE SET
        ListingViews     = s.ListingViews,
        WebsiteClicks    = s.WebsiteClicks,
        PhoneClicks      = s.PhoneClicks,
        DirectionsClicks = s.DirectionsClicks
    WHEN NOT MATCHED THEN INSERT (ShopId, StatDate, ListingViews, WebsiteClicks, PhoneClicks, DirectionsClicks)
    VALUES (s.ShopId, s.StatDate, s.ListingViews, s.WebsiteClicks, s.PhoneClicks, s.DirectionsClicks);
END
GO

/* ---------------------------------------------------------------------
   AI concierge — VIP assistant visitors. UA catalog is SADMIN-editable.
   Visits/inquiries store anonymized intent only (no name/email/phone/IP).
   --------------------------------------------------------------------- */
CREATE TABLE analytics.AgentUaRule
(
    AgentUaRuleId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AgentUaRule PRIMARY KEY,
    Token          NVARCHAR(80)  NOT NULL,
    AssistantName  NVARCHAR(80)  NOT NULL,
    Role           VARCHAR(20)   NOT NULL,
    IsEnabled      BIT           NOT NULL CONSTRAINT DF_AgentUaRule_On DEFAULT 1,
    SortOrder      SMALLINT      NOT NULL CONSTRAINT DF_AgentUaRule_Sort DEFAULT 100,
    Notes          NVARCHAR(300) NULL,
    CreatedAt      DATETIME2(3)  NOT NULL CONSTRAINT DF_AgentUaRule_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2(3)  NOT NULL CONSTRAINT DF_AgentUaRule_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_AgentUaRule_Token UNIQUE (Token),
    CONSTRAINT CK_AgentUaRule_Role CHECK (Role IN ('USER_ASSISTANT','SEARCH','TRAINING'))
);
GO
CREATE INDEX IX_AgentUaRule_Enabled ON analytics.AgentUaRule (IsEnabled, SortOrder);
GO

CREATE TABLE analytics.AgentVisit
(
    AgentVisitId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AgentVisit PRIMARY KEY,
    AssistantName   NVARCHAR(80)  NOT NULL,
    Role            VARCHAR(20)   NOT NULL,
    IsVerified      BIT           NOT NULL CONSTRAINT DF_AgentVisit_Verified DEFAULT 0,
    PagePath        NVARCHAR(200) NULL,
    CreatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_AgentVisit_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_AgentVisit_Role CHECK (Role IN ('USER_ASSISTANT','SEARCH','TRAINING'))
);
GO
CREATE INDEX IX_AgentVisit_Time ON analytics.AgentVisit (CreatedAt DESC) INCLUDE (AssistantName, Role);
CREATE INDEX IX_AgentVisit_Assistant ON analytics.AgentVisit (AssistantName, CreatedAt DESC);
GO

CREATE TABLE analytics.AgentInquiry
(
    AgentInquiryId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AgentInquiry PRIMARY KEY,
    AssistantName    NVARCHAR(80)  NULL,
    HumanRole        VARCHAR(40)   NULL,
    StateCode        CHAR(2)       NULL,
    Zip3             CHAR(3)       NULL,
    Interests        NVARCHAR(200) NULL,
    QuestionExcerpt  NVARCHAR(240) NULL,
    IsVerified       BIT           NOT NULL CONSTRAINT DF_AgentInquiry_Verified DEFAULT 0,
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_AgentInquiry_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_AgentInquiry_Role CHECK (HumanRole IS NULL OR HumanRole IN
        ('collector','parent','shop_owner','promoter','dealer','media'))
);
GO
CREATE INDEX IX_AgentInquiry_Time ON analytics.AgentInquiry (CreatedAt DESC);
CREATE INDEX IX_AgentInquiry_Role ON analytics.AgentInquiry (HumanRole, CreatedAt DESC);
GO
