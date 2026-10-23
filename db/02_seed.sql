/* Reference / lookup seed. Idempotent (MERGE). Safe to re-run.
   Lookup data lives here (not in db/seed/SEED_00_*) so generate_seed can
   regenerate content files without touching tiers, karma, or site settings.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- roles ---- */
MERGE core.Role AS t
USING (VALUES
    ('SUPERADMIN', N'Super admin', N'WTE / NTCA staff for /SADMIN', 1),
    ('ADMIN',      N'Admin',       N'Site administrator', 2),
    ('MODERATOR',  N'Moderator',   N'Content moderation', 3),
    ('EDITOR',     N'Editor',      N'Editorial / CMS', 4)
) AS s (RoleCode, Name, Description, SortOrder)
ON t.RoleCode = s.RoleCode
WHEN MATCHED THEN UPDATE SET Name = s.Name, Description = s.Description, SortOrder = s.SortOrder
WHEN NOT MATCHED THEN INSERT (RoleCode, Name, Description, SortOrder)
VALUES (s.RoleCode, s.Name, s.Description, s.SortOrder);
GO

/* ---- shop tiers (PAID / PREMIUM prices NULL = TBD; FREE = 0) ---- */
MERGE dir.ShopTier AS t
USING (VALUES
    (1, 'FREE',    N'Free Listing',     0,    0,    CAST(1 AS tinyint), CAST(1 AS bit)),
    (2, 'PAID',    N'Featured Listing', NULL, NULL, CAST(2 AS tinyint), CAST(1 AS bit)),
    (3, 'PREMIUM', N'Premium Partner',  NULL, NULL, CAST(3 AS tinyint), CAST(1 AS bit))
) AS s (TierId, TierCode, Name, MonthlyPriceCents, AnnualPriceCents, SortOrder, IsActive)
ON t.TierId = s.TierId
WHEN MATCHED THEN UPDATE SET
    TierCode = s.TierCode, Name = s.Name,
    MonthlyPriceCents = s.MonthlyPriceCents, AnnualPriceCents = s.AnnualPriceCents,
    SortOrder = s.SortOrder, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT (TierId, TierCode, Name, MonthlyPriceCents, AnnualPriceCents, SortOrder, IsActive)
VALUES (s.TierId, s.TierCode, s.Name, s.MonthlyPriceCents, s.AnnualPriceCents, s.SortOrder, s.IsActive);
GO

MERGE dir.Feature AS t
USING (VALUES
    ('BASIC_LISTING',     N'Directory listing', N'Name, address, phone, hours, photos, reviews'),
    ('SHOW_WEBSITE_LINK', N'Website link', N'Clickable website link on the listing'),
    ('SHOW_EMAIL_LINK',   N'Email link', N'Public email / contact link on the listing'),
    ('SHOW_SOCIAL_LINKS', N'Social links', N'Facebook / Instagram links on the listing'),
    ('OWNER_RESPONSES',   N'Reply to reviews', N'Owner can post a public reply to reviews'),
    ('EVENT_CALENDAR',    N'Event calendar', N'Shop events on listing and site calendar'),
    ('EMAIL_PROMOTION',   N'Email promotion', N'Shop promos in NTCA email; LimitValue = campaigns per month'),
    ('SOCIAL_PROMOTION',  N'Social promotion', N'Shop promos on NTCA social; LimitValue = posts per month'),
    ('MAILBOX_PARSING',   N'Mailbox parsing', N'Inbound mailbox parsed into events / promos'),
    ('SOCIAL_MONITORING', N'Facebook/Instagram monitoring', N'Shop FB/IG posts monitored and turned into events / promos'),
    ('ROTATION_BOOST',    N'Rotation boost', N'Higher weight / longer window in the new-shops rotation')
) AS s (FeatureCode, Name, Description)
ON t.FeatureCode = s.FeatureCode
WHEN MATCHED THEN UPDATE SET Name = s.Name, Description = s.Description
WHEN NOT MATCHED THEN INSERT (FeatureCode, Name, Description)
VALUES (s.FeatureCode, s.Name, s.Description);
GO

