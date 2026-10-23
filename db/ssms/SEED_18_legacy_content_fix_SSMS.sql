/* Plain T-SQL for SSMS — SQLCMD Mode is NOT required.
   Database: wte_NtcaDatabase (hardcoded).
   Corrects concatenated SEED_40 bios from legacy-site/about.html
   and adds gallery rows for the About boys photo and Network hall photo.
   Idempotent. Real content only.
*/

USE [wte_NtcaDatabase];
GO
DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

MERGE cms.PersonBio AS t
USING (SELECT N'craig-lloyd' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    FullName = N'Craig Lloyd',
    RoleTitle = N'Founder, National Trading Card Association',
    BioText = N'Craig Lloyd founded the National Trading Card Association out of a lifelong passion for collecting and a belief that the trading card community deserves a strong, independent national organization representing collectors, businesses, professionals and the hobby as a whole.

A lifelong collector, Craig brings more than 30 years of experience in communications, marketing, nonprofit leadership, membership organizations, executive management and large-scale event production, including national conventions and community events.

His vision for the NTCA is to bring together the many segments of the trading card industry while promoting education, advocacy, industry standards, recognition and the long-term growth of the hobby.

Craig is joined by a growing network of collectors, industry professionals, business leaders and advisors who are helping shape the NTCA and its initiatives as the organization prepares for its national launch.',
    Quote = N'We are actively recruiting supporters for guidance and input on building this organization and sponsors to allow us the financial foundation to make this vision a reality.',
    PhotoLegacyPath = N'assets/craig-lloyd.jpeg',
    DisplayOrder = 10,
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
VALUES
    (N'craig-lloyd', N'Craig Lloyd', N'Founder, National Trading Card Association',
     N'Craig Lloyd founded the National Trading Card Association out of a lifelong passion for collecting and a belief that the trading card community deserves a strong, independent national organization representing collectors, businesses, professionals and the hobby as a whole.

A lifelong collector, Craig brings more than 30 years of experience in communications, marketing, nonprofit leadership, membership organizations, executive management and large-scale event production, including national conventions and community events.

His vision for the NTCA is to bring together the many segments of the trading card industry while promoting education, advocacy, industry standards, recognition and the long-term growth of the hobby.

Craig is joined by a growing network of collectors, industry professionals, business leaders and advisors who are helping shape the NTCA and its initiatives as the organization prepares for its national launch.',
     N'We are actively recruiting supporters for guidance and input on building this organization and sponsors to allow us the financial foundation to make this vision a reality.',
     N'assets/craig-lloyd.jpeg', 10, 1);

MERGE cms.PersonBio AS t
USING (SELECT N'eric-garrison' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    FullName = N'Eric Garrison',
    RoleTitle = N'Technology Officer, National Trading Card Association',
    BioText = N'Eric Garrison started his corporate career at Impel Marketing / SkyBox Trading Cards in Durham in 1993. Back then CEO Frank O''Connell, VP Jay Ladd and CFO Tommie Smith were a dream team of growth.

In 1995 they left investor Bennett S. LeBow and sold to Fleer Trading Cards in Mt. Laurel, NJ, which formed Fleer/SkyBox under ownership of Marvel Comics. Garrison was the Director of Online Services for Fleer/SkyBox.

After leaving, he worked with Allen Iverson and Reebok his rookie year, and then Tim Duncan his rookie year. For many years after that he hosted yearly contests for Panini Group.',
    Quote = NULL,
    PhotoLegacyPath = N'assets/eric-garrison.jpeg',
    DisplayOrder = 20,
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, FullName, RoleTitle, BioText, Quote, PhotoLegacyPath, DisplayOrder, IsPublished)
VALUES
    (N'eric-garrison', N'Eric Garrison', N'Technology Officer, National Trading Card Association',
     N'Eric Garrison started his corporate career at Impel Marketing / SkyBox Trading Cards in Durham in 1993. Back then CEO Frank O''Connell, VP Jay Ladd and CFO Tommie Smith were a dream team of growth.

In 1995 they left investor Bennett S. LeBow and sold to Fleer Trading Cards in Mt. Laurel, NJ, which formed Fleer/SkyBox under ownership of Marvel Comics. Garrison was the Director of Online Services for Fleer/SkyBox.

After leaving, he worked with Allen Iverson and Reebok his rookie year, and then Tim Duncan his rookie year. For many years after that he hosted yearly contests for Panini Group.',
     NULL, N'assets/eric-garrison.jpeg', 20, 1);

MERGE core.MediaAsset AS t
USING (SELECT N'legacy/assets/about-boys-cards.jpg' AS ObjectKey) AS s
ON t.ObjectKey = s.ObjectKey
WHEN MATCHED THEN UPDATE SET
    LegacyPath = N'assets/about-boys-cards.jpg',
    Visibility = 'PUBLIC',
    Status = 'READY',
    ContentType = 'image/jpeg',
    AltText = N'Three boys in baseball caps look over rubber-banded stacks of trading cards at a table',
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ObjectKey, LegacyPath, Visibility, ContentType, Status, AltText)
VALUES
    (N'legacy/assets/about-boys-cards.jpg', N'assets/about-boys-cards.jpg', 'PUBLIC', 'image/jpeg', 'READY',
     N'Three boys in baseball caps look over rubber-banded stacks of trading cards at a table');

