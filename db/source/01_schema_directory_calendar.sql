/* =====================================================================
   Directory extras (manufacturers, organizations) + National Card Show
   Calendar (promoters, venues, exhibitors, submissions, sitemap, feed).
   Applied after 01_schema_additions.sql. Draft 2026-10-23.
   No geography persisted columns (app computes radius).
   ===================================================================== */

/* ---------------------------------------------------------------------
   Import marker on shops (CTCA and later imports). Not public branding.
   --------------------------------------------------------------------- */
ALTER TABLE dir.Shop ADD
    SourceSystem NVARCHAR(40)  NULL,
    ImportNotes  NVARCHAR(500) NULL;
GO

/* ---------------------------------------------------------------------
   Manufacturers (Wikipedia Trading card article + later merges)
   --------------------------------------------------------------------- */
CREATE TABLE dir.Manufacturer
(
    ManufacturerId   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Manufacturer PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Manufacturer_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug             NVARCHAR(160)  NOT NULL,
    Name             NVARCHAR(200)  NOT NULL,
    NormalizedName   NVARCHAR(200)  NOT NULL,
    CountryName      NVARCHAR(80)   NULL,
    WebsiteUrl       NVARCHAR(500)  NULL,
    WikipediaTitle   NVARCHAR(200)  NULL,
    SourceSection    VARCHAR(40)    NOT NULL,
    SourceSystem     NVARCHAR(40)   NULL,
    RetrievedOn      DATE           NULL,
    IsActive         BIT            NOT NULL CONSTRAINT DF_Manufacturer_Active DEFAULT 1,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_Manufacturer_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_Manufacturer_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Manufacturer_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Manufacturer_Slug UNIQUE (Slug),
    CONSTRAINT CK_Manufacturer_Section CHECK (SourceSection IN ('Manufacturers','Non-sports','Import','NTCA'))
);
GO
CREATE UNIQUE INDEX UX_Manufacturer_Normalized ON dir.Manufacturer (NormalizedName);
GO

CREATE TABLE dir.ManufacturerCategory
(
    CategoryCode  VARCHAR(20)   NOT NULL CONSTRAINT PK_ManufacturerCategory PRIMARY KEY,
    Name          NVARCHAR(80)  NOT NULL,
    SortOrder     TINYINT       NOT NULL
);
GO

CREATE TABLE dir.ManufacturerCategoryMap
(
    ManufacturerId BIGINT      NOT NULL CONSTRAINT FK_MfrCatMap_Mfr REFERENCES dir.Manufacturer(ManufacturerId),
    CategoryCode   VARCHAR(20) NOT NULL CONSTRAINT FK_MfrCatMap_Cat REFERENCES dir.ManufacturerCategory(CategoryCode),
    CONSTRAINT PK_ManufacturerCategoryMap PRIMARY KEY (ManufacturerId, CategoryCode)
);
GO

CREATE TABLE dir.ManufacturerTopic
(
    ManufacturerTopicId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManufacturerTopic PRIMARY KEY,
    ManufacturerId      BIGINT        NOT NULL CONSTRAINT FK_MfrTopic_Mfr REFERENCES dir.Manufacturer(ManufacturerId),
    TopicGroup          VARCHAR(20)   NOT NULL,
    TopicName           NVARCHAR(80)  NOT NULL,
    CONSTRAINT UQ_ManufacturerTopic UNIQUE (ManufacturerId, TopicGroup, TopicName),
    CONSTRAINT CK_MfrTopic_Group CHECK (TopicGroup IN ('SPORT','NONSPORT'))
);
GO

CREATE TABLE dir.ManufacturerProduct
(
    ManufacturerProductId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManufacturerProduct PRIMARY KEY,
    ManufacturerId        BIGINT        NOT NULL CONSTRAINT FK_MfrProduct_Mfr REFERENCES dir.Manufacturer(ManufacturerId),
    Slug                  NVARCHAR(160) NOT NULL,
    Name                  NVARCHAR(200) NOT NULL,
    ReleasedYear          SMALLINT      NULL,
    CardCount             SMALLINT      NULL,
    CategoryCode          VARCHAR(20)   NULL CONSTRAINT FK_MfrProduct_Cat REFERENCES dir.ManufacturerCategory(CategoryCode),
    SourceSection         VARCHAR(40)   NULL,
    CONSTRAINT UQ_ManufacturerProduct_Slug UNIQUE (Slug)
);
GO

