/* Plain T-SQL for SSMS — SQLCMD Mode is NOT required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice. No ':' directives, no $(vars).
   Eric: open this file in SSMS connected to 10.100.1.87, select wte_NtcaDatabase, Execute.
   Google Places cache is batch-only. Do not call Google from visitor requests.
*/

USE [wte_NtcaDatabase];
GO

IF OBJECT_ID(N'dir.GooglePlaceMatch', N'U') IS NULL
BEGIN
    CREATE TABLE dir.GooglePlaceMatch
    (
        ShopId              BIGINT          NOT NULL
            CONSTRAINT PK_GooglePlaceMatch PRIMARY KEY
            CONSTRAINT FK_GooglePlaceMatch_Shop REFERENCES dir.Shop (ShopId),
        GooglePlaceId       NVARCHAR(255)   NULL,
        MatchStatus         VARCHAR(20)     NOT NULL
            CONSTRAINT DF_GooglePlaceMatch_Status DEFAULT 'UNMATCHED',
        Confidence          DECIMAL(5, 4)   NULL,
        CandidatePlaceId    NVARCHAR(255)   NULL,
        CandidateName       NVARCHAR(300)   NULL,
        CandidateAddress    NVARCHAR(400)   NULL,
        CandidateLat        DECIMAL(9, 6)   NULL,
        CandidateLng        DECIMAL(9, 6)   NULL,
        MatchedAt           DATETIME2(3)    NULL,
        ReviewedByUserId    BIGINT          NULL,
        ReviewedAt          DATETIME2(3)    NULL,
        ReviewNote          NVARCHAR(400)   NULL,
        CreatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceMatch_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceMatch_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_GooglePlaceMatch_Status CHECK
            (MatchStatus IN ('UNMATCHED','PENDING_REVIEW','MATCHED','REJECTED','MANUAL'))
    );
END
GO

IF OBJECT_ID(N'dir.GooglePlaceCache', N'U') IS NULL
BEGIN
    CREATE TABLE dir.GooglePlaceCache
    (
        ShopId              BIGINT          NOT NULL
            CONSTRAINT PK_GooglePlaceCache PRIMARY KEY
            CONSTRAINT FK_GooglePlaceCache_Shop REFERENCES dir.Shop (ShopId),
        GooglePlaceId       NVARCHAR(255)   NOT NULL,
        Rating              DECIMAL(2, 1)   NULL,
        UserRatingCount     INT             NULL,
        GoogleMapsUri       NVARCHAR(500)   NULL,
        HoursJson           NVARCHAR(MAX)   NULL,
        OpenNow             BIT             NULL,
        DisplayName         NVARCHAR(300)   NULL,
        FormattedAddress    NVARCHAR(400)   NULL,
        Latitude            DECIMAL(9, 6)   NULL,
        Longitude           DECIMAL(9, 6)   NULL,
        RefreshedUtc        DATETIME2(3)    NULL,
        ExpiresUtc          DATETIME2(3)    NULL,
        CreatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceCache_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlaceCache_Updated DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID(N'dir.GooglePlacePhoto', N'U') IS NULL
BEGIN
    CREATE TABLE dir.GooglePlacePhoto
    (
        PhotoId             BIGINT          IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_GooglePlacePhoto PRIMARY KEY,
        ShopId              BIGINT          NOT NULL
            CONSTRAINT FK_GooglePlacePhoto_Shop REFERENCES dir.Shop (ShopId),
        GooglePlaceId       NVARCHAR(255)   NOT NULL,
        Slot                TINYINT         NOT NULL,
        PhotoName           NVARCHAR(400)   NOT NULL,
        AuthorName          NVARCHAR(200)   NULL,
        AuthorUri           NVARCHAR(400)   NULL,
        RelativePath        NVARCHAR(400)   NOT NULL,
        ContentType         VARCHAR(80)     NOT NULL
            CONSTRAINT DF_GooglePlacePhoto_Type DEFAULT 'image/jpeg',
        FetchedUtc          DATETIME2(3)    NOT NULL,
        ExpiresUtc          DATETIME2(3)    NOT NULL,
        CreatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GooglePlacePhoto_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_GooglePlacePhoto_ShopSlot UNIQUE (ShopId, Slot)
    );
    CREATE INDEX IX_GooglePlacePhoto_Expires ON dir.GooglePlacePhoto (ExpiresUtc);
END
GO

IF OBJECT_ID(N'analytics.GoogleApiCallLog', N'U') IS NULL
BEGIN
    CREATE TABLE analytics.GoogleApiCallLog
    (
        CallDate            DATE            NOT NULL,
        Sku                 VARCHAR(40)     NOT NULL,
        CallCount           INT             NOT NULL
            CONSTRAINT DF_GoogleApiCallLog_Count DEFAULT 0,
        UpdatedAt           DATETIME2(3)    NOT NULL
            CONSTRAINT DF_GoogleApiCallLog_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_GoogleApiCallLog PRIMARY KEY (CallDate, Sku),
        CONSTRAINT CK_GoogleApiCallLog_Sku CHECK
            (Sku IN ('TEXT_SEARCH','PLACE_DETAILS','PLACE_PHOTO'))
    );
END
GO