MERGE cms.GalleryItem AS t
USING (SELECT N'about-boys-cards' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Caption = N'Sharing stacks at the table',
    AltText = N'Three boys in baseball caps look over rubber-banded stacks of trading cards at a table',
    LegacyPath = N'assets/about-boys-cards.jpg',
    MediaAssetId = (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = N'legacy/assets/about-boys-cards.jpg'),
    DisplayOrder = 210,
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Caption, AltText, LegacyPath, MediaAssetId, DisplayOrder, IsPublished)
VALUES
    (N'about-boys-cards', N'Sharing stacks at the table',
     N'Three boys in baseball caps look over rubber-banded stacks of trading cards at a table',
     N'assets/about-boys-cards.jpg',
     (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = N'legacy/assets/about-boys-cards.jpg'), 210, 1);

MERGE core.MediaAsset AS t
USING (SELECT N'legacy/assets/network-convention-hall.jpeg' AS ObjectKey) AS s
ON t.ObjectKey = s.ObjectKey
WHEN MATCHED THEN UPDATE SET
    LegacyPath = N'assets/network-convention-hall.jpeg',
    Visibility = 'PUBLIC',
    Status = 'READY',
    ContentType = 'image/jpeg',
    AltText = N'Speaker at a lectern addressing a packed convention hall, with the NTCA logo projected on the main screen',
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ObjectKey, LegacyPath, Visibility, ContentType, Status, AltText)
VALUES
    (N'legacy/assets/network-convention-hall.jpeg', N'assets/network-convention-hall.jpeg', 'PUBLIC', 'image/jpeg', 'READY',
     N'Speaker at a lectern addressing a packed convention hall, with the NTCA logo projected on the main screen');

MERGE cms.GalleryItem AS t
USING (SELECT N'network-convention-hall' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Caption = N'Convention hall · NTCA presentation',
    AltText = N'Speaker at a lectern addressing a packed convention hall, with the NTCA logo projected on the main screen',
    LegacyPath = N'assets/network-convention-hall.jpeg',
    MediaAssetId = (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = N'legacy/assets/network-convention-hall.jpeg'),
    DisplayOrder = 220,
    IsPublished = 1,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, Caption, AltText, LegacyPath, MediaAssetId, DisplayOrder, IsPublished)
VALUES
    (N'network-convention-hall', N'Convention hall · NTCA presentation',
     N'Speaker at a lectern addressing a packed convention hall, with the NTCA logo projected on the main screen',
     N'assets/network-convention-hall.jpeg',
     (SELECT MediaAssetId FROM core.MediaAsset WHERE ObjectKey = N'legacy/assets/network-convention-hall.jpeg'), 220, 1);

PRINT 'SEED_18_legacy_content_fix: bios corrected; boys + hall gallery items merged';