CREATE TABLE dir.ManufacturerLink
(
    ManufacturerLinkId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ManufacturerLink PRIMARY KEY,
    ManufacturerId     BIGINT        NOT NULL CONSTRAINT FK_MfrLink_Mfr REFERENCES dir.Manufacturer(ManufacturerId),
    Rel                VARCHAR(30)   NOT NULL,
    Url                NVARCHAR(500) NOT NULL,
    CONSTRAINT CK_MfrLink_Rel CHECK (Rel IN ('WIKIPEDIA','WEBSITE','SAMEAS'))
);
GO

/* ---------------------------------------------------------------------
   Organizations: graders, marketplaces, breakers, media, services, other
   --------------------------------------------------------------------- */
CREATE TABLE dir.Organization
(
    OrganizationId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Organization PRIMARY KEY,
    PublicId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Organization_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug            NVARCHAR(160)  NOT NULL,
    Name            NVARCHAR(200)  NOT NULL,
    NormalizedName  NVARCHAR(200)  NOT NULL,
    OrgType         VARCHAR(20)    NOT NULL,
    AddressLine1    NVARCHAR(200)  NULL,
    City            NVARCHAR(100)  NULL,
    StateCode       CHAR(2)        NULL,
    PostalCode      VARCHAR(10)    NULL,
    CountryCode     CHAR(2)        NOT NULL CONSTRAINT DF_Organization_Country DEFAULT 'US',
    Latitude        DECIMAL(9,6)   NULL,
    Longitude       DECIMAL(9,6)   NULL,
    Phone           VARCHAR(25)    NULL,
    WebsiteUrl      NVARCHAR(500)  NULL,
    PublicEmail     NVARCHAR(320)  NULL,
    Description     NVARCHAR(2000) NULL,
    Source          NVARCHAR(100)  NULL,
    SourceRef       NVARCHAR(200)  NULL,
    SourceSystem    NVARCHAR(40)   NULL,
    ImportNotes     NVARCHAR(500)  NULL,
    ListingStatus   VARCHAR(20)    NOT NULL CONSTRAINT DF_Organization_Status DEFAULT 'ACTIVE',
    CreatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_Organization_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)   NOT NULL CONSTRAINT DF_Organization_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Organization_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Organization_Slug UNIQUE (Slug),
    CONSTRAINT CK_Organization_Type CHECK (OrgType IN
        ('GRADER','MARKETPLACE','BREAKER','MEDIA','PROMOTER','INSURANCE','SERVICES','OTHER')),
    CONSTRAINT CK_Organization_Status CHECK (ListingStatus IN ('PENDING','ACTIVE','HIDDEN','CLOSED')),
    CONSTRAINT CK_Organization_Lat CHECK (Latitude  IS NULL OR Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_Organization_Lng CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);
GO
CREATE INDEX IX_Organization_Type ON dir.Organization (OrgType, ListingStatus);
CREATE INDEX IX_Organization_State ON dir.Organization (StateCode, City) WHERE StateCode IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   Promoters and venues (national calendar)
   --------------------------------------------------------------------- */
CREATE TABLE mkt.PromoterTier
(
    PromoterTierId    TINYINT       NOT NULL CONSTRAINT PK_PromoterTier PRIMARY KEY,
    TierCode          VARCHAR(20)   NOT NULL,
    Name              NVARCHAR(80)  NOT NULL,
    MonthlyPriceCents INT           NULL,
    SortOrder         TINYINT       NOT NULL,
    IsActive          BIT           NOT NULL CONSTRAINT DF_PromoterTier_Active DEFAULT 1,
    CONSTRAINT UQ_PromoterTier_Code UNIQUE (TierCode)
);
GO

