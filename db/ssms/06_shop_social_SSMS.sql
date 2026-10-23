/* Plain T-SQL for SSMS — SQLCMD Mode is NOT required.
   Database: wte_NtcaDatabase (hardcoded).
   Idempotent: safe to run twice. No ':' directives, no $(vars).
   Eric: open this file in SSMS connected to 10.100.1.87, select wte_NtcaDatabase, Execute.
   Seeds the social platform catalog and migrates WebsiteUrl / FacebookUrl / InstagramHandle.
   Real data only — does not invent handles.
*/

USE [wte_NtcaDatabase];
GO

IF OBJECT_ID(N'dir.SocialPlatform', N'U') IS NULL
BEGIN
    CREATE TABLE dir.SocialPlatform
    (
        Code            VARCHAR(20)     NOT NULL
            CONSTRAINT PK_SocialPlatform PRIMARY KEY,
        Label           NVARCHAR(40)    NOT NULL,
        IconClass       NVARCHAR(80)    NULL,
        UrlPattern      NVARCHAR(200)   NULL,
        HostAllowlist   NVARCHAR(400)   NULL,
        HandlePrefix    NVARCHAR(20)    NULL,
        DisplayOrder    INT             NOT NULL
            CONSTRAINT DF_SocialPlatform_Order DEFAULT 100,
        IsActive        BIT             NOT NULL
            CONSTRAINT DF_SocialPlatform_Active DEFAULT 1,
        Kind            VARCHAR(20)     NOT NULL
            CONSTRAINT DF_SocialPlatform_Kind DEFAULT 'URL',
        CreatedAt       DATETIME2(3)    NOT NULL
            CONSTRAINT DF_SocialPlatform_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(3)    NOT NULL
            CONSTRAINT DF_SocialPlatform_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_SocialPlatform_Kind CHECK (Kind IN ('URL','EMAIL','PHONE'))
    );
END
GO

IF OBJECT_ID(N'dir.ShopSocial', N'U') IS NULL
BEGIN
    CREATE TABLE dir.ShopSocial
    (
        ShopSocialId    BIGINT          IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ShopSocial PRIMARY KEY,
        ShopId          BIGINT          NOT NULL
            CONSTRAINT FK_ShopSocial_Shop REFERENCES dir.Shop (ShopId),
        PlatformCode    VARCHAR(20)     NOT NULL
            CONSTRAINT FK_ShopSocial_Platform REFERENCES dir.SocialPlatform (Code),
        Url             NVARCHAR(500)   NOT NULL,
        Handle          NVARCHAR(120)   NULL,
        SortOrder       INT             NOT NULL
            CONSTRAINT DF_ShopSocial_Order DEFAULT 100,
        Verified        BIT             NOT NULL
            CONSTRAINT DF_ShopSocial_Verified DEFAULT 0,
        Source          VARCHAR(20)     NOT NULL
            CONSTRAINT DF_ShopSocial_Source DEFAULT 'seed',
        CreatedAt       DATETIME2(3)    NOT NULL
            CONSTRAINT DF_ShopSocial_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(3)    NOT NULL
            CONSTRAINT DF_ShopSocial_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ShopSocial_ShopPlatform UNIQUE (ShopId, PlatformCode),
        CONSTRAINT CK_ShopSocial_Source CHECK (Source IN ('seed','owner','sadmin','manual'))
    );
    CREATE INDEX IX_ShopSocial_Shop ON dir.ShopSocial (ShopId, SortOrder);
END
GO

/* Catalog: Website, Facebook, Instagram, TikTok, YouTube, Whatnot (required),
   then X, Threads, Bluesky, Discord, eBay store, Twitch, Reddit, LinkedIn,
   Pinterest, Snapchat, Linktree, Email, Phone/SMS.
   Whatnot has no Font Awesome brand icon — IconClass is empty (inline SVG in the app). */
