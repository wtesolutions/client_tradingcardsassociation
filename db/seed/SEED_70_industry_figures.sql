/* SSMS: enable Query > SQLCMD Mode before running.
   ':' directives (:on error, :setvar, :r) and $(vars) fail if SQLCMD Mode is off.
   Plain T-SQL (no SQLCMD Mode): db/ssms/NTCA_FULL_INSTALL_SSMS.sql
*/
/* Staff-authored from the Oct 22, 2026 Gary Vaynerchuk dossier (WTE / NTCA).
   Facts only. Short quotes are attributed. No invented dates or appearances.
   Calendar: no upcoming card-show row — the dossier found no confirmed
   card-show appearance with date + location for Oct 2026–early 2027.
   Idempotent MERGE. Safe to re-run.
*/
:on error exit
USE [$(NtcaDatabase)];
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

MERGE cms.IndustryFigure AS t
USING (SELECT N'gary-vaynerchuk' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    FullName = N'Gary Vaynerchuk',
    AlsoKnownAs = N'GaryVee',
    RoleTitle = N'Entrepreneur; VeeFriends founder; Topps Chrome VeeFriends licensee',
    Summary = N'Public hobby figure who licenses VeeFriends character IP to Topps, appears at the National Sports Collectors Convention and New York Comic Con, and regularly points collectors to local card shops. Research current as of 2026-10-22.',
    WebsiteUrl = N'https://veefriends.com/',
    SourceNotes = N'Dossier prepared for Eric Garrison / NTCA, research current 2026-10-22. Public sources only.',
    IsPublished = 0,
    UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (Slug, FullName, AlsoKnownAs, RoleTitle, Summary, WebsiteUrl, SourceNotes, IsPublished)
VALUES
    (N'gary-vaynerchuk', N'Gary Vaynerchuk', N'GaryVee',
     N'Entrepreneur; VeeFriends founder; Topps Chrome VeeFriends licensee',
     N'Public hobby figure who licenses VeeFriends character IP to Topps, appears at the National Sports Collectors Convention and New York Comic Con, and regularly points collectors to local card shops. Research current as of 2026-10-22.',
     N'https://veefriends.com/',
     N'Dossier prepared for Eric Garrison / NTCA, research current 2026-10-22. Public sources only.',
     0);
GO

DECLARE @Fig BIGINT = (SELECT IndustryFigureId FROM cms.IndustryFigure WHERE Slug = N'gary-vaynerchuk');

MERGE cms.IndustryFigureSource AS t
USING (VALUES
    (@Fig, CAST(1 AS smallint), N'Sports Collectors Digest: Topps VeeFriends partnership at NYCC (2024-10-17)', N'https://sportscollectorsdigest.com/news/topps-to-launch-new-veefriends-partnership-cards-at-new-york-comic-con-'),
    (@Fig, 2, N'X @garyvee: Topps Chrome partnership (2024-10-19)', N'https://x.com/garyvee/status/1847676912602357947'),
    (@Fig, 3, N'Tradercracks / Beckett: 2025 Topps Chrome VeeFriends checklist (2025-04-20)', N'https://www.tradercracks.com/2025-topps-chrome-veefriends-trading-cards-checklist'),
    (@Fig, 4, N'GaryVee Substack: 2026 Topps Chrome VeeFriends launch notes (2026-06-09)', N'https://garyvee.substack.com/p/9-things-you-need-to-know-about-this'),
    (@Fig, 5, N'SI Collectibles: National panel with Logan Paul (2026-07-31)', N'https://www.si.com/collectibles/logan-paul-gary-vee-talk-past-present-future-non-sport-market-the-national'),
    (@Fig, 6, N'X @garyvee: video for people opening or expanding a card shop (2026-08-01)', N'https://x.com/garyvee/status/2083598010030739510'),
    (@Fig, 7, N'VeeFriends blog: NYCC 2026 booth (2026-10-05)', N'https://veefriends.com/blog/veefriends-goes-big-at-nycc-2026-new-comics-event-exclusives-more-9cc796b7c3b8'),
    (@Fig, 8, N'Business Wire: zerocool / first physical VeeFriends set (2022-03-08)', N'https://www.businesswire.com/news/home/20220308006231/en/Fanatics-Collectibles-Launches-zerocool-the-First-Trading-Card-Brand-For-Culture-via-its-Groundbreaking-New-Technology-Platform'),
    (@Fig, 9, N'Sportico: Candy Digital equity / board (2021)', N'https://www.sportico.com/business/finance/2021/fanatics-nft-platform-1234630799/'),
    (@Fig, 10, N'CoinDesk: Sorare Series A investor (2021-02-25)', N'https://www.coindesk.com/business/2021/02/25/benchmark-leads-50m-round-for-digital-soccer-collectibles-platform-sorare'),
    (@Fig, 11, N'PR Newswire: VaynerFund in Courtyard seed (2022-11-02)', N'https://www.prnewswire.com/news-releases/courtyard-raises-7m-in-seed-funding-to-redefine-physical-collectibles-ownership-through-nfts-301666525.html'),
    (@Fig, 12, N'GaryVee YouTube / DailyVee 572: 2019 National as a dealer (2019-08-27)', N'https://rosetta.to/u/garyvee/this-is-literally-the-best-week-of-my-life-dailyvee-572'),
    (@Fig, 13, N'Right About Now transcript: hobby after the COVID bubble (2025-06-10)', N'https://www.ryanisright.com/the-hobby-is-big-business-introducing-vee-friends-w-gary-vaynerchuk/'),
    (@Fig, 14, N'X @garyvee: cards and alt collectibles “are red hot” (2026-08-22)', N'https://x.com/garyvee/status/2091299784615452962'),
    (@Fig, 15, N'Paley Center: shopping talk with eBay CEO (2026-12-01) — not a card show', N'https://www.paleycenter.org/industry-events/mc-iannone-vaynerchuk')
) AS s (IndustryFigureId, SortOrder, Label, Url)
ON t.IndustryFigureId = s.IndustryFigureId AND t.Url = s.Url
WHEN MATCHED THEN UPDATE SET Label = s.Label, SortOrder = s.SortOrder
WHEN NOT MATCHED THEN INSERT (IndustryFigureId, SortOrder, Label, Url, RetrievedOn)
VALUES (s.IndustryFigureId, s.SortOrder, s.Label, s.Url, '2026-10-22');
GO

DECLARE @Fig2 BIGINT = (SELECT IndustryFigureId FROM cms.IndustryFigure WHERE Slug = N'gary-vaynerchuk');
DECLARE @Now2 DATETIME2(3) = SYSUTCDATETIME();

MERGE cms.Post AS t
USING (SELECT N'gary-vaynerchuk-hobby-figure' AS Slug) AS s
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
    Title = N'Gary Vaynerchuk in the trading-card hobby (staff draft)',
    Excerpt = N'Staff briefing: how Gary Vaynerchuk shows up in shops, at the National, and through Topps Chrome VeeFriends — sourced facts only, awaiting SADMIN review.',
    BodyMarkdown = t.BodyMarkdown,
    Body = t.Body,
    AuthorName = N'NTCA staff',
    AuthorOrg = N'NTCA',
    Status = N'DRAFT',
    PublishedAt = NULL,
    SourcePage = N'SEED_70',
    IndustryFigureId = @Fig2,
    UpdatedAt = @Now2
WHEN NOT MATCHED THEN INSERT
    (Slug, Title, Excerpt, Body, BodyMarkdown, AuthorName, AuthorOrg, Status, PublishedAt, SourcePage, IndustryFigureId)
VALUES
    (N'gary-vaynerchuk-hobby-figure',
     N'Gary Vaynerchuk in the trading-card hobby (staff draft)',
     N'Staff briefing: how Gary Vaynerchuk shows up in shops, at the National, and through Topps Chrome VeeFriends — sourced facts only, awaiting SADMIN review.',
     N'<p>This is an unpublished staff draft for SADMIN review. It restates public, sourced facts about Gary Vaynerchuk’s trading-card work. Research is current as of 22 October 2026. Short quotations are his published words; everything else is NTCA paraphrase.</p>
<h2>Who he is in the hobby</h2>
<p>Vaynerchuk (GaryVee) owns the VeeFriends character IP and licenses it to Topps under a multi-year deal announced in October 2024. <a href="https://sportscollectorsdigest.com/news/topps-to-launch-new-veefriends-partnership-cards-at-new-york-comic-con-">Sports Collectors Digest</a> reported the launch at New York Comic Con. On 19 October 2024 he called the Topps Chrome partnership “one of the most substantial days of my career” and said the cards would sit alongside Disney, Marvel, Star Wars, Garbage Pail Kids and Pokémon on Topps Chrome, in hobby shops nationwide (<a href="https://x.com/garyvee/status/1847676912602357947">X @garyvee</a>).</p>
<p>Topps Chrome VeeFriends launched 7 May 2025 (100-character set). A larger 200-character 2026 set followed on 10 June 2026 and was sold at Target, GameStop, Dick’s and hobby shops (<a href="https://www.tradercracks.com/2025-topps-chrome-veefriends-trading-cards-checklist">Tradercracks / Beckett checklist</a>; <a href="https://garyvee.substack.com/p/9-things-you-need-to-know-about-this">GaryVee Substack, 9 June 2026</a>; <a href="https://x.com/garyvee/status/2061180358083662252">X, 31 May 2026</a>). On 4 October 2026, asked about Panini, he wrote: “We have a deal with Topps so there''s that.” (<a href="https://x.com/garyvee/status/2106897669801246826">X @garyvee</a>).</p>
<p>The first physical VeeFriends card set was released on Fanatics’ zerocool in March 2022 (<a href="https://www.businesswire.com/news/home/20220308006231/en/Fanatics-Collectibles-Launches-zerocool-the-First-Trading-Card-Brand-For-Culture-via-its-Groundbreaking-New-Technology-Platform">Business Wire</a>).</p>
<h2>Local shops</h2>
<p>In 2025 he told buyers to search for a sports-card store near them and call about the Chrome release, and said VeeFriends was “picking a card shop in every market to be exclusive” (<a href="https://x.com/garyvee/status/1899467068917142000">11 March 2025</a>; <a href="https://x.com/garyvee/status/1891642617785438649">17 February 2025</a>). On 15 December 2024: “Can''t wait for 2025 - these in every card shop in America.” (<a href="https://x.com/garyvee/status/1868489629764112767">X @garyvee</a>). At the 2026 National he posted a video “for those wanting to open a card shop, or expanding their card shop” (<a href="https://x.com/garyvee/status/2083598010030739510">1 August 2026</a>).</p>
<h2>Shows he has attended (past)</h2>
<p>Sourced National Sports Collectors Convention appearances: 2019 in Chicago as a dealer for four days, booth 309 per Radicards; 2023 in Rosemont; 2024 in Cleveland (24–28 July); 2025 in Rosemont (30 July–3 August), at the VeeFriends booth daily; 2026 in Rosemont (29 July–2 August), including a 30 July panel with Logan Paul. He also appeared at New York Comic Con in 2024 (booth #1217) and 2026 (booth #2835), at a 7 May 2025 GameStop NYC Chrome launch night, at Dave &amp; Adam’s, 1350 Broadway, NYC on 9 May 2025 and 18 February 2026, and at Fanatics Fest NYC in 2025 (exact 2025 dates not in the source). Sources: DailyVee 572 / Radicards; Sports Collectors Digest; TSNN; VeeFriends blog; SI Collectibles; EventCreate; LinkedIn (Chris Toro, GameStop).</p>
<p>VeeFriends listed Fanatics Fest NYC (16–19 July 2026), San Diego Comic-Con (23–26 July 2026) and Card City Expo (30–31 May 2026) as team stops. Sources did not confirm that he attended those 2026 stops. An April 2025 post asking who was going to “the london card show” does not show attendance.</p>
<h2>What he has put money into</h2>
<p>Confirmed: equity partner and board member at Candy Digital (Fanatics NFTs) in 2021; investor in Sorare’s February 2021 Series A; VaynerFund participated in Courtyard’s $7 million seed in November 2022. No sourced ownership of a card shop, a grading company, a breaking company, or a card-show promotion business.</p>
<h2>What he has said about the market</h2>
<p>In a 10 June 2025 interview he said the COVID-era bubble bursting left a more educated buyer and called the hobby “probably in the best place it''s been in a very, very long time” (<a href="https://www.ryanisright.com/the-hobby-is-big-business-introducing-vee-friends-w-gary-vaynerchuk/">Right About Now transcript</a>). At the 2026 National, SI paraphrased him warning that no market goes up forever. On 22 August 2026 he wrote that “trading cards and NFTs and comics and alt collectibles are red hot” and told sellers to take some profits (<a href="https://x.com/garyvee/status/2091299784615452962">X @garyvee</a>). On 2 May 2026: “collectibles have exploded and will continue to with the ai world creating a need for analog and community.” He has been publicly bullish on non-sport and TCG share, vintage fictional-character cards, and live social shopping (Whatnot, TikTok Shop, eBay Live, Walmart Live).</p>
<h2>Upcoming — and why nothing is on the NTCA calendar</h2>
<p>As of 22 October 2026 the dossier found <strong>no confirmed upcoming card-show appearance</strong>. Items that exist in public calendars but do not meet NTCA’s bar (a real card show or event with date + location + source, and a confirmed role) are left out:</p>
<ul>
<li>1 December 2026 Paley Center conversation with eBay CEO Jamie Iannone, 25 W 52nd St, New York — sourced, but the listing is about commerce, not a card show (<a href="https://www.paleycenter.org/industry-events/mc-iannone-vaynerchuk">Paley Center</a>).</li>
<li>VeeFriends Treasure Chest 2026 — dated November 2026 with no exact day.</li>
<li>21 November 2026 online VeeFriends TCG tournament — online only; no role for him stated.</li>
<li>A 2027 “fun drop” mentioned on 25 August 2026 — no date or place.</li>
</ul>
<p>Staff should re-check <a href="https://veefriends.com/calendar">veefriends.com/calendar</a> before publishing this draft or adding a show row.</p>
<h2>Watch items (sourced, not editorialized)</h2>
<p>In September 2026 he said VeeFriends had made “tough firings and reorganizations” and switched suppliers after product “hiccups.” In October 2026 he replied to collectors about Walmart cancelling VeeFriends comic orders. Hobby coverage of VeeFriends cards has been mixed (<a href="https://cardlines.com/2025-topps-chrome-veefriends/">Cardlines</a>).</p>
<p class="fine">Draft status: not indexed, not in <code>/news</code>, not in the sitemap. Publish only after SADMIN review.</p>',
     N'This is an unpublished staff draft for SADMIN review. Facts only; research current 2026-10-22.

Vaynerchuk owns VeeFriends IP and licenses it to Topps (multi-year deal announced Oct 2024). Topps Chrome VeeFriends launched 7 May 2025; a 200-character 2026 set launched 10 June 2026 at Target, GameStop, Dick''s and hobby shops. First physical set: Fanatics zerocool, March 2022.

He has told buyers to call local shops and said VeeFriends was "picking a card shop in every market to be exclusive." At the 2026 National he posted a video "for those wanting to open a card shop, or expanding their card shop."

National appearances sourced: 2019 (Chicago dealer, booth 309), 2023, 2024, 2025 (booth daily), 2026 (panel with Logan Paul). Also NYCC 2024 and 2026, GameStop NYC 7 May 2025, Dave & Adam''s NYC 9 May 2025 and 18 Feb 2026, Fanatics Fest NYC 2025.

Investments sourced: Candy Digital equity/board (2021), Sorare (2021), VaynerFund / Courtyard (2022). No source that he owns a shop, grader, breaker, or show-promotion firm.

No upcoming card-show row: dossier found none with date + location + confirmed card-show role for Oct 2026–early 2027.

Sources listed on cms.IndustryFigureSource for slug gary-vaynerchuk.',
     N'NTCA staff', N'NTCA', N'DRAFT', NULL, N'SEED_70', @Fig2);
GO

PRINT 'SEED_70_industry_figures: Gary Vaynerchuk figure (unpublished) + DRAFT post; no calendar rows (no upcoming card show with date+location+role).';
GO
