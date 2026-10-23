/* =====================================================================
   NTCA — tradingcardsassociation.com
   Database schema v1 (Microsoft SQL Server 2019+)
   Domains: core (users) | dir (shops, tiers, claims) | bill (subscriptions)
            mkt (premium: calendar, promotion, mailbox parsing, FB/IG monitoring)
            feed (new-shops rotation) | ugc (photos, reviews, feedback, votes)
            karma (points ledger, action rules, badges, perks)
   Conventions: all timestamps UTC DATETIME2(3); surrogate keys BIGINT/INT
   IDENTITY; PublicId UNIQUEIDENTIFIER for anything exposed in URLs/APIs.
   Draft 2026-10-22. No prior NTCA schema existed; designed fresh.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE SCHEMA core;
GO
CREATE SCHEMA dir;
GO
CREATE SCHEMA bill;
GO
CREATE SCHEMA mkt;
GO
CREATE SCHEMA feed;
GO
CREATE SCHEMA ugc;
GO
CREATE SCHEMA karma;
GO

/* =====================================================================
   1. USERS / PROFILES
   ===================================================================== */
CREATE TABLE core.AppUser
(
    UserId           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AppUser_PublicId DEFAULT NEWSEQUENTIALID(),
    Email            NVARCHAR(320)  NOT NULL,
    EmailVerifiedAt  DATETIME2(3)   NULL,
    Username         NVARCHAR(40)   NOT NULL,
    DisplayName      NVARCHAR(80)   NOT NULL,
    AccountStatus    VARCHAR(20)    NOT NULL CONSTRAINT DF_AppUser_Status DEFAULT 'ACTIVE',
    -- Suggested: karma standing lets moderators freeze earning without banning the account
    KarmaStatus      VARCHAR(20)    NOT NULL CONSTRAINT DF_AppUser_KarmaStatus DEFAULT 'NORMAL',
    MembershipLevel  VARCHAR(20)    NOT NULL CONSTRAINT DF_AppUser_Membership DEFAULT 'SUPPORTER', -- NTCA supporter tiers (join.html)
    IsStaff          BIT            NOT NULL CONSTRAINT DF_AppUser_IsStaff DEFAULT 0,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT SYSUTCDATETIME(),
    LastLoginAt      DATETIME2(3)   NULL,
    CONSTRAINT UQ_AppUser_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_AppUser_Email    UNIQUE (Email),
    CONSTRAINT UQ_AppUser_Username UNIQUE (Username),
    CONSTRAINT CK_AppUser_Status CHECK (AccountStatus IN ('ACTIVE','SUSPENDED','BANNED','DELETED')),
    CONSTRAINT CK_AppUser_KarmaStatus CHECK (KarmaStatus IN ('NORMAL','PROBATION','FROZEN'))
);
GO

CREATE TABLE core.UserProfile
(
    UserId           BIGINT         NOT NULL CONSTRAINT PK_UserProfile PRIMARY KEY
                                    CONSTRAINT FK_UserProfile_User REFERENCES core.AppUser(UserId),
    Bio              NVARCHAR(1000) NULL,
    AvatarUrl        NVARCHAR(500)  NULL,
    HomeCity         NVARCHAR(100)  NULL,
    HomeState        CHAR(2)        NULL,
    FavoriteHobbies  NVARCHAR(200)  NULL,
    ShowKarma        BIT            NOT NULL CONSTRAINT DF_UserProfile_ShowKarma  DEFAULT 1,
    ShowBadges       BIT            NOT NULL CONSTRAINT DF_UserProfile_ShowBadges DEFAULT 1,
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_UserProfile_UpdatedAt DEFAULT SYSUTCDATETIME()
);
GO

-- Suggested: site roles (moderator/admin) kept separate from shop roles
CREATE TABLE core.UserRole
(
    UserId    BIGINT       NOT NULL CONSTRAINT FK_UserRole_User REFERENCES core.AppUser(UserId),
    RoleCode  VARCHAR(20)  NOT NULL,
    GrantedAt DATETIME2(3) NOT NULL CONSTRAINT DF_UserRole_GrantedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UserRole PRIMARY KEY (UserId, RoleCode),
    CONSTRAINT CK_UserRole_Code CHECK (RoleCode IN ('ADMIN','MODERATOR','EDITOR'))
);
GO

/* =====================================================================
   2. SHOP DIRECTORY + THREE TIERS (entitlements as data)
   ===================================================================== */
CREATE TABLE dir.ShopTier
(
    TierId            TINYINT       NOT NULL CONSTRAINT PK_ShopTier PRIMARY KEY,
    TierCode          VARCHAR(20)   NOT NULL,
    Name              NVARCHAR(50)  NOT NULL,
    MonthlyPriceCents INT           NULL,    -- NULL = price not set yet
    AnnualPriceCents  INT           NULL,
    SortOrder         TINYINT       NOT NULL,
    IsActive          BIT           NOT NULL CONSTRAINT DF_ShopTier_IsActive DEFAULT 1,
    CONSTRAINT UQ_ShopTier_Code UNIQUE (TierCode),
    CONSTRAINT CK_ShopTier_Price CHECK (ISNULL(MonthlyPriceCents,0) >= 0 AND ISNULL(AnnualPriceCents,0) >= 0)
);
GO

CREATE TABLE dir.Feature
(
    FeatureCode  VARCHAR(50)   NOT NULL CONSTRAINT PK_Feature PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(400) NULL
);
GO

CREATE TABLE dir.TierFeature
(
    TierId       TINYINT     NOT NULL CONSTRAINT FK_TierFeature_Tier REFERENCES dir.ShopTier(TierId),
    FeatureCode  VARCHAR(50) NOT NULL CONSTRAINT FK_TierFeature_Feature REFERENCES dir.Feature(FeatureCode),
    IsEnabled    BIT         NOT NULL CONSTRAINT DF_TierFeature_IsEnabled DEFAULT 1,
    LimitValue   INT         NULL,     -- e.g. campaigns/month; NULL = unlimited
    CONSTRAINT PK_TierFeature PRIMARY KEY (TierId, FeatureCode)
);
GO

