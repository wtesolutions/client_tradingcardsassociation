/* Plain T-SQL for SSMS. SQLCMD Mode is not required.
   Database: wte_NtcaDatabase (hardcoded).
   Force-set each person's bio fields to their own legacy-site/about.html text.
   Matches on Slug OR FullName so a MERGE-on-slug miss cannot leave a leak.
   Idempotent. Safe to run twice. Does not touch gallery.
*/
USE [wte_NtcaDatabase];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @Craig NVARCHAR(MAX) = N'Craig Lloyd founded the National Trading Card Association out of a lifelong passion for collecting and a belief that the trading card community deserves a strong, independent national organization representing collectors, businesses, professionals and the hobby as a whole.

A lifelong collector, Craig brings more than 30 years of experience in communications, marketing, nonprofit leadership, membership organizations, executive management and large-scale event production, including national conventions and community events.

His vision for the NTCA is to bring together the many segments of the trading card industry while promoting education, advocacy, industry standards, recognition and the long-term growth of the hobby.

Craig is joined by a growing network of collectors, industry professionals, business leaders and advisors who are helping shape the NTCA and its initiatives as the organization prepares for its national launch.';
DECLARE @CraigQuote NVARCHAR(1000) = N'We are actively recruiting supporters for guidance and input on building this organization and sponsors to allow us the financial foundation to make this vision a reality.';
DECLARE @Eric NVARCHAR(MAX) = N'Eric Garrison started his corporate career at Impel Marketing / SkyBox Trading Cards in Durham in 1993. Back then CEO Frank O''Connell, VP Jay Ladd and CFO Tommie Smith were a dream team of growth.

In 1995 they left investor Bennett S. LeBow and sold to Fleer Trading Cards in Mt. Laurel, NJ, which formed Fleer/SkyBox under ownership of Marvel Comics. Garrison was the Director of Online Services for Fleer/SkyBox.

After leaving, he worked with Allen Iverson and Reebok his rookie year, and then Tim Duncan his rookie year. For many years after that he hosted yearly contests for Panini Group.';
DECLARE @Jay NVARCHAR(MAX) = N'Jay Dudley started out collecting when he was 6 years old. He is now 54 and loves collecting more than anything. He has been a police officer for the last 27 years and will be retiring next year.

Right now he has a small 800 square foot spot, Hobby Tyme Sportscards & Toys inside The Shops of Mebane. His plans are to open a card store/shop next year in Mebane when he retires, with the large majority of his customers urging him to.

Before opening this small business he was the co-owner of Cardiacs Sports & Memorabilia from 2008 until 2018. Before that he also owned the very first sports card shop in Cary, NC from 2002 until 2003 called The Sports Zone.

Police work has changed a tremendous amount in the past 20 years and has demanded the majority of his time, and unfortunately he had to step away from owning card shops until he retires. He learned a tremendous amount owning his own shops as well as being a vendor at many card shows over the last 35 years, and he feels ready to take the obvious next step to continue running his own shop.';

UPDATE cms.PersonBio
SET
    Slug = N'craig-lloyd',
    FullName = N'Craig Lloyd',
    RoleTitle = N'Founder, National Trading Card Association',
    BioText = @Craig,
    Quote = @CraigQuote,
    PhotoLegacyPath = COALESCE(NULLIF(LTRIM(RTRIM(PhotoLegacyPath)), N''), N'assets/craig-lloyd.jpeg'),
    DisplayOrder = 10,
    IsPublished = 1,
    UpdatedAt = @Now
WHERE Slug = N'craig-lloyd'
   OR FullName = N'Craig Lloyd';

IF @@ROWCOUNT = 0
    INSERT INTO cms.PersonBio
        (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
    VALUES
        (N'craig-lloyd', N'Craig Lloyd', N'Founder, National Trading Card Association',
         @Craig, @CraigQuote, N'assets/craig-lloyd.jpeg', 10, 1);

UPDATE cms.PersonBio
SET
    Slug = N'eric-garrison',
    FullName = N'Eric Garrison',
    RoleTitle = N'Technology Officer, National Trading Card Association',
    BioText = @Eric,
    Quote = NULL,
    PhotoLegacyPath = COALESCE(NULLIF(LTRIM(RTRIM(PhotoLegacyPath)), N''), N'assets/eric-garrison.jpeg'),
    DisplayOrder = 20,
    IsPublished = 1,
    UpdatedAt = @Now
WHERE Slug = N'eric-garrison'
   OR FullName = N'Eric Garrison';

IF @@ROWCOUNT = 0
    INSERT INTO cms.PersonBio
        (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
    VALUES
        (N'eric-garrison', N'Eric Garrison', N'Technology Officer, National Trading Card Association',
         @Eric, NULL, N'assets/eric-garrison.jpeg', 20, 1);

UPDATE cms.PersonBio
SET
    Slug = N'jay-dudley',
    FullName = N'Jay Dudley',
    RoleTitle = N'First Official Member, National Trading Card Association',
    BioText = @Jay,
    Quote = NULL,
    PhotoLegacyPath = COALESCE(NULLIF(LTRIM(RTRIM(PhotoLegacyPath)), N''), N'assets/jay-dudley.jpeg'),
    DisplayOrder = 30,
    IsPublished = 1,
    UpdatedAt = @Now
WHERE Slug = N'jay-dudley'
   OR FullName = N'Jay Dudley';

IF @@ROWCOUNT = 0
    INSERT INTO cms.PersonBio
        (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
    VALUES
        (N'jay-dudley', N'Jay Dudley', N'First Official Member, National Trading Card Association',
         @Jay, NULL, N'assets/jay-dudley.jpeg', 30, 1);

/* Optional extra text columns if a later schema added them. */
IF COL_LENGTH(N'cms.PersonBio', N'Body') IS NOT NULL
    EXEC(N'UPDATE cms.PersonBio SET Body = BioText WHERE Slug IN (N''craig-lloyd'', N''eric-garrison'', N''jay-dudley'')');
IF COL_LENGTH(N'cms.PersonBio', N'Summary') IS NOT NULL
    EXEC(N'UPDATE cms.PersonBio SET Summary = NULL WHERE Slug IN (N''craig-lloyd'', N''eric-garrison'', N''jay-dudley'')');
IF COL_LENGTH(N'cms.PersonBio', N'BioHtml') IS NOT NULL
    EXEC(N'UPDATE cms.PersonBio SET BioHtml = NULL WHERE Slug IN (N''craig-lloyd'', N''eric-garrison'', N''jay-dudley'')');
IF COL_LENGTH(N'cms.PersonBio', N'Html') IS NOT NULL
    EXEC(N'UPDATE cms.PersonBio SET Html = NULL WHERE Slug IN (N''craig-lloyd'', N''eric-garrison'', N''jay-dudley'')');

PRINT 'SEED_19_bio_fix: Craig / Eric / Jay bios force-set to their own legacy founder-body text';
GO