CREATE TABLE mkt.Promoter
(
    PromoterId       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promoter PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Promoter_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug             VARCHAR(200)   NOT NULL,
    Name             NVARCHAR(200)  NOT NULL,
    WebsiteUrl       NVARCHAR(500)  NULL,
    Email            NVARCHAR(256)  NULL,
    Phone            VARCHAR(30)    NULL,
    SocialJson       NVARCHAR(1000) NULL,
    ShopId           BIGINT         NULL CONSTRAINT FK_Promoter_Shop REFERENCES dir.Shop(ShopId),
    OrganizationId   BIGINT         NULL CONSTRAINT FK_Promoter_Org REFERENCES dir.Organization(OrganizationId),
    CurrentTierId    TINYINT        NOT NULL CONSTRAINT DF_Promoter_Tier DEFAULT 1
                                     CONSTRAINT FK_Promoter_Tier REFERENCES mkt.PromoterTier(PromoterTierId),
    IsVerified       BIT            NOT NULL CONSTRAINT DF_Promoter_Verified DEFAULT 0,
    ClaimStatus      VARCHAR(20)    NOT NULL CONSTRAINT DF_Promoter_Claim DEFAULT 'UNCLAIMED',
    SourceSystem     NVARCHAR(40)   NULL,
    ImportNotes      NVARCHAR(500)  NULL,
    CreatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_Promoter_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(3)   NOT NULL CONSTRAINT DF_Promoter_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Promoter_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Promoter_Slug UNIQUE (Slug),
    CONSTRAINT CK_Promoter_Claim CHECK (ClaimStatus IN ('UNCLAIMED','PENDING','CLAIMED'))
);
GO

CREATE TABLE mkt.PromoterMember
(
    PromoterId      BIGINT       NOT NULL CONSTRAINT FK_PromoterMember_Promoter REFERENCES mkt.Promoter(PromoterId),
    UserId          BIGINT       NOT NULL CONSTRAINT FK_PromoterMember_User REFERENCES core.AppUser(UserId),
    Role            VARCHAR(20)  NOT NULL,
    GrantedAt       DATETIME2(3) NOT NULL CONSTRAINT DF_PromoterMember_Granted DEFAULT SYSUTCDATETIME(),
    RevokedAt       DATETIME2(3) NULL,
    CONSTRAINT PK_PromoterMember PRIMARY KEY (PromoterId, UserId),
    CONSTRAINT CK_PromoterMember_Role CHECK (Role IN ('OWNER','EDITOR'))
);
GO

CREATE TABLE mkt.PromoterClaimRequest
(
    PromoterClaimRequestId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PromoterClaimRequest PRIMARY KEY,
    PromoterId             BIGINT         NOT NULL CONSTRAINT FK_PromoterClaim_Promoter REFERENCES mkt.Promoter(PromoterId),
    RequestedByUserId      BIGINT         NOT NULL CONSTRAINT FK_PromoterClaim_User REFERENCES core.AppUser(UserId),
    ClaimType              VARCHAR(20)    NOT NULL CONSTRAINT DF_PromoterClaim_Type DEFAULT 'OWNER',
    Status                 VARCHAR(20)    NOT NULL CONSTRAINT DF_PromoterClaim_Status DEFAULT 'PENDING',
    VerificationMethod     VARCHAR(20)    NULL,
    VerificationCodeHash   VARBINARY(32)  NULL,
    VerificationSentAt     DATETIME2(3)   NULL,
    VerifiedAt             DATETIME2(3)   NULL,
    ContactName            NVARCHAR(120)  NULL,
    ContactPhone           VARCHAR(25)    NULL,
    ContactEmail           NVARCHAR(320)  NULL,
    EvidenceUrl            NVARCHAR(500)  NULL,
    Notes                  NVARCHAR(1000) NULL,
    SubmittedAt            DATETIME2(3)   NOT NULL CONSTRAINT DF_PromoterClaim_Submitted DEFAULT SYSUTCDATETIME(),
    ReviewedByUserId       BIGINT         NULL CONSTRAINT FK_PromoterClaim_ReviewedBy REFERENCES core.AppUser(UserId),
    ReviewedAt             DATETIME2(3)   NULL,
    DecisionReason         NVARCHAR(500)  NULL,
    CONSTRAINT CK_PromoterClaim_Type   CHECK (ClaimType IN ('OWNER','ADOPTER')),
    CONSTRAINT CK_PromoterClaim_Status CHECK (Status IN ('PENDING','VERIFYING','APPROVED','REJECTED','WITHDRAWN','EXPIRED')),
    CONSTRAINT CK_PromoterClaim_Method CHECK (VerificationMethod IS NULL OR VerificationMethod IN
        ('EMAIL_DOMAIN','PHONE_CALL','POSTCARD_CODE','DOCUMENT','SOCIAL_DM','ADMIN'))
);
GO
CREATE UNIQUE INDEX UX_PromoterClaim_Open ON mkt.PromoterClaimRequest (PromoterId, RequestedByUserId)
    WHERE Status IN ('PENDING','VERIFYING');