MERGE dir.TierFeature AS t
USING (VALUES
    (1, 'BASIC_LISTING', 1, NULL),
    (2, 'BASIC_LISTING', 1, NULL),
    (2, 'SHOW_WEBSITE_LINK', 1, NULL),
    (2, 'SHOW_EMAIL_LINK', 1, NULL),
    (2, 'SHOW_SOCIAL_LINKS', 1, NULL),
    (2, 'OWNER_RESPONSES', 1, NULL),
    (3, 'BASIC_LISTING', 1, NULL),
    (3, 'SHOW_WEBSITE_LINK', 1, NULL),
    (3, 'SHOW_EMAIL_LINK', 1, NULL),
    (3, 'SHOW_SOCIAL_LINKS', 1, NULL),
    (3, 'OWNER_RESPONSES', 1, NULL),
    (3, 'EVENT_CALENDAR', 1, NULL),
    (3, 'EMAIL_PROMOTION', 1, 4),
    (3, 'SOCIAL_PROMOTION', 1, 4),
    (3, 'MAILBOX_PARSING', 1, NULL),
    (3, 'SOCIAL_MONITORING', 1, NULL),
    (3, 'ROTATION_BOOST', 1, NULL)
) AS s (TierId, FeatureCode, IsEnabled, LimitValue)
ON t.TierId = s.TierId AND t.FeatureCode = s.FeatureCode
WHEN MATCHED THEN UPDATE SET IsEnabled = s.IsEnabled, LimitValue = s.LimitValue
WHEN NOT MATCHED THEN INSERT (TierId, FeatureCode, IsEnabled, LimitValue)
VALUES (s.TierId, s.FeatureCode, s.IsEnabled, s.LimitValue);
GO

MERGE feed.RotationSlot AS t
USING (VALUES
    ('HOME_NEW_SHOPS',    N'Homepage: New shops',        CAST(6 AS tinyint), CAST(30 AS smallint), CAST(60 AS smallint), CAST(1 AS bit)),
    ('DIRECTORY_SIDEBAR', N'Directory sidebar',          CAST(4 AS tinyint), CAST(30 AS smallint), CAST(30 AS smallint), CAST(1 AS bit)),
    ('NEWSLETTER',        N'Newsletter: New this month', CAST(8 AS tinyint), CAST(31 AS smallint), CAST(1440 AS smallint), CAST(1 AS bit))
) AS s (SlotCode, Name, MaxItems, DefaultWindowDays, RefreshEveryMinutes, IsActive)
ON t.SlotCode = s.SlotCode
WHEN MATCHED THEN UPDATE SET
    Name = s.Name, MaxItems = s.MaxItems, DefaultWindowDays = s.DefaultWindowDays,
    RefreshEveryMinutes = s.RefreshEveryMinutes, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT (SlotCode, Name, MaxItems, DefaultWindowDays, RefreshEveryMinutes, IsActive)
VALUES (s.SlotCode, s.Name, s.MaxItems, s.DefaultWindowDays, s.RefreshEveryMinutes, s.IsActive);
GO

MERGE feed.TierRotationWeight AS t
USING (VALUES
    (1, CAST(1.00 AS decimal(5,2)), CAST(30 AS smallint)),
    (2, CAST(1.50 AS decimal(5,2)), CAST(45 AS smallint)),
    (3, CAST(2.50 AS decimal(5,2)), CAST(60 AS smallint))
) AS s (TierId, WeightMultiplier, WindowDays)
ON t.TierId = s.TierId
WHEN MATCHED THEN UPDATE SET WeightMultiplier = s.WeightMultiplier, WindowDays = s.WindowDays
WHEN NOT MATCHED THEN INSERT (TierId, WeightMultiplier, WindowDays)
VALUES (s.TierId, s.WeightMultiplier, s.WindowDays);
GO

MERGE karma.Category AS t
USING (VALUES
    ('PHOTO',     N'Photo karma',     CAST(1 AS tinyint)),
    ('REVIEW',    N'Review karma',    CAST(2 AS tinyint)),
    ('FEEDBACK',  N'Feedback karma',  CAST(3 AS tinyint)),
    ('SHOP_HELP', N'Shop-help karma', CAST(4 AS tinyint)),
    ('COMMUNITY', N'Community karma', CAST(5 AS tinyint))
) AS s (CategoryCode, Name, DisplayOrder)
ON t.CategoryCode = s.CategoryCode
WHEN MATCHED THEN UPDATE SET Name = s.Name, DisplayOrder = s.DisplayOrder
WHEN NOT MATCHED THEN INSERT (CategoryCode, Name, DisplayOrder)
VALUES (s.CategoryCode, s.Name, s.DisplayOrder);
GO