MERGE dir.SocialPlatform AS t
USING (VALUES
    ('WEBSITE',   N'Website',      N'fa-solid fa-globe',          N'https://{handle}',                          NULL,                                          NULL,  10, 'URL'),
    ('FACEBOOK',  N'Facebook',     N'fa-brands fa-facebook',      N'https://www.facebook.com/{handle}',         N'facebook.com,fb.com',                        NULL,  20, 'URL'),
    ('INSTAGRAM', N'Instagram',    N'fa-brands fa-instagram',     N'https://www.instagram.com/{handle}/',       N'instagram.com',                              N'@',  30, 'URL'),
    ('TIKTOK',    N'TikTok',       N'fa-brands fa-tiktok',        N'https://www.tiktok.com/@{handle}',          N'tiktok.com',                                 N'@',  40, 'URL'),
    ('YOUTUBE',   N'YouTube',      N'fa-brands fa-youtube',       N'https://www.youtube.com/@{handle}',         N'youtube.com,youtu.be',                       N'@',  50, 'URL'),
    ('WHATNOT',   N'Whatnot',      NULL,                          N'https://www.whatnot.com/user/{handle}',    N'whatnot.com',                                NULL,  60, 'URL'),
    ('X',         N'X',            N'fa-brands fa-x-twitter',     N'https://x.com/{handle}',                    N'x.com,twitter.com',                          N'@',  70, 'URL'),
    ('THREADS',   N'Threads',      N'fa-brands fa-threads',       N'https://www.threads.net/@{handle}',         N'threads.net',                                N'@',  80, 'URL'),
    ('BLUESKY',   N'Bluesky',      N'fa-brands fa-bluesky',       N'https://bsky.app/profile/{handle}',         N'bsky.app',                                   N'@',  90, 'URL'),
    ('DISCORD',   N'Discord',      N'fa-brands fa-discord',       N'https://discord.gg/{handle}',               N'discord.gg,discord.com,discordapp.com',      NULL, 100, 'URL'),
    ('EBAY',      N'eBay store',   N'fa-brands fa-ebay',          N'https://www.ebay.com/str/{handle}',         N'ebay.com',                                   NULL, 110, 'URL'),
    ('TWITCH',    N'Twitch',       N'fa-brands fa-twitch',        N'https://www.twitch.tv/{handle}',            N'twitch.tv',                                  NULL, 120, 'URL'),
    ('REDDIT',    N'Reddit',       N'fa-brands fa-reddit',        N'https://www.reddit.com/user/{handle}',      N'reddit.com',                                 N'u/', 130, 'URL'),
    ('LINKEDIN',  N'LinkedIn',     N'fa-brands fa-linkedin',      N'https://www.linkedin.com/company/{handle}', N'linkedin.com',                               NULL, 140, 'URL'),
    ('PINTEREST', N'Pinterest',    N'fa-brands fa-pinterest',     N'https://www.pinterest.com/{handle}/',       N'pinterest.com',                              NULL, 150, 'URL'),
    ('SNAPCHAT',  N'Snapchat',     N'fa-brands fa-snapchat',      N'https://www.snapchat.com/add/{handle}',     N'snapchat.com',                               NULL, 160, 'URL'),
    ('LINKTREE',  N'Linktree',     N'fa-solid fa-link',           N'https://linktr.ee/{handle}',                N'linktr.ee',                                  NULL, 170, 'URL'),
    ('EMAIL',     N'Email',        N'fa-solid fa-envelope',       N'mailto:{handle}',                           NULL,                                          NULL, 180, 'EMAIL'),
    ('PHONE',     N'Phone/SMS',    N'fa-solid fa-comment-sms',    N'tel:{handle}',                              NULL,                                          NULL, 190, 'PHONE')
) AS s (Code, Label, IconClass, UrlPattern, HostAllowlist, HandlePrefix, DisplayOrder, Kind)
ON t.Code = s.Code
WHEN MATCHED THEN
    UPDATE SET
        t.Label = s.Label,
        t.IconClass = s.IconClass,
        t.UrlPattern = s.UrlPattern,
        t.HostAllowlist = s.HostAllowlist,
        t.HandlePrefix = s.HandlePrefix,
        t.DisplayOrder = s.DisplayOrder,
        t.Kind = s.Kind,
        t.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Code, Label, IconClass, UrlPattern, HostAllowlist, HandlePrefix, DisplayOrder, IsActive, Kind)
    VALUES (s.Code, s.Label, s.IconClass, s.UrlPattern, s.HostAllowlist, s.HandlePrefix, s.DisplayOrder, 1, s.Kind);
GO