CREATE INDEX IX_PromoterClaim_Queue ON mkt.PromoterClaimRequest (Status, SubmittedAt);
GO

CREATE TABLE mkt.Venue
(
    VenueId      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Venue PRIMARY KEY,
    PublicId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Venue_PublicId DEFAULT NEWSEQUENTIALID(),
    Slug         NVARCHAR(160)  NOT NULL,
    Name         NVARCHAR(200)  NOT NULL,
    Address1     NVARCHAR(200)  NULL,
    City         NVARCHAR(100)  NOT NULL,
    StateCode    CHAR(2)        NOT NULL,
    PostalCode   VARCHAR(10)    NULL,
    CountryCode  CHAR(2)        NOT NULL CONSTRAINT DF_Venue_Country DEFAULT 'US',
    Latitude     DECIMAL(9,6)   NULL,
    Longitude    DECIMAL(9,6)   NULL,
    TimeZoneId   VARCHAR(64)    NOT NULL CONSTRAINT DF_Venue_Tz DEFAULT 'America/New_York',
    CreatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_Venue_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(3)   NOT NULL CONSTRAINT DF_Venue_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Venue_PublicId UNIQUE (PublicId),
    CONSTRAINT UQ_Venue_Slug UNIQUE (Slug),
    CONSTRAINT CK_Venue_Lat CHECK (Latitude  IS NULL OR Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_Venue_Lng CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);
GO
CREATE INDEX IX_Venue_StateCity ON mkt.Venue (StateCode, City);
GO

/* ---------------------------------------------------------------------
   ShopEvent: national calendar columns. ShopId may be NULL.
   UQ_ShopEvent_ShopSlug is not created (nullable ShopId); slug is global.
   --------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ShopEvent_ShopSlug' AND parent_object_id = OBJECT_ID('mkt.ShopEvent'))
    ALTER TABLE mkt.ShopEvent DROP CONSTRAINT UQ_ShopEvent_ShopSlug;
GO

ALTER TABLE mkt.ShopEvent ALTER COLUMN ShopId BIGINT NULL;
GO

ALTER TABLE mkt.ShopEvent DROP CONSTRAINT CK_ShopEvent_Status;
ALTER TABLE mkt.ShopEvent ADD CONSTRAINT CK_ShopEvent_Status
    CHECK (Status IN ('DRAFT','PENDING_REVIEW','PUBLISHED','CANCELED','POSTPONED'));
GO

ALTER TABLE mkt.ShopEvent DROP CONSTRAINT CK_ShopEvent_Source;
ALTER TABLE mkt.ShopEvent ADD CONSTRAINT CK_ShopEvent_Source
    CHECK (SourceType IN ('MANUAL','MAILBOX','FACEBOOK','INSTAGRAM','IMPORT','SUBMIT'));
GO

ALTER TABLE mkt.ShopEvent ADD
    PublicId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ShopEvent_PublicId DEFAULT NEWSEQUENTIALID(),
    PromoterId         BIGINT         NULL CONSTRAINT FK_ShopEvent_Promoter REFERENCES mkt.Promoter(PromoterId),
    VenueId            BIGINT         NULL CONSTRAINT FK_ShopEvent_Venue REFERENCES mkt.Venue(VenueId),
    RecurrenceSeries   VARCHAR(100)   NULL,
    Scope              VARCHAR(10)    NOT NULL CONSTRAINT DF_ShopEvent_Scope DEFAULT 'LOCAL',
    TableCount         INT            NULL,
    AdmissionText      NVARCHAR(200)  NULL,
    AdmissionMinCents  INT            NULL,
    AdmissionMaxCents  INT            NULL,
    EarlyBirdAt        DATETIME2(3)   NULL,
    ParkingText        NVARCHAR(200)  NULL,
    HoursText          NVARCHAR(200)  NULL,
    FocusTags          VARCHAR(200)   NULL,
    OrganizerUrl       NVARCHAR(500)  NULL,
    InventoryUrl       NVARCHAR(500)  NULL,
    CoverPhotoId       BIGINT         NULL CONSTRAINT FK_ShopEvent_Cover REFERENCES core.MediaAsset(MediaAssetId),
    IsFeatured         BIT            NOT NULL CONSTRAINT DF_ShopEvent_Featured DEFAULT 0,
    LastVerifiedAt     DATETIME2(3)   NULL,
    CanceledReason     NVARCHAR(300)  NULL,
    PostponedUntil     DATETIME2(3)   NULL,
    StateCode          CHAR(2)        NULL,
    City               NVARCHAR(100)  NULL,
    SourceSystem       NVARCHAR(40)   NULL,
    CONSTRAINT CK_ShopEvent_Scope CHECK (Scope IN ('LOCAL','REGIONAL','NATIONAL')),
    CONSTRAINT CK_ShopEvent_Owner CHECK (ShopId IS NOT NULL OR PromoterId IS NOT NULL);
GO

CREATE UNIQUE INDEX UX_ShopEvent_PublicId ON mkt.ShopEvent (PublicId);
CREATE UNIQUE INDEX UX_ShopEvent_Slug ON mkt.ShopEvent (Slug) WHERE Slug IS NOT NULL;
CREATE INDEX IX_ShopEvent_Calendar ON mkt.ShopEvent (StartsAt, StateCode)
    INCLUDE (Scope, Status, IsFeatured, Title, Slug, PromoterId, VenueId);
CREATE INDEX IX_ShopEvent_Series ON mkt.ShopEvent (RecurrenceSeries, StartsAt) WHERE RecurrenceSeries IS NOT NULL;
CREATE INDEX IX_ShopEvent_Featured ON mkt.ShopEvent (IsFeatured, StartsAt) WHERE Status = 'PUBLISHED';
GO

CREATE TABLE mkt.EventExhibitor
(
    ShopEventId  BIGINT        NOT NULL CONSTRAINT FK_EventExhibitor_Event REFERENCES mkt.ShopEvent(ShopEventId),
    ShopId       BIGINT        NOT NULL CONSTRAINT FK_EventExhibitor_Shop REFERENCES dir.Shop(ShopId),
    TableLabel   VARCHAR(20)   NULL,
    Status       VARCHAR(20)   NOT NULL CONSTRAINT DF_EventExhibitor_Status DEFAULT 'CLAIMED',
    Specialties  VARCHAR(200)  NULL,
    CreatedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_EventExhibitor_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_EventExhibitor PRIMARY KEY (ShopEventId, ShopId),
    CONSTRAINT CK_EventExhibitor_Status CHECK (Status IN ('CLAIMED','CONFIRMED_BY_PROMOTER','REMOVED'))
);
GO

CREATE TABLE mkt.EventGuest
(
    EventGuestId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventGuest PRIMARY KEY,
    ShopEventId  BIGINT        NOT NULL CONSTRAINT FK_EventGuest_Event REFERENCES mkt.ShopEvent(ShopEventId),
    Name         NVARCHAR(150) NOT NULL,
    GuestType    VARCHAR(20)   NOT NULL,
    SessionText  NVARCHAR(200) NULL,
    PriceText    NVARCHAR(100) NULL,
    CONSTRAINT CK_EventGuest_Type CHECK (GuestType IN ('ATHLETE','ARTIST','CREATOR','GRADER_ONSITE','OTHER'))
);
GO

CREATE TABLE mkt.EventManufacturer
(
    ShopEventId    BIGINT NOT NULL CONSTRAINT FK_EventMfr_Event REFERENCES mkt.ShopEvent(ShopEventId),
    ManufacturerId BIGINT NOT NULL CONSTRAINT FK_EventMfr_Mfr REFERENCES dir.Manufacturer(ManufacturerId),
    CONSTRAINT PK_EventManufacturer PRIMARY KEY (ShopEventId, ManufacturerId)
);
GO

CREATE TABLE mkt.EventFollow
(
    ShopEventId         BIGINT        NOT NULL CONSTRAINT FK_EventFollow_Event REFERENCES mkt.ShopEvent(ShopEventId),
    UserId              BIGINT        NOT NULL CONSTRAINT FK_EventFollow_User REFERENCES core.AppUser(UserId),
    Going               BIT           NOT NULL CONSTRAINT DF_EventFollow_Going DEFAULT 0,
    RemindMinutesBefore INT           NULL CONSTRAINT DF_EventFollow_Remind DEFAULT 1440,
    CreatedAt           DATETIME2(3)  NOT NULL CONSTRAINT DF_EventFollow_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_EventFollow PRIMARY KEY (ShopEventId, UserId)
);
GO

CREATE TABLE mkt.PromoterFollow
(
    PromoterId BIGINT NOT NULL CONSTRAINT FK_PromoterFollow_Promoter REFERENCES mkt.Promoter(PromoterId),
    UserId     BIGINT NOT NULL CONSTRAINT FK_PromoterFollow_User REFERENCES core.AppUser(UserId),
    CreatedAt  DATETIME2(3) NOT NULL CONSTRAINT DF_PromoterFollow_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PromoterFollow PRIMARY KEY (PromoterId, UserId)
);
GO

CREATE TABLE core.UserCalendarToken
(
    UserId    BIGINT      NOT NULL CONSTRAINT PK_UserCalendarToken PRIMARY KEY
                          CONSTRAINT FK_UserCalToken_User REFERENCES core.AppUser(UserId),
    Token     CHAR(32)    NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_UserCalToken_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_UserCalToken UNIQUE (Token)
);
GO

CREATE TABLE mkt.EventSubmission
(
    EventSubmissionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventSubmission PRIMARY KEY,
    PublicId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EventSubmission_PublicId DEFAULT NEWSEQUENTIALID(),
    SubmittedByUserId BIGINT         NULL CONSTRAINT FK_EventSub_User REFERENCES core.AppUser(UserId),
    SubmitterName     NVARCHAR(120)  NOT NULL,
    SubmitterEmail    NVARCHAR(320)  NOT NULL,
    ShopId            BIGINT         NULL CONSTRAINT FK_EventSub_Shop REFERENCES dir.Shop(ShopId),
    PromoterId        BIGINT         NULL CONSTRAINT FK_EventSub_Promoter REFERENCES mkt.Promoter(PromoterId),
    Title             NVARCHAR(200)  NOT NULL,
    Description       NVARCHAR(4000) NULL,
    StartsAt          DATETIME2(3)   NOT NULL,
    EndsAt            DATETIME2(3)   NULL,
    TimeZoneId        VARCHAR(64)    NOT NULL CONSTRAINT DF_EventSub_Tz DEFAULT 'America/New_York',
    VenueName         NVARCHAR(200)  NULL,
    Address1          NVARCHAR(200)  NULL,
    City              NVARCHAR(100)  NULL,
    StateCode         CHAR(2)        NULL,
    PostalCode        VARCHAR(10)    NULL,
    Scope             VARCHAR(10)    NOT NULL CONSTRAINT DF_EventSub_Scope DEFAULT 'LOCAL',
    TableCount        INT            NULL,
    AdmissionText     NVARCHAR(200)  NULL,
    HoursText         NVARCHAR(200)  NULL,
    FocusTags         VARCHAR(200)   NULL,
    OrganizerUrl      NVARCHAR(500)  NULL,
    PromoterName      NVARCHAR(200)  NULL,
    Status            VARCHAR(20)    NOT NULL CONSTRAINT DF_EventSub_Status DEFAULT 'PENDING_REVIEW',
    ShopEventId       BIGINT         NULL CONSTRAINT FK_EventSub_Event REFERENCES mkt.ShopEvent(ShopEventId),
    ReviewedByUserId  BIGINT         NULL CONSTRAINT FK_EventSub_ReviewedBy REFERENCES core.AppUser(UserId),
    ReviewedAt        DATETIME2(3)   NULL,
    DecisionReason    NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_EventSub_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_EventSub_Updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_EventSubmission_PublicId UNIQUE (PublicId),
    CONSTRAINT CK_EventSub_Scope CHECK (Scope IN ('LOCAL','REGIONAL','NATIONAL')),
    CONSTRAINT CK_EventSub_Status CHECK (Status IN ('PENDING_REVIEW','APPROVED','REJECTED','WITHDRAWN'))
);
GO
CREATE INDEX IX_EventSubmission_Queue ON mkt.EventSubmission (Status, CreatedAt);
GO

CREATE TABLE feed.ShowFeedEntry
(
    ShowFeedEntryId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShowFeedEntry PRIMARY KEY,
    SlotCode         VARCHAR(30)   NOT NULL CONSTRAINT FK_ShowFeed_Slot REFERENCES feed.RotationSlot(SlotCode),
    ShopEventId      BIGINT        NOT NULL CONSTRAINT FK_ShowFeed_Event REFERENCES mkt.ShopEvent(ShopEventId),
    Reason           VARCHAR(20)   NOT NULL,
    BaseWeight       DECIMAL(6,2)  NOT NULL CONSTRAINT DF_ShowFeed_Weight DEFAULT 1.00,
    IsPinned         BIT           NOT NULL CONSTRAINT DF_ShowFeed_Pinned DEFAULT 0,
    EligibleFrom     DATETIME2(3)  NOT NULL,
    EligibleUntil    DATETIME2(3)  NOT NULL,
    IsActive         BIT           NOT NULL CONSTRAINT DF_ShowFeed_Active DEFAULT 1,
    CreatedByUserId  BIGINT        NULL CONSTRAINT FK_ShowFeed_User REFERENCES core.AppUser(UserId),
    CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_ShowFeed_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ShowFeed_Reason CHECK (Reason IN ('NEW_SHOW','THIS_WEEKEND','FEATURED','NATIONAL')),
    CONSTRAINT CK_ShowFeed_Window CHECK (EligibleUntil > EligibleFrom),
    CONSTRAINT CK_ShowFeed_Weight CHECK (BaseWeight > 0)
);
GO
CREATE UNIQUE INDEX UX_ShowFeed_Active ON feed.ShowFeedEntry (SlotCode, ShopEventId) WHERE IsActive = 1;
CREATE INDEX IX_ShowFeed_Window ON feed.ShowFeedEntry (SlotCode, EligibleFrom, EligibleUntil)
    INCLUDE (ShopEventId, BaseWeight, IsPinned) WHERE IsActive = 1;
GO

/* ---------------------------------------------------------------------
   Calendar + sitemap views
   --------------------------------------------------------------------- */
CREATE VIEW mkt.vw_CalendarUpcoming
AS
SELECT  e.ShopEventId,
        e.PublicId,
        e.Slug,
        e.Title,
        e.Description,
        e.EventType,
        e.StartsAt,
        e.EndsAt,
        COALESCE(e.TimeZoneId, v.TimeZoneId, 'America/New_York') AS TimeZoneId,
        e.Scope,
        e.Status,
        e.IsFeatured,
        e.FocusTags,
        e.TableCount,
        e.AdmissionText,
        e.AdmissionMinCents,
        e.AdmissionMaxCents,
        e.HoursText,
        e.ParkingText,
        e.OrganizerUrl,
        e.TicketUrl,
        e.RecurrenceSeries,
        e.LastVerifiedAt,
        e.CanceledReason,
        e.PostponedUntil,
        COALESCE(e.City, v.City) AS City,
        COALESCE(e.StateCode, v.StateCode) AS StateCode,
        v.VenueId,
        v.Name AS VenueName,
        v.Address1 AS VenueAddress,
        v.PostalCode,
        v.Latitude,
        v.Longitude,
        p.PromoterId,
        p.Name AS PromoterName,
        p.Slug AS PromoterSlug,
        p.IsVerified AS PromoterVerified,
        e.ShopId,
        CONCAT(N'/shows/', LOWER(COALESCE(e.StateCode, v.StateCode)), N'/',
               LOWER(REPLACE(REPLACE(COALESCE(e.City, v.City), N' ', N'-'), N'.', N'')), N'/',
               e.Slug) AS LocPath,
        CONCAT(N'/shows/', LOWER(COALESCE(e.StateCode, v.StateCode)), N'/',
               LOWER(REPLACE(REPLACE(COALESCE(e.City, v.City), N' ', N'-'), N'.', N'')), N'/',
               e.Slug, N'.ics') AS IcsPath,
        e.UpdatedAt
FROM    mkt.ShopEvent AS e
LEFT JOIN mkt.Venue AS v ON v.VenueId = e.VenueId
LEFT JOIN mkt.Promoter AS p ON p.PromoterId = e.PromoterId
WHERE   e.Status IN ('PUBLISHED', 'POSTPONED')
  AND   e.EventType = 'CARD_SHOW';
GO

CREATE VIEW seo.vw_SitemapShows
AS
SELECT  c.LocPath,
        c.UpdatedAt AS LastModUtc,
        c.StateCode,
        c.City,
        c.Slug,
        c.Title,
        c.StartsAt,
        c.Scope
FROM    mkt.vw_CalendarUpcoming AS c
WHERE   c.Status IN ('PUBLISHED', 'POSTPONED')
  AND   c.StateCode IS NOT NULL
  AND   c.City IS NOT NULL
  AND   c.Slug IS NOT NULL;
GO

CREATE VIEW seo.vw_SitemapShowStates
AS
SELECT  CONCAT(N'/shows/', LOWER(c.StateCode)) AS LocPath,
        MAX(c.UpdatedAt) AS LastModUtc,
        c.StateCode,
        COUNT_BIG(*) AS ShowCount
FROM    mkt.vw_CalendarUpcoming AS c
WHERE   c.StateCode IS NOT NULL
GROUP BY c.StateCode;
GO

CREATE VIEW seo.vw_SitemapShowCities
AS
SELECT  CONCAT(N'/shows/', LOWER(c.StateCode), N'/',
               LOWER(REPLACE(REPLACE(c.City, N' ', N'-'), N'.', N''))) AS LocPath,
        MAX(c.UpdatedAt) AS LastModUtc,
        c.StateCode,
        c.City,
        COUNT_BIG(*) AS ShowCount
FROM    mkt.vw_CalendarUpcoming AS c
WHERE   c.StateCode IS NOT NULL
  AND   c.City IS NOT NULL
GROUP BY c.StateCode, c.City;
GO

CREATE VIEW seo.vw_SitemapPromoters
AS
SELECT  CONCAT(N'/promoters/', p.Slug) AS LocPath,
        p.UpdatedAt AS LastModUtc,
        p.Slug,
        p.Name
FROM    mkt.Promoter AS p;
GO

CREATE VIEW seo.vw_SitemapManufacturers
AS
SELECT  CONCAT(N'/manufacturers/', m.Slug) AS LocPath,
        m.UpdatedAt AS LastModUtc,
        m.Slug,
        m.Name
FROM    dir.Manufacturer AS m
WHERE   m.IsActive = 1;
GO

CREATE OR ALTER VIEW seo.vw_SitemapEvents
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
SELECT  c.LocPath,
        c.UpdatedAt,
        c.StateCode,
        c.Slug,
        c.Title,
        c.StartsAt
FROM    mkt.vw_CalendarUpcoming AS c;
GO