MERGE karma.ActionType AS t
USING (VALUES
    ('PHOTO_APPROVED',               'PHOTO',     N'Shop photo approved', 5, 50, 30, 10, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    ('PHOTO_FIRST_FOR_SHOP',         'PHOTO',     N'First approved photo of a shop', 10, NULL, NULL, 1, 1, 1, 1),
    ('PHOTO_UNCLAIMED_SHOP_BONUS',   'PHOTO',     N'Photo of an unclaimed free-tier shop', 3, 15, NULL, 1, 1, 1, 1),
    ('PHOTO_UPVOTE_RECEIVED',        'PHOTO',     N'Upvote received on a photo', 1, 50, NULL, NULL, 0, 1, 1),
    ('PHOTO_DOWNVOTE_RECEIVED',      'PHOTO',     N'Downvote received on a photo', -1, NULL, NULL, NULL, 0, 1, 1),
    ('REVIEW_APPROVED',              'REVIEW',    N'Review approved', 10, 50, 300, 1, 1, 1, 1),
    ('REVIEW_DETAILED_BONUS',        'REVIEW',    N'Detailed review bonus (length/photos)', 5, 25, NULL, 1, 1, 1, 1),
    ('REVIEW_HELPFUL_RECEIVED',      'REVIEW',    N'Helpful vote received on a review', 1, 50, NULL, NULL, 0, 1, 1),
    ('REVIEW_UNHELPFUL_RECEIVED',    'REVIEW',    N'Unhelpful vote received on a review', -1, NULL, NULL, NULL, 0, 1, 1),
    ('FEEDBACK_ACCEPTED',            'FEEDBACK',  N'Site / general feedback accepted', 3, 15, NULL, NULL, 1, 0, 1),
    ('LISTING_CORRECTION_ACCEPTED',  'FEEDBACK',  N'Listing correction accepted', 5, 25, NULL, 3, 1, 1, 1),
    ('EVENT_TIP_ACCEPTED',           'FEEDBACK',  N'Event tip accepted', 3, 15, NULL, NULL, 1, 1, 1),
    ('NEW_SHOP_SUGGESTION_ACCEPTED', 'SHOP_HELP', N'Suggested a new shop that was added', 15, 60, NULL, 1, 1, 1, 1),
    ('CLAIM_REFERRAL_APPROVED',      'SHOP_HELP', N'Referred an owner whose claim was approved', 25, NULL, NULL, 1, 1, 1, 1),
    ('BADGE_BONUS',                  'COMMUNITY', N'Bonus for earning a badge', 0, NULL, NULL, NULL, 0, 0, 1),
    ('ADMIN_ADJUSTMENT',             'COMMUNITY', N'Manual adjustment by staff', 0, NULL, NULL, NULL, 0, 0, 1)
) AS s (ActionCode, CategoryCode, Name, Points, DailyPointCap, CooldownSeconds, PerShopLifetimeCap, RequiresModeration, BlockOnOwnShop, IsActive)
ON t.ActionCode = s.ActionCode
WHEN MATCHED THEN UPDATE SET
    CategoryCode = s.CategoryCode, Name = s.Name, Points = s.Points,
    DailyPointCap = s.DailyPointCap, CooldownSeconds = s.CooldownSeconds,
    PerShopLifetimeCap = s.PerShopLifetimeCap, RequiresModeration = s.RequiresModeration,
    BlockOnOwnShop = s.BlockOnOwnShop, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT
    (ActionCode, CategoryCode, Name, Points, DailyPointCap, CooldownSeconds, PerShopLifetimeCap, RequiresModeration, BlockOnOwnShop, IsActive)
VALUES
    (s.ActionCode, s.CategoryCode, s.Name, s.Points, s.DailyPointCap, s.CooldownSeconds, s.PerShopLifetimeCap, s.RequiresModeration, s.BlockOnOwnShop, s.IsActive);
GO

MERGE karma.BadgeDefinition AS t
USING (VALUES
    ('FIRST_PHOTO',     N'First Snapshot', N'First shop photo approved', 'PHOTO', 1, 'ACTION_COUNT', 'PHOTO_APPROVED', NULL, 1, 10),
    ('PHOTOGRAPHER_10', N'Shutterbug', N'10 shop photos approved', 'PHOTO', 2, 'ACTION_COUNT', 'PHOTO_APPROVED', NULL, 10, 11),
    ('PHOTOGRAPHER_50', N'Shop Photographer', N'50 shop photos approved', 'PHOTO', 3, 'ACTION_COUNT', 'PHOTO_APPROVED', NULL, 50, 12),
    ('TRAILBLAZER',     N'Trailblazer', N'First person to photograph a shop', 'TRAILBLAZER', 1, 'ACTION_COUNT', 'PHOTO_FIRST_FOR_SHOP', NULL, 1, 13),
    ('FIRST_REVIEW',    N'First Review', N'First review approved', 'REVIEW', 1, 'ACTION_COUNT', 'REVIEW_APPROVED', NULL, 1, 20),
    ('REVIEWER_10',     N'Regular', N'10 reviews approved', 'REVIEW', 2, 'ACTION_COUNT', 'REVIEW_APPROVED', NULL, 10, 21),
    ('REVIEWER_50',     N'Critic', N'50 reviews approved', 'REVIEW', 3, 'ACTION_COUNT', 'REVIEW_APPROVED', NULL, 50, 22),
    ('HELPFUL_100',     N'Trusted Voice', N'100 helpful votes on your reviews', 'HELPFUL', 1, 'ACTION_COUNT', 'REVIEW_HELPFUL_RECEIVED', NULL, 100, 23),
    ('ROAD_TRIP_5',     N'Road Trip', N'Reviewed shops in 5 different states', 'ROAD_TRIP', 1, 'DISTINCT_STATES', 'REVIEW_APPROVED', NULL, 5, 30),
    ('SHOP_HOPPER_25',  N'Shop Hopper', N'Reviewed 25 different shops', 'SHOP_HOPPER', 1, 'DISTINCT_SHOPS', 'REVIEW_APPROVED', NULL, 25, 31),
    ('SHOP_SCOUT',      N'Shop Scout', N'Suggested a shop that was added to the directory', 'SCOUT', 1, 'ACTION_COUNT', 'NEW_SHOP_SUGGESTION_ACCEPTED', NULL, 1, 40),
    ('MATCHMAKER',      N'Matchmaker', N'Brought a shop owner in to claim their listing', 'MATCHMAKER', 1, 'ACTION_COUNT', 'CLAIM_REFERRAL_APPROVED', NULL, 1, 41),
    ('FACT_CHECKER_10', N'Fact Checker', N'10 listing corrections accepted', 'FACT_CHECKER', 1, 'ACTION_COUNT', 'LISTING_CORRECTION_ACCEPTED', NULL, 10, 42),
    ('KARMA_100',       N'Rookie Card', N'Reached 100 karma', 'KARMA', 1, 'TOTAL_KARMA', NULL, NULL, 100, 50),
    ('KARMA_1000',      N'All-Star', N'Reached 1,000 karma', 'KARMA', 2, 'TOTAL_KARMA', NULL, NULL, 1000, 51),
    ('KARMA_10000',     N'Hall of Famer', N'Reached 10,000 karma', 'KARMA', 3, 'TOTAL_KARMA', NULL, NULL, 10000, 52),
    ('ONE_YEAR',        N'One-Year Club', N'Member for one year', 'TENURE', 1, 'ACCOUNT_AGE_DAYS', NULL, NULL, 365, 60),
    ('FOUNDING_MEMBER', N'Founding Member', N'Joined during the NTCA founding period (staff-awarded)', 'FOUNDING', 1, 'MANUAL', NULL, NULL, NULL, 1)
) AS s (BadgeCode, Name, Description, BadgeFamily, BadgeLevel, CriteriaType, ActionCode, CategoryCode, Threshold, DisplayOrder)
ON t.BadgeCode = s.BadgeCode
WHEN MATCHED THEN UPDATE SET
    Name = s.Name, Description = s.Description, BadgeFamily = s.BadgeFamily, BadgeLevel = s.BadgeLevel,
    CriteriaType = s.CriteriaType, ActionCode = s.ActionCode, CategoryCode = s.CategoryCode,
    Threshold = s.Threshold, DisplayOrder = s.DisplayOrder, IsActive = 1
WHEN NOT MATCHED THEN INSERT
    (BadgeCode, Name, Description, BadgeFamily, BadgeLevel, CriteriaType, ActionCode, CategoryCode, Threshold, DisplayOrder)
VALUES
    (s.BadgeCode, s.Name, s.Description, s.BadgeFamily, s.BadgeLevel, s.CriteriaType, s.ActionCode, s.CategoryCode, s.Threshold, s.DisplayOrder);
GO

MERGE karma.Perk AS t
USING (VALUES
    ('AUTO_APPROVE_REVIEWS', N'Reviews skip the queue', N'Reviews publish immediately (still reportable)', NULL, 'REVIEW', 150, 60, 0, CAST(1 AS bit)),
    ('AUTO_APPROVE_PHOTOS',  N'Photos skip the queue', N'Photos publish immediately (still reportable)', NULL, 'PHOTO', 150, 60, 0, 1),
    ('DIRECT_LISTING_EDITS', N'Direct listing edits', N'Edit unclaimed listings without review', 1000, NULL, NULL, 180, 0, 1),
    ('PRIORITY_FLAGS',       N'Priority reports', N'Reports jump the moderation queue', 250, NULL, NULL, 30, 1, 1)
) AS s (PerkCode, Name, Description, MinTotalKarma, MinCategoryCode, MinCategoryKarma, MinAccountAgeDays, MaxRemovalsLast90d, IsActive)
ON t.PerkCode = s.PerkCode
WHEN MATCHED THEN UPDATE SET
    Name = s.Name, Description = s.Description, MinTotalKarma = s.MinTotalKarma,
    MinCategoryCode = s.MinCategoryCode, MinCategoryKarma = s.MinCategoryKarma,
    MinAccountAgeDays = s.MinAccountAgeDays, MaxRemovalsLast90d = s.MaxRemovalsLast90d, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT
    (PerkCode, Name, Description, MinTotalKarma, MinCategoryCode, MinCategoryKarma, MinAccountAgeDays, MaxRemovalsLast90d, IsActive)
VALUES
    (s.PerkCode, s.Name, s.Description, s.MinTotalKarma, s.MinCategoryCode, s.MinCategoryKarma, s.MinAccountAgeDays, s.MaxRemovalsLast90d, s.IsActive);
GO

/* Site mode = Preview until SADMIN flips it to Live. */
MERGE core.SiteSetting AS t
USING (VALUES
    ('Site:Mode',          N'Preview', 'STRING', N'Preview | Live. Preview gates pages and Disallow: / in robots.txt'),
    ('Site:PublicBaseUrl', N'https://tradingcardsassociation.com', 'STRING', N'Canonical public origin for sitemap loc and emails'),
    ('Site:LlmsSummary',   N'The National Trading Card Association (NTCA) supports, educates and connects trading card collectors, hobby shops and industry professionals.', 'STRING', N'Short summary for /llms.txt'),
    ('Media:PublicBaseUrl', N'https://media.tradingcardsassociation.com', 'STRING', N'CloudFront origin for public media keys'),
    ('Preview:AllowPaths', N'["/","/history","/signup","/signup/confirm","/unsubscribe","/SADMIN","/owner"]', 'JSON', N'Paths reachable without a preview cookie')
) AS s (SettingKey, SettingValue, ValueType, Description)
ON t.SettingKey = s.SettingKey
WHEN MATCHED THEN UPDATE SET
    SettingValue = s.SettingValue, ValueType = s.ValueType, Description = s.Description, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue, ValueType, Description)
VALUES (s.SettingKey, s.SettingValue, s.ValueType, s.Description);
GO

PRINT '02_seed: roles, tiers, features, entitlements, rotation, karma, badges, perks, site settings merged';
GO