/* Migrate existing shop columns. Skip rows that already have a social link for that platform. */
INSERT INTO dir.ShopSocial (ShopId, PlatformCode, Url, Handle, SortOrder, Verified, Source)
SELECT
    s.ShopId,
    'WEBSITE',
    CASE
        WHEN LOWER(LTRIM(RTRIM(s.WebsiteUrl))) LIKE N'http://%'
          OR LOWER(LTRIM(RTRIM(s.WebsiteUrl))) LIKE N'https://%'
            THEN LTRIM(RTRIM(s.WebsiteUrl))
        ELSE N'https://' + LTRIM(RTRIM(s.WebsiteUrl))
    END,
    NULL,
    10,
    0,
    'seed'
FROM dir.Shop s
WHERE s.WebsiteUrl IS NOT NULL
  AND LTRIM(RTRIM(s.WebsiteUrl)) <> N''
  AND NOT EXISTS (
        SELECT 1 FROM dir.ShopSocial x
        WHERE x.ShopId = s.ShopId AND x.PlatformCode = 'WEBSITE');
GO

INSERT INTO dir.ShopSocial (ShopId, PlatformCode, Url, Handle, SortOrder, Verified, Source)
SELECT
    s.ShopId,
    'FACEBOOK',
    CASE
        WHEN LOWER(LTRIM(RTRIM(s.FacebookUrl))) LIKE N'http://%'
          OR LOWER(LTRIM(RTRIM(s.FacebookUrl))) LIKE N'https://%'
            THEN LTRIM(RTRIM(s.FacebookUrl))
        WHEN LOWER(LTRIM(RTRIM(s.FacebookUrl))) LIKE N'facebook.com/%'
          OR LOWER(LTRIM(RTRIM(s.FacebookUrl))) LIKE N'www.facebook.com/%'
          OR LOWER(LTRIM(RTRIM(s.FacebookUrl))) LIKE N'fb.com/%'
            THEN N'https://' + LTRIM(RTRIM(s.FacebookUrl))
        ELSE N'https://www.facebook.com/' + LTRIM(RTRIM(REPLACE(s.FacebookUrl, N'@', N'')))
    END,
    CASE
        WHEN LTRIM(RTRIM(s.FacebookUrl)) LIKE N'http%'
          OR LTRIM(RTRIM(s.FacebookUrl)) LIKE N'%facebook.com/%'
          OR LTRIM(RTRIM(s.FacebookUrl)) LIKE N'%fb.com/%'
            THEN NULL
        ELSE LTRIM(RTRIM(REPLACE(s.FacebookUrl, N'@', N'')))
    END,
    20,
    0,
    'seed'
FROM dir.Shop s
WHERE s.FacebookUrl IS NOT NULL
  AND LTRIM(RTRIM(s.FacebookUrl)) <> N''
  AND NOT EXISTS (
        SELECT 1 FROM dir.ShopSocial x
        WHERE x.ShopId = s.ShopId AND x.PlatformCode = 'FACEBOOK');
GO

INSERT INTO dir.ShopSocial (ShopId, PlatformCode, Url, Handle, SortOrder, Verified, Source)
SELECT
    s.ShopId,
    'INSTAGRAM',
    CASE
        WHEN LOWER(LTRIM(RTRIM(s.InstagramHandle))) LIKE N'http://%'
          OR LOWER(LTRIM(RTRIM(s.InstagramHandle))) LIKE N'https://%'
            THEN LTRIM(RTRIM(s.InstagramHandle))
        WHEN LOWER(LTRIM(RTRIM(s.InstagramHandle))) LIKE N'instagram.com/%'
          OR LOWER(LTRIM(RTRIM(s.InstagramHandle))) LIKE N'www.instagram.com/%'
            THEN N'https://' + LTRIM(RTRIM(s.InstagramHandle))
        ELSE N'https://www.instagram.com/' + LTRIM(RTRIM(REPLACE(s.InstagramHandle, N'@', N''))) + N'/'
    END,
    CASE
        WHEN LTRIM(RTRIM(s.InstagramHandle)) LIKE N'http%'
          OR LTRIM(RTRIM(s.InstagramHandle)) LIKE N'%instagram.com/%'
            THEN NULL
        ELSE LTRIM(RTRIM(REPLACE(s.InstagramHandle, N'@', N'')))
    END,
    30,
    0,
    'seed'
FROM dir.Shop s
WHERE s.InstagramHandle IS NOT NULL
  AND LTRIM(RTRIM(s.InstagramHandle)) <> N''
  AND NOT EXISTS (
        SELECT 1 FROM dir.ShopSocial x
        WHERE x.ShopId = s.ShopId AND x.PlatformCode = 'INSTAGRAM');
GO