CREATE TABLE dir.Shop
(
    ShopId            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shop PRIMARY KEY,
    PublicId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Shop_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug              NVARCHAR(120)  NOT NULL,
    Name              NVARCHAR(200)  NOT NULL,
    AddressLine1      NVARCHAR(200)  NULL,
    AddressLine2      NVARCHAR(200)  NULL,
    City              NVARCHAR(100)  NOT NULL,
    StateCode         CHAR(2)        NOT NULL,
    PostalCode        VARCHAR(10)    NULL,
    CountryCode       CHAR(2)        NOT NULL CONSTRAINT DF_Shop_Country DEFAULT 'US',
    Latitude          DECIMAL(9,6)   NULL,
    Longitude         DECIMAL(9,6)   NULL,
    Phone             VARCHAR(25)    NULL,
    -- Links are always stored; display is gated by tier (see dir.vw_ShopPublicListing)
    WebsiteUrl        NVARCHAR(500)  NULL,
    PublicEmail       NVARCHAR(320)  NULL,
    FacebookUrl       NVARCHAR(500)  NULL,
    InstagramHandle   NVARCHAR(100)  NULL,
    Description       NVARCHAR(2000) NULL,
    PrimarySpecialty  VARCHAR(20)    NULL,
    CurrentTierId     TINYINT        NOT NULL CONSTRAINT DF_Shop_Tier DEFAULT 1
                                     CONSTRAINT FK_Shop_Tier REFERENCES dir.ShopTier(TierId),
    ClaimStatus       VARCHAR(20)    NOT NULL CONSTRAINT DF_Shop_ClaimStatus DEFAULT 'UNCLAIMED',
    ListingStatus     VARCHAR(20)    NOT NULL CONSTRAINT DF_Shop_ListingStatus DEFAULT 'PENDING',
    Source            NVARCHAR(100)  NULL,   -- 'Beckett Marketplace', 'Craig Excel import', 'User suggestion'
    SourceRef         NVARCHAR(200)  NULL,
    SuggestedByUserId BIGINT         NULL CONSTRAINT FK_Shop_SuggestedBy REFERENCES core.AppUser(UserId),
    CoverPhotoId      BIGINT         NULL,   -- FK added after ugc.ShopPhoto exists
    PublishedAt       DATETIME2(3)   NULL,   -- drives the new-shops rotation feed
    ClaimedAt         DATETIME2(3)   NULL,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_Shop_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_Shop_UpdatedAt DEFAULT SYSUTCDATETIME(),
    RowVer            ROWVERSION     NOT NULL,
    CONSTRAINT UQ_Shop_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Shop_Slug     UNIQUE (Slug),
    CONSTRAINT CK_Shop_ClaimStatus   CHECK (ClaimStatus IN ('UNCLAIMED','PENDING','CLAIMED')),
    CONSTRAINT CK_Shop_ListingStatus CHECK (ListingStatus IN ('PENDING','ACTIVE','HIDDEN','CLOSED')),
    CONSTRAINT CK_Shop_Specialty     CHECK (PrimarySpecialty IS NULL OR PrimarySpecialty IN ('SPORTS','POKEMON','MAGIC','TCG_MIXED','NONSPORT','ALL')),
    CONSTRAINT CK_Shop_Lat CHECK (Latitude  IS NULL OR Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_Shop_Lng CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);
GO
CREATE INDEX IX_Shop_State_City ON dir.Shop (StateCode, City) INCLUDE (Name, CurrentTierId, ListingStatus);
CREATE INDEX IX_Shop_Published  ON dir.Shop (PublishedAt) WHERE ListingStatus = 'ACTIVE';
CREATE INDEX IX_Shop_Tier       ON dir.Shop (CurrentTierId, ClaimStatus);
GO

-- Shop owners / managers (result of an approved claim). Used to block self-karma.
CREATE TABLE dir.ShopMember
(
    ShopMemberId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopMember PRIMARY KEY,
    ShopId          BIGINT       NOT NULL CONSTRAINT FK_ShopMember_Shop REFERENCES dir.Shop(ShopId),
    UserId          BIGINT       NOT NULL CONSTRAINT FK_ShopMember_User REFERENCES core.AppUser(UserId),
    MemberRole      VARCHAR(20)  NOT NULL,
    ClaimRequestId  BIGINT       NULL,     -- FK added below
    GrantedAt       DATETIME2(3) NOT NULL CONSTRAINT DF_ShopMember_GrantedAt DEFAULT SYSUTCDATETIME(),
    GrantedByUserId BIGINT       NULL CONSTRAINT FK_ShopMember_GrantedBy REFERENCES core.AppUser(UserId),
    RevokedAt       DATETIME2(3) NULL,
    CONSTRAINT CK_ShopMember_Role CHECK (MemberRole IN ('OWNER','MANAGER','STAFF'))
);
GO
CREATE UNIQUE INDEX UX_ShopMember_ActiveUser ON dir.ShopMember (ShopId, UserId) WHERE RevokedAt IS NULL;
CREATE UNIQUE INDEX UX_ShopMember_OneOwner   ON dir.ShopMember (ShopId) WHERE RevokedAt IS NULL AND MemberRole = 'OWNER';
CREATE INDEX IX_ShopMember_User ON dir.ShopMember (UserId) INCLUDE (ShopId, MemberRole) WHERE RevokedAt IS NULL;
GO

-- Claim / adoption workflow
CREATE TABLE dir.ShopClaimRequest
(
    ClaimRequestId       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopClaimRequest PRIMARY KEY,
    ShopId               BIGINT         NOT NULL CONSTRAINT FK_Claim_Shop REFERENCES dir.Shop(ShopId),
    RequestedByUserId    BIGINT         NOT NULL CONSTRAINT FK_Claim_User REFERENCES core.AppUser(UserId),
    ClaimType            VARCHAR(20)    NOT NULL CONSTRAINT DF_Claim_Type DEFAULT 'OWNER', -- OWNER, or community ADOPTER (Suggested)
    Status               VARCHAR(20)    NOT NULL CONSTRAINT DF_Claim_Status DEFAULT 'PENDING',
    VerificationMethod   VARCHAR(20)    NULL,
    VerificationCodeHash VARBINARY(32)  NULL,  -- hash of code sent by phone/postcard/email; never plaintext
    VerificationSentAt   DATETIME2(3)   NULL,
    VerifiedAt           DATETIME2(3)   NULL,
    ContactName          NVARCHAR(120)  NULL,
    ContactPhone         VARCHAR(25)    NULL,
    ContactEmail         NVARCHAR(320)  NULL,
    EvidenceUrl          NVARCHAR(500)  NULL,
    ReferredByUserId     BIGINT         NULL CONSTRAINT FK_Claim_ReferredBy REFERENCES core.AppUser(UserId), -- member who brought the owner in (earns karma)
    Notes                NVARCHAR(1000) NULL,
    SubmittedAt          DATETIME2(3)   NOT NULL CONSTRAINT DF_Claim_SubmittedAt DEFAULT SYSUTCDATETIME(),
    ReviewedByUserId     BIGINT         NULL CONSTRAINT FK_Claim_ReviewedBy REFERENCES core.AppUser(UserId),
    ReviewedAt           DATETIME2(3)   NULL,
    DecisionReason       NVARCHAR(500)  NULL,
    CONSTRAINT CK_Claim_Type   CHECK (ClaimType IN ('OWNER','ADOPTER')),
    CONSTRAINT CK_Claim_Status CHECK (Status IN ('PENDING','VERIFYING','APPROVED','REJECTED','WITHDRAWN','EXPIRED')),
    CONSTRAINT CK_Claim_Method CHECK (VerificationMethod IS NULL OR VerificationMethod IN ('EMAIL_DOMAIN','PHONE_CALL','POSTCARD_CODE','DOCUMENT','SOCIAL_DM','ADMIN')),
    CONSTRAINT CK_Claim_NoSelfReferral CHECK (ReferredByUserId IS NULL OR ReferredByUserId <> RequestedByUserId)
);
GO
CREATE UNIQUE INDEX UX_Claim_OneOpenPerUserShop ON dir.ShopClaimRequest (ShopId, RequestedByUserId)
    WHERE Status IN ('PENDING','VERIFYING');
CREATE INDEX IX_Claim_Queue ON dir.ShopClaimRequest (Status, SubmittedAt);
GO
ALTER TABLE dir.ShopMember ADD CONSTRAINT FK_ShopMember_Claim FOREIGN KEY (ClaimRequestId) REFERENCES dir.ShopClaimRequest(ClaimRequestId);
GO
/* =====================================================================
   3. SUBSCRIPTIONS / BILLING HISTORY (Stripe)
   ===================================================================== */
CREATE TABLE bill.ShopSubscription
(
    SubscriptionId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopSubscription PRIMARY KEY,
    ShopId                  BIGINT        NOT NULL CONSTRAINT FK_Sub_Shop REFERENCES dir.Shop(ShopId),
    TierId                  TINYINT       NOT NULL CONSTRAINT FK_Sub_Tier REFERENCES dir.ShopTier(TierId),
    PayerUserId             BIGINT        NULL CONSTRAINT FK_Sub_Payer REFERENCES core.AppUser(UserId),
    Status                  VARCHAR(20)   NOT NULL,
    BillingInterval         VARCHAR(10)   NOT NULL,
    StartedAt               DATETIME2(3)  NOT NULL CONSTRAINT DF_Sub_StartedAt DEFAULT SYSUTCDATETIME(),
    CurrentPeriodStart      DATETIME2(3)  NULL,
    CurrentPeriodEnd        DATETIME2(3)  NULL,
    CancelAtPeriodEnd       BIT           NOT NULL CONSTRAINT DF_Sub_CancelAtEnd DEFAULT 0,
    CanceledAt              DATETIME2(3)  NULL,
    EndedAt                 DATETIME2(3)  NULL,
    Provider                VARCHAR(20)   NOT NULL CONSTRAINT DF_Sub_Provider DEFAULT 'STRIPE',
    ExternalCustomerId      VARCHAR(100)  NULL,
    ExternalSubscriptionId  VARCHAR(100)  NULL,
    CreatedAt               DATETIME2(3)  NOT NULL CONSTRAINT DF_Sub_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Sub_Status   CHECK (Status IN ('TRIALING','ACTIVE','PAST_DUE','CANCELED','EXPIRED')),
    CONSTRAINT CK_Sub_Interval CHECK (BillingInterval IN ('MONTH','YEAR','COMP')),
    CONSTRAINT CK_Sub_Period   CHECK (CurrentPeriodEnd IS NULL OR CurrentPeriodStart IS NULL OR CurrentPeriodEnd > CurrentPeriodStart)
);
GO
CREATE UNIQUE INDEX UX_Sub_OneLivePerShop ON bill.ShopSubscription (ShopId) WHERE Status IN ('TRIALING','ACTIVE','PAST_DUE');
CREATE UNIQUE INDEX UX_Sub_External ON bill.ShopSubscription (Provider, ExternalSubscriptionId) WHERE ExternalSubscriptionId IS NOT NULL;
GO

CREATE TABLE bill.Payment
(
    PaymentId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
    SubscriptionId     BIGINT       NOT NULL CONSTRAINT FK_Payment_Sub REFERENCES bill.ShopSubscription(SubscriptionId),
    AmountCents        INT          NOT NULL,
    Currency           CHAR(3)      NOT NULL CONSTRAINT DF_Payment_Currency DEFAULT 'USD',
    Status             VARCHAR(20)  NOT NULL,
    ExternalInvoiceId  VARCHAR(100) NULL,
    PeriodStart        DATETIME2(3) NULL,
    PeriodEnd          DATETIME2(3) NULL,
    PaidAt             DATETIME2(3) NULL,
    CreatedAt          DATETIME2(3) NOT NULL CONSTRAINT DF_Payment_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Payment_Status CHECK (Status IN ('PENDING','PAID','FAILED','REFUNDED','VOID')),
    CONSTRAINT CK_Payment_Amount CHECK (AmountCents >= 0)
);
GO
CREATE UNIQUE INDEX UX_Payment_ExternalInvoice ON bill.Payment (ExternalInvoiceId) WHERE ExternalInvoiceId IS NOT NULL;
GO

-- Every tier change (upgrade, downgrade, lapse, comp): audit trail behind dir.Shop.CurrentTierId
CREATE TABLE bill.ShopTierHistory
(
    TierHistoryId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopTierHistory PRIMARY KEY,
    ShopId          BIGINT        NOT NULL CONSTRAINT FK_TierHist_Shop REFERENCES dir.Shop(ShopId),
    FromTierId      TINYINT       NULL CONSTRAINT FK_TierHist_From REFERENCES dir.ShopTier(TierId),
    ToTierId        TINYINT       NOT NULL CONSTRAINT FK_TierHist_To REFERENCES dir.ShopTier(TierId),
    SubscriptionId  BIGINT        NULL CONSTRAINT FK_TierHist_Sub REFERENCES bill.ShopSubscription(SubscriptionId),
    Reason          VARCHAR(30)   NOT NULL,
    ChangedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_TierHist_ChangedAt DEFAULT SYSUTCDATETIME(),
    ChangedByUserId BIGINT        NULL CONSTRAINT FK_TierHist_User REFERENCES core.AppUser(UserId),
    CONSTRAINT CK_TierHist_Reason CHECK (Reason IN ('INITIAL','UPGRADE','DOWNGRADE','PAYMENT_LAPSED','CANCELED','COMP','ADMIN'))
);
GO
CREATE INDEX IX_TierHist_Shop ON bill.ShopTierHistory (ShopId, ChangedAt);
GO

/* =====================================================================
   4. PREMIUM FEATURES (calendar, promotion, mailbox parsing, FB/IG monitoring)
   ===================================================================== */
CREATE TABLE mkt.ShopEvent
(
    ShopEventId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopEvent PRIMARY KEY,
    ShopId          BIGINT         NOT NULL CONSTRAINT FK_ShopEvent_Shop REFERENCES dir.Shop(ShopId),
    Title           NVARCHAR(200)  NOT NULL,
    Description     NVARCHAR(4000) NULL,
    EventType       VARCHAR(20)    NOT NULL,
    StartsAt        DATETIME2(3)   NOT NULL,  -- UTC
    EndsAt          DATETIME2(3)   NULL,
    TimeZoneId      VARCHAR(64)    NOT NULL CONSTRAINT DF_ShopEvent_Tz DEFAULT 'America/New_York',
    RecurrenceRule  VARCHAR(500)   NULL,      -- iCal RRULE, e.g. weekly trade night
    VenueName       NVARCHAR(200)  NULL,      -- NULL = at the shop
    VenueAddress    NVARCHAR(300)  NULL,
    TicketUrl       NVARCHAR(500)  NULL,
    Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopEvent_Status DEFAULT 'DRAFT',
    SourceType      VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopEvent_Source DEFAULT 'MANUAL',
    ParsedItemId    BIGINT         NULL,      -- FK added after mkt.ParsedItem
    CreatedByUserId BIGINT         NULL CONSTRAINT FK_ShopEvent_User REFERENCES core.AppUser(UserId),
    PublishedAt     DATETIME2(3)   NULL,
    CreatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopEvent_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ShopEvent_Type   CHECK (EventType IN ('CARD_SHOW','RELEASE_DAY','TRADE_NIGHT','TOURNAMENT','SIGNING','BREAK','SALE','MEETUP','OTHER')),
    CONSTRAINT CK_ShopEvent_Status CHECK (Status IN ('DRAFT','PENDING_REVIEW','PUBLISHED','CANCELED')),
    CONSTRAINT CK_ShopEvent_Source CHECK (SourceType IN ('MANUAL','MAILBOX','FACEBOOK','INSTAGRAM','IMPORT')),
    CONSTRAINT CK_ShopEvent_Times  CHECK (EndsAt IS NULL OR EndsAt >= StartsAt)
);
GO
CREATE INDEX IX_ShopEvent_Upcoming ON mkt.ShopEvent (StartsAt) INCLUDE (ShopId, Title, EventType) WHERE Status = 'PUBLISHED';
CREATE INDEX IX_ShopEvent_Shop ON mkt.ShopEvent (ShopId, StartsAt);
GO

CREATE TABLE mkt.PromotionCampaign
(
    CampaignId       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PromotionCampaign PRIMARY KEY,
    ShopId           BIGINT         NOT NULL CONSTRAINT FK_Campaign_Shop REFERENCES dir.Shop(ShopId),
    ShopEventId      BIGINT         NULL CONSTRAINT FK_Campaign_Event REFERENCES mkt.ShopEvent(ShopEventId),
    Channel          VARCHAR(20)    NOT NULL,
    Subject          NVARCHAR(200)  NULL,
    Body             NVARCHAR(MAX)  NOT NULL,
    MediaUrl         NVARCHAR(500)  NULL,
    Status           VARCHAR(20)    NOT NULL CONSTRAINT DF_Campaign_Status DEFAULT 'DRAFT',
    ScheduledFor     DATETIME2(3)   NULL,
    SentAt           DATETIME2(3)   NULL,
    ApprovedByUserId BIGINT         NULL CONSTRAINT FK_Campaign_Approver REFERENCES core.AppUser(UserId), -- NTCA staff approve before send
    ApprovedAt       DATETIME2(3)   NULL,
    RecipientCount   INT            NULL,
    OpenCount        INT            NULL,
    ClickCount       INT            NULL,
    ExternalRef      VARCHAR(200)   NULL,     -- ESP message id / FB post id
    CreatedByUserId  BIGINT         NOT NULL CONSTRAINT FK_Campaign_User REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_Campaign_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Campaign_Channel CHECK (Channel IN ('EMAIL','FACEBOOK','INSTAGRAM','X','SITE_FEATURE','NEWSLETTER')),
    CONSTRAINT CK_Campaign_Status  CHECK (Status IN ('DRAFT','PENDING_REVIEW','SCHEDULED','SENT','CANCELED','FAILED'))
);
GO
CREATE INDEX IX_Campaign_Schedule ON mkt.PromotionCampaign (Status, ScheduledFor);
GO

-- Mailbox parsing: each premium shop gets an inbound address (or forwards its inbox)
CREATE TABLE mkt.ShopMailbox
(
    MailboxId            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopMailbox PRIMARY KEY,
    ShopId               BIGINT        NOT NULL CONSTRAINT FK_Mailbox_Shop REFERENCES dir.Shop(ShopId),
    InboundAddress       NVARCHAR(320) NOT NULL,   -- e.g. {slug}@in.tradingcardsassociation.com
    ForwardFromAddress   NVARCHAR(320) NULL,
    ForwardingVerifiedAt DATETIME2(3)  NULL,
    ParseRulesJson       NVARCHAR(MAX) NULL,
    AutoPublish          BIT           NOT NULL CONSTRAINT DF_Mailbox_AutoPublish DEFAULT 0,
    IsActive             BIT           NOT NULL CONSTRAINT DF_Mailbox_IsActive DEFAULT 1,
    CreatedAt            DATETIME2(3)  NOT NULL CONSTRAINT DF_Mailbox_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Mailbox_Address UNIQUE (InboundAddress),
    CONSTRAINT CK_Mailbox_Rules CHECK (ParseRulesJson IS NULL OR ISJSON(ParseRulesJson) = 1)
);
GO

CREATE TABLE mkt.InboundEmail
(
    InboundEmailId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InboundEmail PRIMARY KEY,
    MailboxId       BIGINT         NOT NULL CONSTRAINT FK_InboundEmail_Mailbox REFERENCES mkt.ShopMailbox(MailboxId),
    MessageId       NVARCHAR(400)  NOT NULL,    -- RFC 5322 Message-ID, dedupe key
    FromAddress     NVARCHAR(320)  NOT NULL,
    Subject         NVARCHAR(500)  NULL,
    ReceivedAt      DATETIME2(3)   NOT NULL,
    RawStorageUrl   NVARCHAR(500)  NOT NULL,    -- .eml kept in blob storage, not the DB
    ParseStatus     VARCHAR(20)    NOT NULL CONSTRAINT DF_InboundEmail_Status DEFAULT 'PENDING',
    ParsedAt        DATETIME2(3)   NULL,
    ParseError      NVARCHAR(1000) NULL,
    CONSTRAINT UQ_InboundEmail_Msg UNIQUE (MailboxId, MessageId),
    CONSTRAINT CK_InboundEmail_Status CHECK (ParseStatus IN ('PENDING','PARSED','NEEDS_REVIEW','IGNORED','FAILED'))
);
GO
CREATE INDEX IX_InboundEmail_Queue ON mkt.InboundEmail (ParseStatus, ReceivedAt);
GO

-- Facebook / Instagram monitoring
CREATE TABLE mkt.SocialAccount
(
    SocialAccountId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SocialAccount PRIMARY KEY,
    ShopId            BIGINT        NOT NULL CONSTRAINT FK_Social_Shop REFERENCES dir.Shop(ShopId),
    Platform          VARCHAR(20)   NOT NULL,
    Handle            NVARCHAR(100) NOT NULL,
    ExternalAccountId VARCHAR(100)  NULL,
    TokenVaultRef     VARCHAR(200)  NULL,     -- pointer to a vault secret; tokens never stored here
    MonitorEnabled    BIT           NOT NULL CONSTRAINT DF_Social_Monitor DEFAULT 1,
    LastPolledAt      DATETIME2(3)  NULL,
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_Social_Status DEFAULT 'ACTIVE',
    CreatedAt         DATETIME2(3)  NOT NULL CONSTRAINT DF_Social_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Social_ShopPlatform UNIQUE (ShopId, Platform, Handle),
    CONSTRAINT CK_Social_Platform CHECK (Platform IN ('FACEBOOK','INSTAGRAM')),
    CONSTRAINT CK_Social_Status   CHECK (Status IN ('ACTIVE','TOKEN_EXPIRED','DISCONNECTED','ERROR'))
);
GO

CREATE TABLE mkt.SocialPost
(
    SocialPostId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SocialPost PRIMARY KEY,
    SocialAccountId BIGINT         NOT NULL CONSTRAINT FK_SocialPost_Account REFERENCES mkt.SocialAccount(SocialAccountId),
    ExternalPostId  VARCHAR(100)   NOT NULL,
    PostedAt        DATETIME2(3)   NOT NULL,
    PermalinkUrl    NVARCHAR(500)  NULL,
    Caption         NVARCHAR(MAX)  NULL,
    MediaUrl        NVARCHAR(500)  NULL,
    CapturedAt      DATETIME2(3)   NOT NULL CONSTRAINT DF_SocialPost_CapturedAt DEFAULT SYSUTCDATETIME(),
    ProcessStatus   VARCHAR(20)    NOT NULL CONSTRAINT DF_SocialPost_Status DEFAULT 'PENDING',
    CONSTRAINT UQ_SocialPost_External UNIQUE (SocialAccountId, ExternalPostId),
    CONSTRAINT CK_SocialPost_Status CHECK (ProcessStatus IN ('PENDING','PARSED','NEEDS_REVIEW','IGNORED','FAILED'))
);
GO

-- Structured items extracted from an email OR a social post
CREATE TABLE mkt.ParsedItem
(
    ParsedItemId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ParsedItem PRIMARY KEY,
    ShopId           BIGINT        NOT NULL CONSTRAINT FK_ParsedItem_Shop REFERENCES dir.Shop(ShopId),
    InboundEmailId   BIGINT        NULL CONSTRAINT FK_ParsedItem_Email REFERENCES mkt.InboundEmail(InboundEmailId),
    SocialPostId     BIGINT        NULL CONSTRAINT FK_ParsedItem_Post  REFERENCES mkt.SocialPost(SocialPostId),
    ItemType         VARCHAR(20)   NOT NULL,
    ExtractedJson    NVARCHAR(MAX) NOT NULL,
    Confidence       DECIMAL(4,3)  NULL,
    ReviewStatus     VARCHAR(20)   NOT NULL CONSTRAINT DF_ParsedItem_Review DEFAULT 'PENDING',
    ReviewedByUserId BIGINT        NULL CONSTRAINT FK_ParsedItem_Reviewer REFERENCES core.AppUser(UserId),
    ReviewedAt       DATETIME2(3)  NULL,
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_ParsedItem_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ParsedItem_OneSource CHECK ((CASE WHEN InboundEmailId IS NULL THEN 0 ELSE 1 END
                                             + CASE WHEN SocialPostId   IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT CK_ParsedItem_Type   CHECK (ItemType IN ('EVENT','PRODUCT_RELEASE','PROMOTION','HOURS_CHANGE','OTHER')),
    CONSTRAINT CK_ParsedItem_Review CHECK (ReviewStatus IN ('PENDING','APPROVED','REJECTED','AUTO_APPLIED')),
    CONSTRAINT CK_ParsedItem_Json   CHECK (ISJSON(ExtractedJson) = 1),
    CONSTRAINT CK_ParsedItem_Conf   CHECK (Confidence IS NULL OR Confidence BETWEEN 0 AND 1)
);
GO
CREATE INDEX IX_ParsedItem_Review ON mkt.ParsedItem (ReviewStatus, CreatedAt) INCLUDE (ShopId, ItemType);
GO
ALTER TABLE mkt.ShopEvent ADD CONSTRAINT FK_ShopEvent_ParsedItem FOREIGN KEY (ParsedItemId) REFERENCES mkt.ParsedItem(ParsedItemId);
GO
/* =====================================================================
   5. NEW-SHOPS ROTATION FEED
   ===================================================================== */
CREATE TABLE feed.RotationSlot
(
    SlotCode             VARCHAR(30)   NOT NULL CONSTRAINT PK_RotationSlot PRIMARY KEY,
    Name                 NVARCHAR(100) NOT NULL,
    MaxItems             TINYINT       NOT NULL,
    DefaultWindowDays    SMALLINT      NOT NULL,
    RefreshEveryMinutes  SMALLINT      NOT NULL,
    IsActive             BIT           NOT NULL CONSTRAINT DF_RotationSlot_IsActive DEFAULT 1,
    CONSTRAINT CK_RotationSlot_Max CHECK (MaxItems BETWEEN 1 AND 50)
);
GO

-- Suggested: paid tiers get more weight / a longer stay in the feed; free shops always appear
CREATE TABLE feed.TierRotationWeight
(
    TierId           TINYINT      NOT NULL CONSTRAINT PK_TierRotationWeight PRIMARY KEY
                                  CONSTRAINT FK_TierRotWeight_Tier REFERENCES dir.ShopTier(TierId),
    WeightMultiplier DECIMAL(5,2) NOT NULL,
    WindowDays       SMALLINT     NOT NULL,
    CONSTRAINT CK_TierRotWeight CHECK (WeightMultiplier > 0 AND WindowDays > 0)
);
GO

CREATE TABLE feed.NewShopFeedEntry
(
    FeedEntryId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewShopFeedEntry PRIMARY KEY,
    SlotCode         VARCHAR(30)   NOT NULL CONSTRAINT FK_FeedEntry_Slot REFERENCES feed.RotationSlot(SlotCode),
    ShopId           BIGINT        NOT NULL CONSTRAINT FK_FeedEntry_Shop REFERENCES dir.Shop(ShopId),
    Reason           VARCHAR(20)   NOT NULL,
    BaseWeight       DECIMAL(6,2)  NOT NULL CONSTRAINT DF_FeedEntry_Weight DEFAULT 1.00,
    IsPinned         BIT           NOT NULL CONSTRAINT DF_FeedEntry_Pinned DEFAULT 0,
    EligibleFrom     DATETIME2(3)  NOT NULL,
    EligibleUntil    DATETIME2(3)  NOT NULL,
    IsActive         BIT           NOT NULL CONSTRAINT DF_FeedEntry_IsActive DEFAULT 1,
    CreatedByUserId  BIGINT        NULL CONSTRAINT FK_FeedEntry_User REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_FeedEntry_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_FeedEntry_Reason CHECK (Reason IN ('NEW_LISTING','NEWLY_CLAIMED','TIER_UPGRADE','STAFF_PICK','GRAND_OPENING')),
    CONSTRAINT CK_FeedEntry_Window CHECK (EligibleUntil > EligibleFrom),
    CONSTRAINT CK_FeedEntry_Weight CHECK (BaseWeight > 0)
);
GO
CREATE UNIQUE INDEX UX_FeedEntry_ActivePerSlotShop ON feed.NewShopFeedEntry (SlotCode, ShopId) WHERE IsActive = 1;
CREATE INDEX IX_FeedEntry_Window ON feed.NewShopFeedEntry (SlotCode, EligibleFrom, EligibleUntil) INCLUDE (ShopId, BaseWeight, IsPinned) WHERE IsActive = 1;
GO

CREATE TABLE feed.FeedImpression
(
    ImpressionId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeedImpression PRIMARY KEY,
    FeedEntryId   BIGINT       NOT NULL CONSTRAINT FK_Impression_Entry REFERENCES feed.NewShopFeedEntry(FeedEntryId),
    ShownAt       DATETIME2(3) NOT NULL CONSTRAINT DF_Impression_ShownAt DEFAULT SYSUTCDATETIME(),
    Position      TINYINT      NOT NULL,
    UserId        BIGINT       NULL CONSTRAINT FK_Impression_User REFERENCES core.AppUser(UserId),
    SessionHash   BINARY(32)   NULL,   -- hashed anonymous session id, no raw IP stored
    ClickedAt     DATETIME2(3) NULL
);
GO
CREATE INDEX IX_Impression_Entry ON feed.FeedImpression (FeedEntryId, ShownAt);
GO

/* =====================================================================
   6. USER-GENERATED CONTENT (photos, reviews, feedback, votes, moderation)
   ===================================================================== */
CREATE TABLE ugc.ShopPhoto
(
    PhotoId                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopPhoto PRIMARY KEY,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ShopPhoto_PublicId DEFAULT NEWSEQUENTIALID(),
    ShopId                  BIGINT         NOT NULL CONSTRAINT FK_ShopPhoto_Shop REFERENCES dir.Shop(ShopId),
    UploadedByUserId        BIGINT         NOT NULL CONSTRAINT FK_ShopPhoto_User REFERENCES core.AppUser(UserId),
    StorageUrl              NVARCHAR(500)  NOT NULL,
    ThumbnailUrl            NVARCHAR(500)  NULL,
    Caption                 NVARCHAR(300)  NULL,
    PhotoKind               VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopPhoto_Kind DEFAULT 'INTERIOR',
    ContentHash             BINARY(32)     NOT NULL,  -- SHA-256 of bytes: exact-duplicate guard
    PerceptualHash          BIGINT         NULL,      -- Suggested: near-duplicate detection (pHash)
    WidthPx                 INT            NULL,
    HeightPx                INT            NULL,
    TakenAt                 DATETIME2(3)   NULL,
    ModerationStatus        VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopPhoto_Mod DEFAULT 'PENDING',
    ModeratedByUserId       BIGINT         NULL CONSTRAINT FK_ShopPhoto_Moderator REFERENCES core.AppUser(UserId),
    ModeratedAt             DATETIME2(3)   NULL,
    RejectReason            NVARCHAR(300)  NULL,
    WasFirstApprovedForShop BIT            NOT NULL CONSTRAINT DF_ShopPhoto_First DEFAULT 0,
    UpvoteCount             INT            NOT NULL CONSTRAINT DF_ShopPhoto_Up DEFAULT 0,
    DownvoteCount           INT            NOT NULL CONSTRAINT DF_ShopPhoto_Down DEFAULT 0,
    CreatedAt               DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopPhoto_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ShopPhoto_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_ShopPhoto_Kind CHECK (PhotoKind IN ('EXTERIOR','INTERIOR','SIGNAGE','INVENTORY','EVENT','OTHER')),
    CONSTRAINT CK_ShopPhoto_Mod  CHECK (ModerationStatus IN ('PENDING','APPROVED','REJECTED','REMOVED'))
);
GO
CREATE UNIQUE INDEX UX_ShopPhoto_HashPerShop ON ugc.ShopPhoto (ShopId, ContentHash) WHERE ModerationStatus IN ('PENDING','APPROVED');
CREATE INDEX IX_ShopPhoto_Shop  ON ugc.ShopPhoto (ShopId, ModerationStatus, CreatedAt);
CREATE INDEX IX_ShopPhoto_User  ON ugc.ShopPhoto (UploadedByUserId, CreatedAt);
CREATE INDEX IX_ShopPhoto_Queue ON ugc.ShopPhoto (CreatedAt) WHERE ModerationStatus = 'PENDING';
GO
ALTER TABLE dir.Shop ADD CONSTRAINT FK_Shop_CoverPhoto FOREIGN KEY (CoverPhotoId) REFERENCES ugc.ShopPhoto(PhotoId);
GO

CREATE TABLE ugc.ShopReview
(
    ReviewId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShopReview PRIMARY KEY,
    PublicId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ShopReview_PublicId DEFAULT NEWSEQUENTIALID(),
    ShopId            BIGINT         NOT NULL CONSTRAINT FK_ShopReview_Shop REFERENCES dir.Shop(ShopId),
    AuthorUserId      BIGINT         NOT NULL CONSTRAINT FK_ShopReview_User REFERENCES core.AppUser(UserId),
    Rating            TINYINT        NOT NULL,
    Title             NVARCHAR(150)  NULL,
    Body              NVARCHAR(4000) NOT NULL,
    VisitedOn         DATE           NULL,
    ModerationStatus  VARCHAR(20)    NOT NULL CONSTRAINT DF_ShopReview_Mod DEFAULT 'PENDING',
    AutoApproved      BIT            NOT NULL CONSTRAINT DF_ShopReview_Auto DEFAULT 0,  -- via trusted-karma perk
    ModeratedByUserId BIGINT         NULL CONSTRAINT FK_ShopReview_Moderator REFERENCES core.AppUser(UserId),
    ModeratedAt       DATETIME2(3)   NULL,
    HelpfulCount      INT            NOT NULL CONSTRAINT DF_ShopReview_Helpful DEFAULT 0,
    UnhelpfulCount    INT            NOT NULL CONSTRAINT DF_ShopReview_Unhelpful DEFAULT 0,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ShopReview_CreatedAt DEFAULT SYSUTCDATETIME(),
    EditedAt          DATETIME2(3)   NULL,
    CONSTRAINT UQ_ShopReview_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_ShopReview_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_ShopReview_Mod    CHECK (ModerationStatus IN ('PENDING','APPROVED','REJECTED','REMOVED'))
);
GO
CREATE UNIQUE INDEX UX_ShopReview_OnePerUserShop ON ugc.ShopReview (ShopId, AuthorUserId) WHERE ModerationStatus IN ('PENDING','APPROVED');
CREATE INDEX IX_ShopReview_Shop ON ugc.ShopReview (ShopId, ModerationStatus) INCLUDE (Rating, CreatedAt);
CREATE INDEX IX_ShopReview_User ON ugc.ShopReview (AuthorUserId, CreatedAt);
GO

-- Suggested: owner reply to a review (PAID/PREMIUM feature OWNER_RESPONSES)
CREATE TABLE ugc.ReviewResponse
(
    ReviewId          BIGINT         NOT NULL CONSTRAINT PK_ReviewResponse PRIMARY KEY
                                     CONSTRAINT FK_ReviewResponse_Review REFERENCES ugc.ShopReview(ReviewId),
    ResponderUserId   BIGINT         NOT NULL CONSTRAINT FK_ReviewResponse_User REFERENCES core.AppUser(UserId),
    Body              NVARCHAR(2000) NOT NULL,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ReviewResponse_CreatedAt DEFAULT SYSUTCDATETIME(),
    EditedAt          DATETIME2(3)   NULL
);
GO

CREATE TABLE ugc.Feedback
(
    FeedbackId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Feedback PRIMARY KEY,
    SubmittedByUserId   BIGINT         NOT NULL CONSTRAINT FK_Feedback_User REFERENCES core.AppUser(UserId),
    ShopId              BIGINT         NULL CONSTRAINT FK_Feedback_Shop REFERENCES dir.Shop(ShopId),
    FeedbackType        VARCHAR(30)    NOT NULL,
    Body                NVARCHAR(4000) NOT NULL,
    ProposedChangesJson NVARCHAR(MAX)  NULL,    -- e.g. {"Phone":"...","WebsiteUrl":"..."}
    Status              VARCHAR(20)    NOT NULL CONSTRAINT DF_Feedback_Status DEFAULT 'OPEN',
    ResolvedByUserId    BIGINT         NULL CONSTRAINT FK_Feedback_Resolver REFERENCES core.AppUser(UserId),
    ResolvedAt          DATETIME2(3)   NULL,
    ResolutionNote      NVARCHAR(500)  NULL,
    ResultingShopId     BIGINT         NULL CONSTRAINT FK_Feedback_ResultShop REFERENCES dir.Shop(ShopId), -- new shop created from a suggestion
    CreatedAt           DATETIME2(3)   NOT NULL CONSTRAINT DF_Feedback_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Feedback_Type   CHECK (FeedbackType IN ('LISTING_CORRECTION','SHOP_CLOSED','NEW_SHOP_SUGGESTION','EVENT_TIP','SITE_FEEDBACK')),
    CONSTRAINT CK_Feedback_Status CHECK (Status IN ('OPEN','ACCEPTED','REJECTED','DUPLICATE','SPAM')),
    CONSTRAINT CK_Feedback_Json   CHECK (ProposedChangesJson IS NULL OR ISJSON(ProposedChangesJson) = 1)
);
GO
CREATE INDEX IX_Feedback_Queue ON ugc.Feedback (Status, CreatedAt) INCLUDE (FeedbackType, ShopId);
CREATE INDEX IX_Feedback_User  ON ugc.Feedback (SubmittedByUserId, CreatedAt);
GO

-- Up/down votes (photos) and helpful/unhelpful (reviews): Reddit-style "received" karma
CREATE TABLE ugc.ContentVote
(
    VoteId       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContentVote PRIMARY KEY,
    VoterUserId  BIGINT       NOT NULL CONSTRAINT FK_Vote_User REFERENCES core.AppUser(UserId),
    PhotoId      BIGINT       NULL CONSTRAINT FK_Vote_Photo  REFERENCES ugc.ShopPhoto(PhotoId),
    ReviewId     BIGINT       NULL CONSTRAINT FK_Vote_Review REFERENCES ugc.ShopReview(ReviewId),
    VoteValue    SMALLINT     NOT NULL,   -- +1 / -1 / 0 (retracted)
    CreatedAt    DATETIME2(3) NOT NULL CONSTRAINT DF_Vote_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(3) NOT NULL CONSTRAINT DF_Vote_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Vote_OneTarget CHECK ((CASE WHEN PhotoId IS NULL THEN 0 ELSE 1 END
                                       + CASE WHEN ReviewId IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT CK_Vote_Value CHECK (VoteValue IN (-1, 0, 1))
);
GO
CREATE UNIQUE INDEX UX_Vote_Photo  ON ugc.ContentVote (PhotoId, VoterUserId)  WHERE PhotoId  IS NOT NULL;
CREATE UNIQUE INDEX UX_Vote_Review ON ugc.ContentVote (ReviewId, VoterUserId) WHERE ReviewId IS NOT NULL;
CREATE INDEX IX_Vote_Voter ON ugc.ContentVote (VoterUserId, CreatedAt);
GO

-- Suggested: user flags on content
CREATE TABLE ugc.ContentReport
(
    ReportId         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContentReport PRIMARY KEY,
    ReporterUserId   BIGINT        NOT NULL CONSTRAINT FK_Report_User REFERENCES core.AppUser(UserId),
    TargetType       VARCHAR(20)   NOT NULL,
    TargetId         BIGINT        NOT NULL,
    ReasonCode       VARCHAR(30)   NOT NULL,
    Details          NVARCHAR(1000) NULL,
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Report_Status DEFAULT 'OPEN',
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_Report_CreatedAt DEFAULT SYSUTCDATETIME(),
    ResolvedAt       DATETIME2(3)  NULL,
    ResolvedByUserId BIGINT        NULL CONSTRAINT FK_Report_Resolver REFERENCES core.AppUser(UserId),
    CONSTRAINT CK_Report_Target CHECK (TargetType IN ('PHOTO','REVIEW','REVIEW_RESPONSE','USER','SHOP')),
    CONSTRAINT CK_Report_Reason CHECK (ReasonCode IN ('SPAM','FAKE','OFFENSIVE','WRONG_SHOP','COPYRIGHT','CONFLICT_OF_INTEREST','OTHER')),
    CONSTRAINT CK_Report_Status CHECK (Status IN ('OPEN','UPHELD','DISMISSED'))
);
GO
CREATE UNIQUE INDEX UX_Report_OneOpenPerReporter ON ugc.ContentReport (TargetType, TargetId, ReporterUserId) WHERE Status = 'OPEN';
GO

CREATE TABLE ugc.ModerationAction
(
    ModerationActionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ModerationAction PRIMARY KEY,
    ModeratorUserId    BIGINT        NOT NULL CONSTRAINT FK_ModAction_User REFERENCES core.AppUser(UserId),
    TargetType         VARCHAR(20)   NOT NULL,
    TargetId           BIGINT        NOT NULL,
    ActionCode         VARCHAR(30)   NOT NULL,
    Reason             NVARCHAR(500) NULL,
    CreatedAt          DATETIME2(3)  NOT NULL CONSTRAINT DF_ModAction_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ModAction_Target CHECK (TargetType IN ('PHOTO','REVIEW','REVIEW_RESPONSE','FEEDBACK','USER','SHOP','CLAIM','BADGE','LEDGER')),
    CONSTRAINT CK_ModAction_Code   CHECK (ActionCode IN ('APPROVE','REJECT','REMOVE','RESTORE','WARN','SUSPEND','BAN','FREEZE_KARMA','UNFREEZE_KARMA','REVOKE_BADGE','ADJUST_POINTS'))
);
GO
CREATE INDEX IX_ModAction_Target ON ugc.ModerationAction (TargetType, TargetId, CreatedAt);
GO
/* =====================================================================
   7. KARMA: categories, action rules, append-only points ledger
   ===================================================================== */
CREATE TABLE karma.Category
(
    CategoryCode VARCHAR(20)  NOT NULL CONSTRAINT PK_KarmaCategory PRIMARY KEY,
    Name         NVARCHAR(50) NOT NULL,
    DisplayOrder TINYINT      NOT NULL
);
GO

-- Rules table: point values and anti-gaming limits live in data, not code
CREATE TABLE karma.ActionType
(
    ActionCode          VARCHAR(40)   NOT NULL CONSTRAINT PK_ActionType PRIMARY KEY,
    CategoryCode        VARCHAR(20)   NOT NULL CONSTRAINT FK_ActionType_Category REFERENCES karma.Category(CategoryCode),
    Name                NVARCHAR(100) NOT NULL,
    Description         NVARCHAR(400) NULL,
    Points              INT           NOT NULL,
    DailyPointCap       INT           NULL,   -- max points/user/UTC day from this action (NULL = none)
    CooldownSeconds     INT           NULL,   -- min gap between two awards of this action to one user
    PerShopLifetimeCap  INT           NULL,   -- max awards of this action per user per shop, ever
    RequiresModeration  BIT           NOT NULL CONSTRAINT DF_ActionType_ReqMod DEFAULT 1,
    BlockOnOwnShop      BIT           NOT NULL CONSTRAINT DF_ActionType_BlockOwn DEFAULT 1,
    IsActive            BIT           NOT NULL CONSTRAINT DF_ActionType_IsActive DEFAULT 1,
    CONSTRAINT CK_ActionType_Points CHECK (Points <> 0 OR ActionCode IN ('ADMIN_ADJUSTMENT','BADGE_BONUS')),
    CONSTRAINT CK_ActionType_Caps   CHECK (ISNULL(DailyPointCap,1) > 0 AND ISNULL(CooldownSeconds,0) >= 0 AND ISNULL(PerShopLifetimeCap,1) > 0)
);
GO

/* PointsLedger: one row per point event. Never updated or deleted.
   Corrections = a new row with negative Points that references the original (ReversesLedgerId).
   SourceKey makes every award idempotent: re-processing an approval cannot double-pay. */
CREATE TABLE karma.PointsLedger
(
    LedgerId          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PointsLedger PRIMARY KEY,
    UserId            BIGINT        NOT NULL CONSTRAINT FK_Ledger_User REFERENCES core.AppUser(UserId),
    ActionCode        VARCHAR(40)   NOT NULL CONSTRAINT FK_Ledger_Action REFERENCES karma.ActionType(ActionCode),
    CategoryCode      VARCHAR(20)   NOT NULL CONSTRAINT FK_Ledger_Category REFERENCES karma.Category(CategoryCode), -- denormalized for the indexed view
    Points            INT           NOT NULL,
    SourceType        VARCHAR(20)   NOT NULL,
    SourceId          BIGINT        NULL,
    ShopId            BIGINT        NULL CONSTRAINT FK_Ledger_Shop REFERENCES dir.Shop(ShopId),
    SourceKey         VARCHAR(200)  NOT NULL,   -- e.g. 'PHOTO_APPROVED:PHOTO:123'
    ReversesLedgerId  BIGINT        NULL CONSTRAINT FK_Ledger_Reverses REFERENCES karma.PointsLedger(LedgerId),
    Reason            NVARCHAR(300) NULL,
    CreatedByUserId   BIGINT        NULL CONSTRAINT FK_Ledger_CreatedBy REFERENCES core.AppUser(UserId), -- NULL = system
    CreatedAt         DATETIME2(3)  NOT NULL CONSTRAINT DF_Ledger_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Ledger_SourceKey UNIQUE (SourceKey),
    CONSTRAINT CK_Ledger_Points CHECK (Points <> 0),
    CONSTRAINT CK_Ledger_SourceType CHECK (SourceType IN ('PHOTO','REVIEW','FEEDBACK','CLAIM','SHOP','BADGE','VOTE','ADMIN')),
    CONSTRAINT CK_Ledger_ReversalReason CHECK (ReversesLedgerId IS NULL OR Reason IS NOT NULL)
);
GO
CREATE UNIQUE INDEX UX_Ledger_OneReversal ON karma.PointsLedger (ReversesLedgerId) WHERE ReversesLedgerId IS NOT NULL;
CREATE INDEX IX_Ledger_User_Time   ON karma.PointsLedger (UserId, CreatedAt) INCLUDE (ActionCode, Points, ShopId);
CREATE INDEX IX_Ledger_User_Action ON karma.PointsLedger (UserId, ActionCode, CreatedAt) INCLUDE (Points, ShopId, ReversesLedgerId);
CREATE INDEX IX_Ledger_Source      ON karma.PointsLedger (SourceType, SourceId);
CREATE INDEX IX_Ledger_Shop        ON karma.PointsLedger (ShopId, CreatedAt) WHERE ShopId IS NOT NULL;
GO

CREATE TRIGGER karma.TR_PointsLedger_AppendOnly
ON karma.PointsLedger
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'karma.PointsLedger is append-only. Insert a reversal row instead (karma.usp_ReverseForSource).', 1;
END;
GO

/* Indexed (materialized) view: running karma per user per category, maintained by SQL Server.
   Gives O(1) profile reads without a mutable balance column. */
CREATE VIEW karma.vw_UserKarmaByCategory
WITH SCHEMABINDING
AS
SELECT  l.UserId,
        l.CategoryCode,
        SUM(l.Points)  AS Karma,
        COUNT_BIG(*)   AS LedgerRows
FROM    karma.PointsLedger AS l
GROUP BY l.UserId, l.CategoryCode;
GO
CREATE UNIQUE CLUSTERED INDEX UCX_vw_UserKarmaByCategory ON karma.vw_UserKarmaByCategory (UserId, CategoryCode);
GO

/* =====================================================================
   8. BADGES + PERKS
   ===================================================================== */
CREATE TABLE karma.BadgeDefinition
(
    BadgeId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BadgeDefinition PRIMARY KEY,
    BadgeCode      VARCHAR(40)   NOT NULL,
    Name           NVARCHAR(80)  NOT NULL,
    Description    NVARCHAR(300) NOT NULL,
    BadgeFamily    VARCHAR(30)   NOT NULL,   -- PHOTO, REVIEW, KARMA ... (levels within a family)
    BadgeLevel     TINYINT       NOT NULL CONSTRAINT DF_Badge_Level DEFAULT 1,
    IconUrl        NVARCHAR(500) NULL,
    CriteriaType   VARCHAR(20)   NOT NULL,
    ActionCode     VARCHAR(40)   NULL CONSTRAINT FK_Badge_Action   REFERENCES karma.ActionType(ActionCode),
    CategoryCode   VARCHAR(20)   NULL CONSTRAINT FK_Badge_Category REFERENCES karma.Category(CategoryCode),
    Threshold      INT           NULL,
    BonusPoints    INT           NOT NULL CONSTRAINT DF_Badge_Bonus DEFAULT 0,   -- Suggested
    DisplayOrder   SMALLINT      NOT NULL CONSTRAINT DF_Badge_Order DEFAULT 100,
    IsSecret       BIT           NOT NULL CONSTRAINT DF_Badge_Secret DEFAULT 0,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Badge_IsActive DEFAULT 1,
    CreatedAt      DATETIME2(3)  NOT NULL CONSTRAINT DF_Badge_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Badge_Code UNIQUE (BadgeCode),
    CONSTRAINT UQ_Badge_FamilyLevel UNIQUE (BadgeFamily, BadgeLevel),
    CONSTRAINT CK_Badge_CriteriaType CHECK (CriteriaType IN ('ACTION_COUNT','CATEGORY_KARMA','TOTAL_KARMA','DISTINCT_SHOPS','DISTINCT_STATES','ACCOUNT_AGE_DAYS','MANUAL')),
    CONSTRAINT CK_Badge_Bonus CHECK (BonusPoints >= 0),
    CONSTRAINT CK_Badge_Criteria CHECK (
           (CriteriaType = 'MANUAL'           AND Threshold IS NULL)
        OR (CriteriaType = 'ACTION_COUNT'     AND ActionCode   IS NOT NULL AND Threshold > 0)
        OR (CriteriaType = 'CATEGORY_KARMA'   AND CategoryCode IS NOT NULL AND Threshold > 0)
        OR (CriteriaType = 'TOTAL_KARMA'      AND Threshold > 0)
        OR (CriteriaType IN ('DISTINCT_SHOPS','DISTINCT_STATES') AND ActionCode IS NOT NULL AND Threshold > 0)
        OR (CriteriaType = 'ACCOUNT_AGE_DAYS' AND Threshold > 0))
);
GO

CREATE TABLE karma.UserBadge
(
    UserBadgeId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserBadge PRIMARY KEY,
    UserId           BIGINT        NOT NULL CONSTRAINT FK_UserBadge_User  REFERENCES core.AppUser(UserId),
    BadgeId          INT           NOT NULL CONSTRAINT FK_UserBadge_Badge REFERENCES karma.BadgeDefinition(BadgeId),
    AwardedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_UserBadge_AwardedAt DEFAULT SYSUTCDATETIME(),
    AwardSource      VARCHAR(10)   NOT NULL CONSTRAINT DF_UserBadge_Source DEFAULT 'RULE',
    AwardedByUserId  BIGINT        NULL CONSTRAINT FK_UserBadge_AwardedBy REFERENCES core.AppUser(UserId),
    TriggerLedgerId  BIGINT        NULL CONSTRAINT FK_UserBadge_Ledger REFERENCES karma.PointsLedger(LedgerId), -- the event that crossed the threshold
    IsDisplayed      BIT           NOT NULL CONSTRAINT DF_UserBadge_Displayed DEFAULT 1,
    PinOrder         TINYINT       NULL,   -- 1..3 = featured next to username
    RevokedAt        DATETIME2(3)  NULL,
    RevokedByUserId  BIGINT        NULL CONSTRAINT FK_UserBadge_RevokedBy REFERENCES core.AppUser(UserId),
    RevokedReason    NVARCHAR(300) NULL,
    CONSTRAINT UQ_UserBadge UNIQUE (UserId, BadgeId),
    CONSTRAINT CK_UserBadge_Source CHECK (AwardSource IN ('RULE','MANUAL','BACKFILL')),
    CONSTRAINT CK_UserBadge_Pin    CHECK (PinOrder IS NULL OR PinOrder BETWEEN 1 AND 3),
    CONSTRAINT CK_UserBadge_Revoke CHECK (RevokedAt IS NULL OR RevokedReason IS NOT NULL)
);
GO
CREATE UNIQUE INDEX UX_UserBadge_Pin ON karma.UserBadge (UserId, PinOrder) WHERE PinOrder IS NOT NULL AND RevokedAt IS NULL;
CREATE INDEX IX_UserBadge_Profile ON karma.UserBadge (UserId) INCLUDE (BadgeId, AwardedAt, PinOrder, IsDisplayed) WHERE RevokedAt IS NULL;
GO

-- Suggested: karma unlocks privileges (Reddit-style thresholds), defined as data
CREATE TABLE karma.Perk
(
    PerkCode            VARCHAR(40)   NOT NULL CONSTRAINT PK_Perk PRIMARY KEY,
    Name                NVARCHAR(100) NOT NULL,
    Description         NVARCHAR(300) NULL,
    MinTotalKarma       INT           NULL,
    MinCategoryCode     VARCHAR(20)   NULL CONSTRAINT FK_Perk_Category REFERENCES karma.Category(CategoryCode),
    MinCategoryKarma    INT           NULL,
    RequiredBadgeId     INT           NULL CONSTRAINT FK_Perk_Badge REFERENCES karma.BadgeDefinition(BadgeId),
    MinAccountAgeDays   INT           NULL,
    MaxRemovalsLast90d  INT           NULL,
    IsActive            BIT           NOT NULL CONSTRAINT DF_Perk_IsActive DEFAULT 1,
    CONSTRAINT CK_Perk_Category CHECK ((MinCategoryCode IS NULL AND MinCategoryKarma IS NULL) OR (MinCategoryCode IS NOT NULL AND MinCategoryKarma IS NOT NULL))
);
GO
/* =====================================================================
   9. VIEWS
   ===================================================================== */

-- Profile karma: total + per-category breakdown (reads the indexed view)
CREATE VIEW karma.vw_UserKarma
AS
SELECT  u.UserId,
        u.Username,
        u.KarmaStatus,
        ISNULL(SUM(CASE WHEN k.CategoryCode = 'PHOTO'     THEN k.Karma END), 0) AS PhotoKarma,
        ISNULL(SUM(CASE WHEN k.CategoryCode = 'REVIEW'    THEN k.Karma END), 0) AS ReviewKarma,
        ISNULL(SUM(CASE WHEN k.CategoryCode = 'FEEDBACK'  THEN k.Karma END), 0) AS FeedbackKarma,
        ISNULL(SUM(CASE WHEN k.CategoryCode = 'SHOP_HELP' THEN k.Karma END), 0) AS ShopHelpKarma,
        ISNULL(SUM(CASE WHEN k.CategoryCode = 'COMMUNITY' THEN k.Karma END), 0) AS CommunityKarma,
        ISNULL(SUM(k.Karma), 0)                                               AS TotalKarma
FROM    core.AppUser AS u
LEFT JOIN karma.vw_UserKarmaByCategory AS k WITH (NOEXPAND)
       ON k.UserId = u.UserId
GROUP BY u.UserId, u.Username, u.KarmaStatus;
GO

-- Karma over time (profile chart / leaderboards by month)
CREATE VIEW karma.vw_UserKarmaMonthly
AS
SELECT  l.UserId,
        DATEFROMPARTS(YEAR(l.CreatedAt), MONTH(l.CreatedAt), 1) AS MonthStart,
        l.CategoryCode,
        SUM(l.Points)                                         AS PointsEarned,
        SUM(CASE WHEN l.ReversesLedgerId IS NULL THEN 1 ELSE 0 END) AS AwardEvents
FROM    karma.PointsLedger AS l
GROUP BY l.UserId, DATEFROMPARTS(YEAR(l.CreatedAt), MONTH(l.CreatedAt), 1), l.CategoryCode;
GO

-- What each shop is entitled to, given its current tier
CREATE VIEW dir.vw_ShopEntitlement
AS
SELECT  s.ShopId,
        s.CurrentTierId,
        t.TierCode,
        tf.FeatureCode,
        tf.LimitValue
FROM    dir.Shop AS s
JOIN    dir.ShopTier AS t     ON t.TierId = s.CurrentTierId
JOIN    dir.TierFeature AS tf ON tf.TierId = s.CurrentTierId AND tf.IsEnabled = 1;
GO

/* Public directory listing: website / email / social links are masked unless the tier allows them.
   FREE tier = no website or email links (per the three-tier structure). */
CREATE VIEW dir.vw_ShopPublicListing
AS
SELECT  s.ShopId,
        s.PublicId,
        s.Slug,
        s.Name,
        s.AddressLine1,
        s.City,
        s.StateCode,
        s.PostalCode,
        s.Latitude,
        s.Longitude,
        s.Phone,
        s.Description,
        s.PrimarySpecialty,
        t.TierCode,
        s.ClaimStatus,
        CASE WHEN EXISTS (SELECT 1 FROM dir.TierFeature f WHERE f.TierId = s.CurrentTierId AND f.FeatureCode = 'SHOW_WEBSITE_LINK' AND f.IsEnabled = 1)
             THEN s.WebsiteUrl END      AS WebsiteUrl,
        CASE WHEN EXISTS (SELECT 1 FROM dir.TierFeature f WHERE f.TierId = s.CurrentTierId AND f.FeatureCode = 'SHOW_EMAIL_LINK' AND f.IsEnabled = 1)
             THEN s.PublicEmail END     AS PublicEmail,
        CASE WHEN EXISTS (SELECT 1 FROM dir.TierFeature f WHERE f.TierId = s.CurrentTierId AND f.FeatureCode = 'SHOW_SOCIAL_LINKS' AND f.IsEnabled = 1)
             THEN s.FacebookUrl END     AS FacebookUrl,
        CASE WHEN EXISTS (SELECT 1 FROM dir.TierFeature f WHERE f.TierId = s.CurrentTierId AND f.FeatureCode = 'SHOW_SOCIAL_LINKS' AND f.IsEnabled = 1)
             THEN s.InstagramHandle END AS InstagramHandle,
        cp.ThumbnailUrl                 AS CoverThumbnailUrl,
        rv.ReviewCount,
        rv.AvgRating,
        ph.PhotoCount
FROM    dir.Shop AS s
JOIN    dir.ShopTier AS t ON t.TierId = s.CurrentTierId
LEFT JOIN ugc.ShopPhoto AS cp ON cp.PhotoId = s.CoverPhotoId AND cp.ModerationStatus = 'APPROVED'
OUTER APPLY (SELECT COUNT(*) AS ReviewCount, CAST(AVG(CAST(r.Rating AS DECIMAL(4,2))) AS DECIMAL(3,2)) AS AvgRating
             FROM ugc.ShopReview r WHERE r.ShopId = s.ShopId AND r.ModerationStatus = 'APPROVED') AS rv
OUTER APPLY (SELECT COUNT(*) AS PhotoCount
             FROM ugc.ShopPhoto p WHERE p.ShopId = s.ShopId AND p.ModerationStatus = 'APPROVED') AS ph
WHERE   s.ListingStatus = 'ACTIVE';
GO

/* New-shops rotation: eligible entries with an effective weight.
   EffectiveWeight = BaseWeight x tier multiplier x freshness (linear decay 1.0 -> 0.25 across the window). */
CREATE VIEW feed.vw_NewShopRotation
AS
SELECT  e.FeedEntryId,
        e.SlotCode,
        e.ShopId,
        s.Slug,
        s.Name,
        s.City,
        s.StateCode,
        t.TierCode,
        e.Reason,
        e.IsPinned,
        e.EligibleFrom,
        e.EligibleUntil,
        CAST(e.BaseWeight
             * ISNULL(w.WeightMultiplier, 1.0)
             * (1.0 - 0.75 * CAST(DATEDIFF(MINUTE, e.EligibleFrom, SYSUTCDATETIME()) AS FLOAT)
                           / NULLIF(DATEDIFF(MINUTE, e.EligibleFrom, e.EligibleUntil), 0))
             AS DECIMAL(9,4)) AS EffectiveWeight,
        (SELECT COUNT(*) FROM feed.FeedImpression i
          WHERE i.FeedEntryId = e.FeedEntryId AND i.ShownAt >= DATEADD(HOUR, -24, SYSUTCDATETIME())) AS Impressions24h
FROM    feed.NewShopFeedEntry AS e
JOIN    dir.Shop AS s ON s.ShopId = e.ShopId AND s.ListingStatus = 'ACTIVE'
JOIN    dir.ShopTier AS t ON t.TierId = s.CurrentTierId
LEFT JOIN feed.TierRotationWeight AS w ON w.TierId = s.CurrentTierId
JOIN    feed.RotationSlot AS rs ON rs.SlotCode = e.SlotCode AND rs.IsActive = 1
WHERE   e.IsActive = 1
  AND   SYSUTCDATETIME() >= e.EligibleFrom
  AND   SYSUTCDATETIME() <  e.EligibleUntil;
GO

-- Suggested: perks each user currently qualifies for
CREATE VIEW karma.vw_UserPerk
AS
SELECT  u.UserId,
        p.PerkCode
FROM    core.AppUser AS u
CROSS JOIN karma.Perk AS p
JOIN    karma.vw_UserKarma AS k ON k.UserId = u.UserId
WHERE   p.IsActive = 1
  AND   u.AccountStatus = 'ACTIVE'
  AND   u.KarmaStatus   = 'NORMAL'
  AND   (p.MinTotalKarma     IS NULL OR k.TotalKarma >= p.MinTotalKarma)
  AND   (p.MinAccountAgeDays IS NULL OR DATEDIFF(DAY, u.CreatedAt, SYSUTCDATETIME()) >= p.MinAccountAgeDays)
  AND   (p.MinCategoryCode   IS NULL OR
         (SELECT ISNULL(SUM(c.Karma), 0) FROM karma.vw_UserKarmaByCategory c WITH (NOEXPAND)
           WHERE c.UserId = u.UserId AND c.CategoryCode = p.MinCategoryCode) >= p.MinCategoryKarma)
  AND   (p.RequiredBadgeId   IS NULL OR EXISTS (SELECT 1 FROM karma.UserBadge b
                                                 WHERE b.UserId = u.UserId AND b.BadgeId = p.RequiredBadgeId AND b.RevokedAt IS NULL))
  AND   (p.MaxRemovalsLast90d IS NULL OR
         (SELECT COUNT(*) FROM ugc.ShopPhoto x
           WHERE x.UploadedByUserId = u.UserId AND x.ModerationStatus = 'REMOVED' AND x.ModeratedAt >= DATEADD(DAY, -90, SYSUTCDATETIME()))
       + (SELECT COUNT(*) FROM ugc.ShopReview y
           WHERE y.AuthorUserId = u.UserId AND y.ModerationStatus = 'REMOVED' AND y.ModeratedAt >= DATEADD(DAY, -90, SYSUTCDATETIME()))
         <= p.MaxRemovalsLast90d);
GO

-- Suggested: shops don't hold karma; they get a community-activity score derived from members' contributions
CREATE VIEW dir.vw_ShopCommunityScore
AS
SELECT  l.ShopId,
        SUM(l.Points)                                   AS CommunityPoints,
        COUNT(DISTINCT l.UserId)                        AS DistinctContributors,
        SUM(CASE WHEN l.CreatedAt >= DATEADD(DAY, -30, SYSUTCDATETIME()) THEN l.Points ELSE 0 END) AS CommunityPoints30d
FROM    karma.PointsLedger AS l
WHERE   l.ShopId IS NOT NULL
GROUP BY l.ShopId;
GO
/* =====================================================================
   10. STORED PROCEDURES: karma engine
   ===================================================================== */

/* Evaluate every active, rule-based badge the user doesn't hold yet; award those whose criteria are met.
   Counts are NET of reversals, so removed content doesn't count toward a badge.
   Badges are NOT auto-revoked when counts later drop (Reddit-style permanence); moderators revoke manually. */
CREATE PROCEDURE karma.usp_EvaluateBadges
    @UserId          BIGINT,
    @TriggerLedgerId BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Pass TINYINT = 0, @NewCount INT = 1;
    DECLARE @Awarded TABLE (UserBadgeId BIGINT NOT NULL, BadgeId INT NOT NULL);

    -- Loop because a badge bonus can push total karma over the next karma badge threshold
    WHILE @NewCount > 0 AND @Pass < 5
    BEGIN
        SET @Pass += 1;
        DELETE FROM @Awarded;

        ;WITH LiveRows AS
        (   -- original award rows that have not been reversed
            SELECT l.LedgerId, l.ActionCode, l.ShopId
            FROM   karma.PointsLedger AS l
            WHERE  l.UserId = @UserId
              AND  l.ReversesLedgerId IS NULL
              AND  NOT EXISTS (SELECT 1 FROM karma.PointsLedger r WHERE r.ReversesLedgerId = l.LedgerId)
        ),
        Metric AS
        (
            SELECT b.BadgeId,
                   b.Threshold,
                   CASE b.CriteriaType
                        WHEN 'ACTION_COUNT' THEN
                            (SELECT COUNT(*) FROM LiveRows lr WHERE lr.ActionCode = b.ActionCode)
                        WHEN 'DISTINCT_SHOPS' THEN
                            (SELECT COUNT(DISTINCT lr.ShopId) FROM LiveRows lr WHERE lr.ActionCode = b.ActionCode AND lr.ShopId IS NOT NULL)
                        WHEN 'DISTINCT_STATES' THEN
                            (SELECT COUNT(DISTINCT s.StateCode) FROM LiveRows lr JOIN dir.Shop s ON s.ShopId = lr.ShopId
                              WHERE lr.ActionCode = b.ActionCode)
                        WHEN 'CATEGORY_KARMA' THEN
                            (SELECT ISNULL(SUM(k.Karma), 0) FROM karma.vw_UserKarmaByCategory k WITH (NOEXPAND)
                              WHERE k.UserId = @UserId AND k.CategoryCode = b.CategoryCode)
                        WHEN 'TOTAL_KARMA' THEN
                            (SELECT ISNULL(SUM(k.Karma), 0) FROM karma.vw_UserKarmaByCategory k WITH (NOEXPAND)
                              WHERE k.UserId = @UserId)
                        WHEN 'ACCOUNT_AGE_DAYS' THEN
                            (SELECT DATEDIFF(DAY, u.CreatedAt, SYSUTCDATETIME()) FROM core.AppUser u WHERE u.UserId = @UserId)
                   END AS CurrentValue
            FROM   karma.BadgeDefinition AS b
            WHERE  b.IsActive = 1
              AND  b.CriteriaType <> 'MANUAL'
              AND  NOT EXISTS (SELECT 1 FROM karma.UserBadge ub WHERE ub.UserId = @UserId AND ub.BadgeId = b.BadgeId)
        )
        INSERT INTO karma.UserBadge (UserId, BadgeId, AwardSource, TriggerLedgerId)
        OUTPUT inserted.UserBadgeId, inserted.BadgeId INTO @Awarded (UserBadgeId, BadgeId)
        SELECT @UserId, m.BadgeId, 'RULE', @TriggerLedgerId
        FROM   Metric AS m
        WHERE  m.CurrentValue >= m.Threshold;

        SET @NewCount = @@ROWCOUNT;

        -- Suggested: optional one-time bonus points for earning a badge (idempotent by UserBadgeId)
        INSERT INTO karma.PointsLedger (UserId, ActionCode, CategoryCode, Points, SourceType, SourceId, SourceKey, Reason)
        SELECT @UserId, 'BADGE_BONUS', 'COMMUNITY', b.BonusPoints, 'BADGE', a.UserBadgeId,
               CONCAT('BADGE_BONUS:BADGE:', a.UserBadgeId), CONCAT(N'Badge earned: ', b.Name)
        FROM   @Awarded AS a
        JOIN   karma.BadgeDefinition AS b ON b.BadgeId = a.BadgeId
        WHERE  b.BonusPoints > 0
          AND  NOT EXISTS (SELECT 1 FROM karma.PointsLedger x WHERE x.SourceKey = CONCAT('BADGE_BONUS:BADGE:', a.UserBadgeId));
    END
END;
GO

/* Award points for one action. Idempotent: the same SourceKey can only ever be paid once.
   @Result: AWARDED | DUPLICATE | CAPPED | COOLDOWN | SELF_SHOP | SHOP_CAP | INELIGIBLE | INACTIVE_ACTION */
CREATE PROCEDURE karma.usp_AwardPoints
    @UserId          BIGINT,
    @ActionCode      VARCHAR(40),
    @SourceType      VARCHAR(20),
    @SourceId        BIGINT        = NULL,
    @ShopId          BIGINT        = NULL,
    @SourceKey       VARCHAR(200)  = NULL,
    @PointsOverride  INT           = NULL,   -- ADMIN_ADJUSTMENT only
    @Reason          NVARCHAR(300) = NULL,
    @ActorUserId     BIGINT        = NULL,
    @LedgerId        BIGINT        = NULL OUTPUT,
    @Result          VARCHAR(20)   = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @LedgerId = NULL;

    DECLARE @Category VARCHAR(20), @Points INT, @DailyCap INT, @Cooldown INT, @ShopCap INT,
            @BlockOwn BIT, @IsActive BIT, @Now DATETIME2(3) = SYSUTCDATETIME();

    SELECT @Category = a.CategoryCode, @Points = a.Points, @DailyCap = a.DailyPointCap,
           @Cooldown = a.CooldownSeconds, @ShopCap = a.PerShopLifetimeCap,
           @BlockOwn = a.BlockOnOwnShop, @IsActive = a.IsActive
    FROM   karma.ActionType AS a
    WHERE  a.ActionCode = @ActionCode;

    IF @Category IS NULL OR @IsActive = 0
    BEGIN
        SET @Result = 'INACTIVE_ACTION';
        RETURN;
    END;

    IF @PointsOverride IS NOT NULL
    BEGIN
        IF @ActionCode <> 'ADMIN_ADJUSTMENT' OR @Reason IS NULL OR @ActorUserId IS NULL
            THROW 51001, 'PointsOverride is allowed only for ADMIN_ADJUSTMENT with a Reason and ActorUserId.', 1;
        SET @Points = @PointsOverride;
    END;

    IF @Points = 0
    BEGIN
        SET @Result = 'INELIGIBLE';
        RETURN;
    END;

    SET @SourceKey = ISNULL(@SourceKey, CONCAT(@ActionCode, ':', @SourceType, ':', @SourceId));

    -- Account must be active with a verified email, and karma not frozen (admin adjustments bypass)
    IF @ActionCode <> 'ADMIN_ADJUSTMENT' AND NOT EXISTS (
        SELECT 1 FROM core.AppUser u
        WHERE  u.UserId = @UserId AND u.AccountStatus = 'ACTIVE'
          AND  u.EmailVerifiedAt IS NOT NULL AND u.KarmaStatus <> 'FROZEN')
    BEGIN
        SET @Result = 'INELIGIBLE';
        RETURN;
    END;

    -- Owners/managers/staff earn no karma on their own shop
    IF @BlockOwn = 1 AND @ShopId IS NOT NULL AND EXISTS (
        SELECT 1 FROM dir.ShopMember m WHERE m.ShopId = @ShopId AND m.UserId = @UserId AND m.RevokedAt IS NULL)
    BEGIN
        SET @Result = 'SELF_SHOP';
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Serialize awards per user so caps can't be raced
        DECLARE @LockName NVARCHAR(255) = CONCAT(N'karma:user:', @UserId);
        EXEC sp_getapplock @Resource = @LockName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;

        IF EXISTS (SELECT 1 FROM karma.PointsLedger WHERE SourceKey = @SourceKey)
        BEGIN
            COMMIT TRANSACTION;
            SET @Result = 'DUPLICATE';
            RETURN;
        END;

        IF @Cooldown IS NOT NULL AND @Cooldown > 0 AND EXISTS (
            SELECT 1 FROM karma.PointsLedger l
            WHERE  l.UserId = @UserId AND l.ActionCode = @ActionCode AND l.ReversesLedgerId IS NULL
              AND  l.CreatedAt > DATEADD(SECOND, -@Cooldown, @Now))
        BEGIN
            COMMIT TRANSACTION;
            SET @Result = 'COOLDOWN';
            RETURN;
        END;

        IF @ShopCap IS NOT NULL AND @ShopId IS NOT NULL AND (
            SELECT COUNT(*) FROM karma.PointsLedger l
            WHERE  l.UserId = @UserId AND l.ActionCode = @ActionCode AND l.ShopId = @ShopId
              AND  l.ReversesLedgerId IS NULL
              AND  NOT EXISTS (SELECT 1 FROM karma.PointsLedger r WHERE r.ReversesLedgerId = l.LedgerId)) >= @ShopCap
        BEGIN
            COMMIT TRANSACTION;
            SET @Result = 'SHOP_CAP';
            RETURN;
        END;

        IF @DailyCap IS NOT NULL AND @Points > 0
        BEGIN
            DECLARE @Today DATETIME2(3) = CAST(CAST(@Now AS DATE) AS DATETIME2(3));
            DECLARE @EarnedToday INT =
                (SELECT ISNULL(SUM(l.Points), 0) FROM karma.PointsLedger l
                 WHERE l.UserId = @UserId AND l.ActionCode = @ActionCode
                   AND l.ReversesLedgerId IS NULL AND l.CreatedAt >= @Today);
            IF @EarnedToday >= @DailyCap
            BEGIN
                COMMIT TRANSACTION;
                SET @Result = 'CAPPED';
                RETURN;
            END;
            IF @Points > @DailyCap - @EarnedToday
                SET @Points = @DailyCap - @EarnedToday;   -- partial award up to the cap
        END;

        INSERT INTO karma.PointsLedger (UserId, ActionCode, CategoryCode, Points, SourceType, SourceId, ShopId, SourceKey, Reason, CreatedByUserId, CreatedAt)
        VALUES (@UserId, @ActionCode, @Category, @Points, @SourceType, @SourceId, @ShopId, @SourceKey, @Reason, @ActorUserId, @Now);

        SET @LedgerId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
        SET @Result = 'AWARDED';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)   -- unique SourceKey hit by a concurrent caller
        BEGIN
            SET @Result = 'DUPLICATE';
            RETURN;
        END;
        THROW;
    END CATCH;

    EXEC karma.usp_EvaluateBadges @UserId = @UserId, @TriggerLedgerId = @LedgerId;
END;
GO

/* Claw back points: either one ledger row (@LedgerId) or every live award tied to a source
   (e.g. a photo later removed). Inserts negating rows; never edits history. */
CREATE PROCEDURE karma.usp_ReverseForSource
    @SourceType  VARCHAR(20)   = NULL,
    @SourceId    BIGINT        = NULL,
    @LedgerId    BIGINT        = NULL,
    @Reason       NVARCHAR(300),
    @ActorUserId  BIGINT        = NULL,
    @RowsReversed INT           = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Reason IS NULL OR (@LedgerId IS NULL AND (@SourceType IS NULL OR @SourceId IS NULL))
        THROW 51002, 'Provide @Reason and either @LedgerId or @SourceType + @SourceId.', 1;

    INSERT INTO karma.PointsLedger (UserId, ActionCode, CategoryCode, Points, SourceType, SourceId, ShopId,
                                    SourceKey, ReversesLedgerId, Reason, CreatedByUserId)
    SELECT l.UserId, l.ActionCode, l.CategoryCode, -l.Points, l.SourceType, l.SourceId, l.ShopId,
           CONCAT('REV:', l.LedgerId), l.LedgerId, @Reason, @ActorUserId
    FROM   karma.PointsLedger AS l
    WHERE  l.ReversesLedgerId IS NULL
      AND  ((@LedgerId IS NOT NULL AND l.LedgerId = @LedgerId)
         OR (@LedgerId IS NULL AND l.SourceType = @SourceType AND l.SourceId = @SourceId))
      AND  NOT EXISTS (SELECT 1 FROM karma.PointsLedger r WHERE r.ReversesLedgerId = l.LedgerId);

    SET @RowsReversed = @@ROWCOUNT;
END;
GO
/* =====================================================================
   11. STORED PROCEDURES: content workflows that feed karma
   ===================================================================== */

/* Cast / change / retract a vote. Photos: up/down. Reviews: helpful(+1)/unhelpful(-1). 0 = retract.
   The content author's karma moves with the vote; prior vote karma is reversed first. */
CREATE PROCEDURE ugc.usp_CastVote
    @VoterUserId BIGINT,
    @PhotoId     BIGINT   = NULL,
    @ReviewId    BIGINT   = NULL,
    @VoteValue   SMALLINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF (CASE WHEN @PhotoId IS NULL THEN 0 ELSE 1 END + CASE WHEN @ReviewId IS NULL THEN 0 ELSE 1 END) <> 1
        THROW 51010, 'Exactly one of @PhotoId / @ReviewId is required.', 1;
    IF @VoteValue NOT IN (-1, 0, 1)
        THROW 51011, '@VoteValue must be -1, 0 or 1.', 1;

    DECLARE @AuthorUserId BIGINT, @ShopId BIGINT, @Status VARCHAR(20);
    IF @PhotoId IS NOT NULL
        SELECT @AuthorUserId = p.UploadedByUserId, @ShopId = p.ShopId, @Status = p.ModerationStatus
        FROM ugc.ShopPhoto p WHERE p.PhotoId = @PhotoId;
    ELSE
        SELECT @AuthorUserId = r.AuthorUserId, @ShopId = r.ShopId, @Status = r.ModerationStatus
        FROM ugc.ShopReview r WHERE r.ReviewId = @ReviewId;

    IF @AuthorUserId IS NULL OR @Status <> 'APPROVED'
        THROW 51012, 'Content not found or not approved.', 1;
    IF @AuthorUserId = @VoterUserId
        THROW 51013, 'Users cannot vote on their own content.', 1;

    DECLARE @VoteId BIGINT, @OldValue SMALLINT = 0;

    BEGIN TRANSACTION;

    SELECT @VoteId = v.VoteId, @OldValue = v.VoteValue
    FROM   ugc.ContentVote v WITH (UPDLOCK, HOLDLOCK)
    WHERE  v.VoterUserId = @VoterUserId
      AND  ((@PhotoId IS NOT NULL AND v.PhotoId = @PhotoId) OR (@ReviewId IS NOT NULL AND v.ReviewId = @ReviewId));

    IF @VoteId IS NOT NULL AND @OldValue = @VoteValue
    BEGIN
        COMMIT TRANSACTION;
        RETURN;
    END;

    IF @VoteId IS NULL
    BEGIN
        INSERT INTO ugc.ContentVote (VoterUserId, PhotoId, ReviewId, VoteValue)
        VALUES (@VoterUserId, @PhotoId, @ReviewId, @VoteValue);
        SET @VoteId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE ugc.ContentVote SET VoteValue = @VoteValue, UpdatedAt = SYSUTCDATETIME() WHERE VoteId = @VoteId;

    DECLARE @dUp INT   = (CASE WHEN @VoteValue = 1  THEN 1 ELSE 0 END) - (CASE WHEN @OldValue = 1  THEN 1 ELSE 0 END);
    DECLARE @dDown INT = (CASE WHEN @VoteValue = -1 THEN 1 ELSE 0 END) - (CASE WHEN @OldValue = -1 THEN 1 ELSE 0 END);

    IF @PhotoId IS NOT NULL
        UPDATE ugc.ShopPhoto SET UpvoteCount = UpvoteCount + @dUp, DownvoteCount = DownvoteCount + @dDown WHERE PhotoId = @PhotoId;
    ELSE
        UPDATE ugc.ShopReview SET HelpfulCount = HelpfulCount + @dUp, UnhelpfulCount = UnhelpfulCount + @dDown WHERE ReviewId = @ReviewId;

    COMMIT TRANSACTION;

    -- Karma side effects (outside the vote transaction; each step is idempotent)
    EXEC karma.usp_ReverseForSource @SourceType = 'VOTE', @SourceId = @VoteId, @Reason = N'Vote changed or retracted';

    -- Votes from shop staff, or from unverified / frozen voters, move counters but not karma
    IF @VoteValue <> 0
       AND NOT EXISTS (SELECT 1 FROM dir.ShopMember m WHERE m.ShopId = @ShopId AND m.UserId = @VoterUserId AND m.RevokedAt IS NULL)
       AND EXISTS (SELECT 1 FROM core.AppUser u WHERE u.UserId = @VoterUserId AND u.AccountStatus = 'ACTIVE'
                                                 AND u.EmailVerifiedAt IS NOT NULL AND u.KarmaStatus <> 'FROZEN')
    BEGIN
        DECLARE @Action VARCHAR(40) =
            CASE WHEN @PhotoId IS NOT NULL AND @VoteValue = 1  THEN 'PHOTO_UPVOTE_RECEIVED'
                 WHEN @PhotoId IS NOT NULL AND @VoteValue = -1 THEN 'PHOTO_DOWNVOTE_RECEIVED'
                 WHEN @VoteValue = 1                            THEN 'REVIEW_HELPFUL_RECEIVED'
                 ELSE 'REVIEW_UNHELPFUL_RECEIVED' END;
        DECLARE @Key VARCHAR(200) = CONCAT(@Action, ':VOTE:', @VoteId, ':', DATEDIFF_BIG(MILLISECOND, '2020-01-01', SYSUTCDATETIME()));
        DECLARE @Res VARCHAR(20);
        EXEC karma.usp_AwardPoints @UserId = @AuthorUserId, @ActionCode = @Action, @SourceType = 'VOTE',
             @SourceId = @VoteId, @ShopId = @ShopId, @SourceKey = @Key, @Result = @Res OUTPUT;
    END;
END;
GO

/* Moderator approves a pending photo: sets first-photo / cover, then awards karma. */
CREATE PROCEDURE ugc.usp_ApprovePhoto
    @PhotoId         BIGINT,
    @ModeratorUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ShopId BIGINT, @UserId BIGINT, @IsFirst BIT = 0, @ClaimStatus VARCHAR(20), @TierCode VARCHAR(20);

    BEGIN TRANSACTION;

    SELECT @ShopId = p.ShopId, @UserId = p.UploadedByUserId
    FROM   ugc.ShopPhoto p WITH (UPDLOCK, HOLDLOCK)
    WHERE  p.PhotoId = @PhotoId AND p.ModerationStatus = 'PENDING';

    IF @ShopId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51020, 'Photo not found or not pending.', 1;
    END;

    -- lock the shop row so two simultaneous approvals can't both be "first"
    SELECT @ClaimStatus = s.ClaimStatus, @TierCode = t.TierCode
    FROM   dir.Shop s WITH (UPDLOCK, HOLDLOCK)
    JOIN   dir.ShopTier t ON t.TierId = s.CurrentTierId
    WHERE  s.ShopId = @ShopId;

    IF NOT EXISTS (SELECT 1 FROM ugc.ShopPhoto x WHERE x.ShopId = @ShopId AND x.WasFirstApprovedForShop = 1)
        SET @IsFirst = 1;

    UPDATE ugc.ShopPhoto
    SET    ModerationStatus = 'APPROVED', ModeratedByUserId = @ModeratorUserId, ModeratedAt = SYSUTCDATETIME(),
           WasFirstApprovedForShop = @IsFirst
    WHERE  PhotoId = @PhotoId;

    UPDATE dir.Shop SET CoverPhotoId = @PhotoId, UpdatedAt = SYSUTCDATETIME()
    WHERE  ShopId = @ShopId AND CoverPhotoId IS NULL;

    INSERT INTO ugc.ModerationAction (ModeratorUserId, TargetType, TargetId, ActionCode)
    VALUES (@ModeratorUserId, 'PHOTO', @PhotoId, 'APPROVE');

    COMMIT TRANSACTION;

    DECLARE @Res VARCHAR(20);
    EXEC karma.usp_AwardPoints @UserId = @UserId, @ActionCode = 'PHOTO_APPROVED', @SourceType = 'PHOTO',
         @SourceId = @PhotoId, @ShopId = @ShopId, @Result = @Res OUTPUT;

    IF @IsFirst = 1
        EXEC karma.usp_AwardPoints @UserId = @UserId, @ActionCode = 'PHOTO_FIRST_FOR_SHOP', @SourceType = 'PHOTO',
             @SourceId = @PhotoId, @ShopId = @ShopId, @Result = @Res OUTPUT;

    -- Community helps fill out unclaimed free listings: small bonus
    IF @ClaimStatus = 'UNCLAIMED' AND @TierCode = 'FREE'
        EXEC karma.usp_AwardPoints @UserId = @UserId, @ActionCode = 'PHOTO_UNCLAIMED_SHOP_BONUS', @SourceType = 'PHOTO',
             @SourceId = @PhotoId, @ShopId = @ShopId, @Result = @Res OUTPUT;
END;
GO

/* =====================================================================
   12. STORED PROCEDURES: new-shops rotation feed
   ===================================================================== */

-- Run on a schedule (e.g. every 15 min): expire old entries, add newly published / newly claimed shops
CREATE PROCEDURE feed.usp_RefreshNewShopFeed
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

    BEGIN TRANSACTION;

    UPDATE feed.NewShopFeedEntry
    SET    IsActive = 0
    WHERE  IsActive = 1 AND EligibleUntil <= @Now;

    ;WITH Candidate AS
    (
        SELECT s.ShopId,
               CASE WHEN s.ClaimedAt IS NOT NULL AND s.ClaimedAt > s.PublishedAt THEN 'NEWLY_CLAIMED' ELSE 'NEW_LISTING' END AS Reason,
               CASE WHEN s.ClaimedAt IS NOT NULL AND s.ClaimedAt > s.PublishedAt THEN s.ClaimedAt ELSE s.PublishedAt END AS AnchorAt,
               s.CurrentTierId
        FROM   dir.Shop s
        WHERE  s.ListingStatus = 'ACTIVE' AND s.PublishedAt IS NOT NULL
    )
    INSERT INTO feed.NewShopFeedEntry (SlotCode, ShopId, Reason, BaseWeight, EligibleFrom, EligibleUntil)
    SELECT rs.SlotCode, c.ShopId, c.Reason, 1.00, c.AnchorAt,
           DATEADD(DAY, ISNULL(w.WindowDays, rs.DefaultWindowDays), c.AnchorAt)
    FROM   Candidate c
    CROSS JOIN feed.RotationSlot rs
    LEFT JOIN feed.TierRotationWeight w ON w.TierId = c.CurrentTierId
    WHERE  rs.IsActive = 1
      AND  DATEADD(DAY, ISNULL(w.WindowDays, rs.DefaultWindowDays), c.AnchorAt) > @Now
      AND  NOT EXISTS (SELECT 1 FROM feed.NewShopFeedEntry e
                       WHERE e.SlotCode = rs.SlotCode AND e.ShopId = c.ShopId
                         AND (e.IsActive = 1 OR e.EligibleFrom >= c.AnchorAt));

    COMMIT TRANSACTION;
END;
GO

/* Serve a slot: pinned entries first, then weighted-random (Efraimidis-Spirakis: sort by -ln(U)/w).
   Every page view gets a different, fair mix; higher tiers and fresher shops show up more often. */
CREATE PROCEDURE feed.usp_GetNewShopRotation
    @SlotCode       VARCHAR(30),
    @UserId         BIGINT     = NULL,
    @SessionHash    BINARY(32) = NULL,
    @LogImpressions BIT        = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Max TINYINT = (SELECT MaxItems FROM feed.RotationSlot WHERE SlotCode = @SlotCode AND IsActive = 1);
    IF @Max IS NULL
        THROW 51030, 'Unknown or inactive rotation slot.', 1;

    -- Materialize the random sort key once per row so ranking and ordering agree
    DECLARE @Cand TABLE (FeedEntryId BIGINT NOT NULL, ShopId BIGINT NOT NULL, IsPinned BIT NOT NULL, SortKey FLOAT NOT NULL);
    DECLARE @Pick TABLE (Position TINYINT NOT NULL, FeedEntryId BIGINT NOT NULL, ShopId BIGINT NOT NULL);

    INSERT INTO @Cand (FeedEntryId, ShopId, IsPinned, SortKey)
    SELECT r.FeedEntryId, r.ShopId, r.IsPinned,
           -LOG((ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 1000000 + 1) / 1000001.0)
             / CAST(CASE WHEN r.EffectiveWeight > 0 THEN r.EffectiveWeight ELSE 0.01 END AS FLOAT)
    FROM   feed.vw_NewShopRotation AS r
    WHERE  r.SlotCode = @SlotCode;

    INSERT INTO @Pick (Position, FeedEntryId, ShopId)
    SELECT TOP (@Max)
           CAST(ROW_NUMBER() OVER (ORDER BY c.IsPinned DESC, c.SortKey ASC) AS TINYINT),
           c.FeedEntryId, c.ShopId
    FROM   @Cand AS c
    ORDER BY c.IsPinned DESC, c.SortKey ASC;

    IF @LogImpressions = 1
        INSERT INTO feed.FeedImpression (FeedEntryId, Position, UserId, SessionHash)
        SELECT p.FeedEntryId, p.Position, @UserId, @SessionHash FROM @Pick p;

    SELECT p.Position, l.*
    FROM   @Pick p
    JOIN   dir.vw_ShopPublicListing l ON l.ShopId = p.ShopId
    ORDER BY p.Position;
END;
GO
/* =====================================================================
   13. REFERENCE DATA (seed). Point values / caps / thresholds are proposed
   defaults to tune; PAID and PREMIUM prices are intentionally NULL (TBD).
   ===================================================================== */
INSERT INTO dir.ShopTier (TierId, TierCode, Name, MonthlyPriceCents, AnnualPriceCents, SortOrder) VALUES
 (1, 'FREE',    N'Free Listing',    0,    0,    1),
 (2, 'PAID',    N'Featured Listing', NULL, NULL, 2),   -- price TBD
 (3, 'PREMIUM', N'Premium Partner',  NULL, NULL, 3);   -- price TBD
GO

INSERT INTO dir.Feature (FeatureCode, Name, Description) VALUES
 ('BASIC_LISTING',     N'Directory listing',          N'Name, address, phone, hours, photos, reviews'),
 ('SHOW_WEBSITE_LINK', N'Website link',               N'Clickable website link on the listing'),
 ('SHOW_EMAIL_LINK',   N'Email link',                 N'Public email / contact link on the listing'),
 ('SHOW_SOCIAL_LINKS', N'Social links',               N'Facebook / Instagram links on the listing'),
 ('OWNER_RESPONSES',   N'Reply to reviews',           N'Owner can post a public reply to reviews'),
 ('EVENT_CALENDAR',    N'Event calendar',             N'Shop events on listing and site calendar'),
 ('EMAIL_PROMOTION',   N'Email promotion',            N'Shop promos in NTCA email; LimitValue = campaigns per month'),
 ('SOCIAL_PROMOTION',  N'Social promotion',           N'Shop promos on NTCA social; LimitValue = posts per month'),
 ('MAILBOX_PARSING',   N'Mailbox parsing',            N'Inbound mailbox parsed into events / promos'),
 ('SOCIAL_MONITORING', N'Facebook/Instagram monitoring', N'Shop FB/IG posts monitored and turned into events / promos'),
 ('ROTATION_BOOST',    N'Rotation boost',             N'Higher weight / longer window in the new-shops rotation');
GO

INSERT INTO dir.TierFeature (TierId, FeatureCode, IsEnabled, LimitValue) VALUES
 (1, 'BASIC_LISTING', 1, NULL),
 (2, 'BASIC_LISTING', 1, NULL), (2, 'SHOW_WEBSITE_LINK', 1, NULL), (2, 'SHOW_EMAIL_LINK', 1, NULL),
 (2, 'SHOW_SOCIAL_LINKS', 1, NULL), (2, 'OWNER_RESPONSES', 1, NULL),
 (3, 'BASIC_LISTING', 1, NULL), (3, 'SHOW_WEBSITE_LINK', 1, NULL), (3, 'SHOW_EMAIL_LINK', 1, NULL),
 (3, 'SHOW_SOCIAL_LINKS', 1, NULL), (3, 'OWNER_RESPONSES', 1, NULL), (3, 'EVENT_CALENDAR', 1, NULL),
 (3, 'EMAIL_PROMOTION', 1, 4), (3, 'SOCIAL_PROMOTION', 1, 4), (3, 'MAILBOX_PARSING', 1, NULL),
 (3, 'SOCIAL_MONITORING', 1, NULL), (3, 'ROTATION_BOOST', 1, NULL);
GO

INSERT INTO feed.RotationSlot (SlotCode, Name, MaxItems, DefaultWindowDays, RefreshEveryMinutes) VALUES
 ('HOME_NEW_SHOPS',    N'Homepage: New shops',      6, 30, 60),
 ('DIRECTORY_SIDEBAR', N'Directory sidebar',        4, 30, 30),
 ('NEWSLETTER',        N'Newsletter: New this month', 8, 31, 1440);
GO

-- Suggested multipliers
INSERT INTO feed.TierRotationWeight (TierId, WeightMultiplier, WindowDays) VALUES
 (1, 1.00, 30),
 (2, 1.50, 45),
 (3, 2.50, 60);
GO

INSERT INTO karma.Category (CategoryCode, Name, DisplayOrder) VALUES
 ('PHOTO',     N'Photo karma',     1),
 ('REVIEW',    N'Review karma',    2),
 ('FEEDBACK',  N'Feedback karma',  3),
 ('SHOP_HELP', N'Shop-help karma', 4),
 ('COMMUNITY', N'Community karma', 5);
GO

INSERT INTO karma.ActionType
 (ActionCode, CategoryCode, Name, Points, DailyPointCap, CooldownSeconds, PerShopLifetimeCap, RequiresModeration, BlockOnOwnShop) VALUES
 ('PHOTO_APPROVED',               'PHOTO',     N'Shop photo approved',                    5,  50,   30,   10,   1, 1),
 ('PHOTO_FIRST_FOR_SHOP',         'PHOTO',     N'First approved photo of a shop',        10,  NULL, NULL, 1,    1, 1),
 ('PHOTO_UNCLAIMED_SHOP_BONUS',   'PHOTO',     N'Photo of an unclaimed free-tier shop',   3,  15,   NULL, 1,    1, 1),
 ('PHOTO_UPVOTE_RECEIVED',        'PHOTO',     N'Upvote received on a photo',             1,  50,   NULL, NULL, 0, 1),
 ('PHOTO_DOWNVOTE_RECEIVED',      'PHOTO',     N'Downvote received on a photo',          -1,  NULL, NULL, NULL, 0, 1),
 ('REVIEW_APPROVED',              'REVIEW',    N'Review approved',                       10,  50,   300,  1,    1, 1),
 ('REVIEW_DETAILED_BONUS',        'REVIEW',    N'Detailed review bonus (length/photos)',  5,  25,   NULL, 1,    1, 1),
 ('REVIEW_HELPFUL_RECEIVED',      'REVIEW',    N'Helpful vote received on a review',      1,  50,   NULL, NULL, 0, 1),
 ('REVIEW_UNHELPFUL_RECEIVED',    'REVIEW',    N'Unhelpful vote received on a review',   -1,  NULL, NULL, NULL, 0, 1),
 ('FEEDBACK_ACCEPTED',            'FEEDBACK',  N'Site / general feedback accepted',       3,  15,   NULL, NULL, 1, 0),
 ('LISTING_CORRECTION_ACCEPTED',  'FEEDBACK',  N'Listing correction accepted',            5,  25,   NULL, 3,    1, 1),
 ('EVENT_TIP_ACCEPTED',           'FEEDBACK',  N'Event tip accepted',                     3,  15,   NULL, NULL, 1, 1),
 ('NEW_SHOP_SUGGESTION_ACCEPTED', 'SHOP_HELP', N'Suggested a new shop that was added',   15,  60,   NULL, 1,    1, 1),
 ('CLAIM_REFERRAL_APPROVED',      'SHOP_HELP', N'Referred an owner whose claim was approved', 25, NULL, NULL, 1, 1, 1),
 ('BADGE_BONUS',                  'COMMUNITY', N'Bonus for earning a badge',              0,  NULL, NULL, NULL, 0, 0),
 ('ADMIN_ADJUSTMENT',             'COMMUNITY', N'Manual adjustment by staff',             0,  NULL, NULL, NULL, 0, 0);
GO

INSERT INTO karma.BadgeDefinition
 (BadgeCode, Name, Description, BadgeFamily, BadgeLevel, CriteriaType, ActionCode, CategoryCode, Threshold, DisplayOrder) VALUES
 ('FIRST_PHOTO',      N'First Snapshot',  N'First shop photo approved',                         'PHOTO',        1, 'ACTION_COUNT',    'PHOTO_APPROVED',               NULL, 1,     10),
 ('PHOTOGRAPHER_10',  N'Shutterbug',      N'10 shop photos approved',                           'PHOTO',        2, 'ACTION_COUNT',    'PHOTO_APPROVED',               NULL, 10,    11),
 ('PHOTOGRAPHER_50',  N'Shop Photographer', N'50 shop photos approved',                         'PHOTO',        3, 'ACTION_COUNT',    'PHOTO_APPROVED',               NULL, 50,    12),
 ('TRAILBLAZER',      N'Trailblazer',     N'First person to photograph a shop',                 'TRAILBLAZER',  1, 'ACTION_COUNT',    'PHOTO_FIRST_FOR_SHOP',         NULL, 1,     13),
 ('FIRST_REVIEW',     N'First Review',    N'First review approved',                             'REVIEW',       1, 'ACTION_COUNT',    'REVIEW_APPROVED',              NULL, 1,     20),
 ('REVIEWER_10',      N'Regular',         N'10 reviews approved',                               'REVIEW',       2, 'ACTION_COUNT',    'REVIEW_APPROVED',              NULL, 10,    21),
 ('REVIEWER_50',      N'Critic',          N'50 reviews approved',                               'REVIEW',       3, 'ACTION_COUNT',    'REVIEW_APPROVED',              NULL, 50,    22),
 ('HELPFUL_100',      N'Trusted Voice',   N'100 helpful votes on your reviews',                 'HELPFUL',      1, 'ACTION_COUNT',    'REVIEW_HELPFUL_RECEIVED',      NULL, 100,   23),
 ('ROAD_TRIP_5',      N'Road Trip',       N'Reviewed shops in 5 different states',              'ROAD_TRIP',    1, 'DISTINCT_STATES', 'REVIEW_APPROVED',              NULL, 5,     30),
 ('SHOP_HOPPER_25',   N'Shop Hopper',     N'Reviewed 25 different shops',                       'SHOP_HOPPER',  1, 'DISTINCT_SHOPS',  'REVIEW_APPROVED',              NULL, 25,    31),
 ('SHOP_SCOUT',       N'Shop Scout',      N'Suggested a shop that was added to the directory',  'SCOUT',        1, 'ACTION_COUNT',    'NEW_SHOP_SUGGESTION_ACCEPTED', NULL, 1,     40),
 ('MATCHMAKER',       N'Matchmaker',      N'Brought a shop owner in to claim their listing',    'MATCHMAKER',   1, 'ACTION_COUNT',    'CLAIM_REFERRAL_APPROVED',      NULL, 1,     41),
 ('FACT_CHECKER_10',  N'Fact Checker',    N'10 listing corrections accepted',                   'FACT_CHECKER', 1, 'ACTION_COUNT',    'LISTING_CORRECTION_ACCEPTED',  NULL, 10,    42),
 ('KARMA_100',        N'Rookie Card',     N'Reached 100 karma',                                 'KARMA',        1, 'TOTAL_KARMA',     NULL,                           NULL, 100,   50),
 ('KARMA_1000',       N'All-Star',        N'Reached 1,000 karma',                               'KARMA',        2, 'TOTAL_KARMA',     NULL,                           NULL, 1000,  51),
 ('KARMA_10000',      N'Hall of Famer',   N'Reached 10,000 karma',                              'KARMA',        3, 'TOTAL_KARMA',     NULL,                           NULL, 10000, 52),
 ('ONE_YEAR',         N'One-Year Club',   N'Member for one year',                               'TENURE',       1, 'ACCOUNT_AGE_DAYS', NULL,                          NULL, 365,   60),
 ('FOUNDING_MEMBER',  N'Founding Member', N'Joined during the NTCA founding period (staff-awarded)', 'FOUNDING', 1, 'MANUAL',         NULL,                           NULL, NULL,  1);
GO

-- Suggested: Reddit-style karma-gated privileges
INSERT INTO karma.Perk (PerkCode, Name, Description, MinTotalKarma, MinCategoryCode, MinCategoryKarma, MinAccountAgeDays, MaxRemovalsLast90d) VALUES
 ('AUTO_APPROVE_REVIEWS', N'Reviews skip the queue', N'Reviews publish immediately (still reportable)', NULL, 'REVIEW', 150, 60,  0),
 ('AUTO_APPROVE_PHOTOS',  N'Photos skip the queue',  N'Photos publish immediately (still reportable)',  NULL, 'PHOTO',  150, 60,  0),
 ('DIRECT_LISTING_EDITS', N'Direct listing edits',   N'Edit unclaimed listings without review',          1000, NULL,    NULL, 180, 0),
 ('PRIORITY_FLAGS',       N'Priority reports',       N'Reports jump the moderation queue',               250,  NULL,    NULL, 30,  1);
GO
