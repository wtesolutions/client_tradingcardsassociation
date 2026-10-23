using Microsoft.EntityFrameworkCore;
using Ntca.Core;

namespace Ntca.Data;

public sealed class NtcaDbContext : DbContext
{
    public NtcaDbContext(DbContextOptions<NtcaDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<ShopTier> ShopTiers => Set<ShopTier>();
    public DbSet<ShopHours> ShopHours => Set<ShopHours>();
    public DbSet<ShopReview> ShopReviews => Set<ShopReview>();
    public DbSet<ShopMember> ShopMembers => Set<ShopMember>();
    public DbSet<ShopEditRequest> ShopEditRequests => Set<ShopEditRequest>();
    public DbSet<ShopDailyStat> ShopDailyStatsLegacy => Set<ShopDailyStat>();
    public DbSet<ShopImpression> ShopImpressions => Set<ShopImpression>();
    public DbSet<ShopDailyStats> ShopDailyStats => Set<ShopDailyStats>();
    public DbSet<SessionSalt> SessionSalts => Set<SessionSalt>();
    public DbSet<EventExhibitor> EventExhibitors => Set<EventExhibitor>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<ManufacturerCategory> ManufacturerCategories => Set<ManufacturerCategory>();
    public DbSet<ManufacturerCategoryMap> ManufacturerCategoryMaps => Set<ManufacturerCategoryMap>();
    public DbSet<ManufacturerTopic> ManufacturerTopics => Set<ManufacturerTopic>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Promoter> Promoters => Set<Promoter>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<ShopEvent> ShopEvents => Set<ShopEvent>();
    public DbSet<EventSubmission> EventSubmissions => Set<EventSubmission>();
    public DbSet<Signup> Signups => Set<Signup>();
    public DbSet<SignupFunnel> SignupFunnels => Set<SignupFunnel>();
    public DbSet<SignupStepEvent> SignupStepEvents => Set<SignupStepEvent>();
    public DbSet<OnboardingSubmission> OnboardingSubmissions => Set<OnboardingSubmission>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<IndustryFigure> IndustryFigures => Set<IndustryFigure>();
    public DbSet<IndustryFigureSource> IndustryFigureSources => Set<IndustryFigureSource>();
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();
    public DbSet<ConditionGrade> ConditionGrades => Set<ConditionGrade>();
    public DbSet<StaticPage> StaticPages => Set<StaticPage>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<AgentUaRule> AgentUaRules => Set<AgentUaRule>();
    public DbSet<AgentVisit> AgentVisits => Set<AgentVisit>();
    public DbSet<AgentInquiry> AgentInquiries => Set<AgentInquiry>();
    public DbSet<GooglePlaceMatch> GooglePlaceMatches => Set<GooglePlaceMatch>();
    public DbSet<GooglePlaceCache> GooglePlaceCaches => Set<GooglePlaceCache>();
    public DbSet<GooglePlacePhoto> GooglePlacePhotos => Set<GooglePlacePhoto>();
    public DbSet<GoogleApiCallLog> GoogleApiCallLogs => Set<GoogleApiCallLog>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AppUser>(e =>
        {
            e.ToTable("AppUser", "core");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Username).HasMaxLength(40);
            e.Property(x => x.DisplayName).HasMaxLength(80);
            e.Property(x => x.AccountStatus).HasMaxLength(20);
            e.Property(x => x.KarmaStatus).HasMaxLength(20);
            e.Property(x => x.MembershipLevel).HasMaxLength(20);
            e.HasMany(x => x.Roles).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        });

        model.Entity<UserRole>(e =>
        {
            e.ToTable("UserRole", "core");
            e.HasKey(x => new { x.UserId, x.RoleCode });
            e.Property(x => x.RoleCode).HasMaxLength(20);
        });

        model.Entity<VerificationCode>(e =>
        {
            e.ToTable("VerificationCode", "core");
            e.HasKey(x => x.VerificationCodeId);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Purpose).HasMaxLength(30);
            e.Property(x => x.CodeHash).HasMaxLength(32).IsRequired();
        });

        model.Entity<SiteSetting>(e =>
        {
            e.ToTable("SiteSetting", "core");
            e.HasKey(x => x.SettingKey);
            e.Property(x => x.SettingKey).HasMaxLength(80);
            e.Property(x => x.SettingValue).HasColumnType("nvarchar(max)");
            e.Property(x => x.ValueType).HasMaxLength(20);
        });

        model.Entity<Shop>(e =>
        {
            e.ToTable("Shop", "dir");
            e.HasKey(x => x.ShopId);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.ClaimStatus).HasMaxLength(20);
            e.Property(x => x.ListingStatus).HasMaxLength(20);
            e.HasOne(x => x.Tier).WithMany().HasForeignKey(x => x.CurrentTierId);
            e.HasMany(x => x.Hours).WithOne(x => x.Shop).HasForeignKey(x => x.ShopId);
        });

        model.Entity<ShopTier>(e =>
        {
            e.ToTable("ShopTier", "dir");
            e.HasKey(x => x.TierId);
            e.Property(x => x.TierCode).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(50);
        });

        model.Entity<ShopHours>(e =>
        {
            e.ToTable("ShopHours", "dir");
            e.HasKey(x => x.ShopHoursId);
            e.Property(x => x.OpensAt).HasColumnType("time(0)");
            e.Property(x => x.ClosesAt).HasColumnType("time(0)");
        });

        model.Entity<ShopReview>(e =>
        {
            e.ToTable("ShopReview", "ugc");
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.ModerationStatus).HasMaxLength(20);
        });

        model.Entity<ShopDailyStat>(e =>
        {
            e.ToTable("ShopDailyStat", "dir");
            e.HasKey(x => new { x.ShopId, x.StatDate });
            e.Property(x => x.StatDate).HasColumnType("date");
        });

        model.Entity<ShopImpression>(e =>
        {
            e.ToTable("ShopImpression", "analytics");
            e.HasKey(x => x.ImpressionId);
            e.Property(x => x.Surface).HasMaxLength(40);
            e.Property(x => x.PageUrl).HasMaxLength(400);
            e.Property(x => x.SessionHash).HasMaxLength(32).IsFixedLength();
        });

        model.Entity<ShopDailyStats>(e =>
        {
            e.ToTable("ShopDailyStats", "analytics");
            e.HasKey(x => new { x.ShopId, x.StatDate, x.Surface });
            e.Property(x => x.StatDate).HasColumnType("date");
            e.Property(x => x.Surface).HasMaxLength(40);
        });

        model.Entity<SessionSalt>(e =>
        {
            e.ToTable("SessionSalt", "analytics");
            e.HasKey(x => x.SaltDate);
            e.Property(x => x.SaltDate).HasColumnType("date");
            e.Property(x => x.Salt).HasMaxLength(32).IsRequired();
        });

        model.Entity<EventExhibitor>(e =>
        {
            e.ToTable("EventExhibitor", "mkt");
            e.HasKey(x => new { x.ShopEventId, x.ShopId });
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.ShopEvent).WithMany().HasForeignKey(x => x.ShopEventId);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
        });

        model.Entity<ShopMember>(e =>
        {
            e.ToTable("ShopMember", "dir");
            e.HasKey(x => x.ShopMemberId);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
            e.HasOne(x => x.User).WithMany(x => x.ShopMemberships).HasForeignKey(x => x.UserId);
        });

        model.Entity<ShopEditRequest>(e =>
        {
            e.ToTable("ShopEditRequest", "dir");
            e.HasKey(x => x.ShopEditRequestId);
            e.Property(x => x.ProposedJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
        });

        model.Entity<Manufacturer>(e =>
        {
            e.ToTable("Manufacturer", "dir");
            e.HasKey(x => x.ManufacturerId);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.RetrievedOn).HasColumnType("date");
        });

        model.Entity<ManufacturerCategory>(e =>
        {
            e.ToTable("ManufacturerCategory", "dir");
            e.HasKey(x => x.CategoryCode);
        });

        model.Entity<ManufacturerCategoryMap>(e =>
        {
            e.ToTable("ManufacturerCategoryMap", "dir");
            e.HasKey(x => new { x.ManufacturerId, x.CategoryCode });
            e.HasOne(x => x.Manufacturer).WithMany(x => x.Categories).HasForeignKey(x => x.ManufacturerId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryCode);
        });

        model.Entity<ManufacturerTopic>(e =>
        {
            e.ToTable("ManufacturerTopic", "dir");
            e.HasKey(x => x.ManufacturerTopicId);
            e.Property(x => x.TopicGroup).HasMaxLength(20);
            e.Property(x => x.TopicName).HasMaxLength(80);
            e.HasOne(x => x.Manufacturer).WithMany(x => x.Topics).HasForeignKey(x => x.ManufacturerId);
        });

        model.Entity<Organization>(e =>
        {
            e.ToTable("Organization", "dir");
            e.HasKey(x => x.OrganizationId);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.OrgType).HasMaxLength(20);
        });

        model.Entity<Promoter>(e =>
        {
            e.ToTable("Promoter", "mkt");
            e.HasKey(x => x.PromoterId);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
        });

        model.Entity<Venue>(e =>
        {
            e.ToTable("Venue", "mkt");
            e.HasKey(x => x.VenueId);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
        });

        model.Entity<ShopEvent>(e =>
        {
            e.ToTable("ShopEvent", "mkt");
            e.HasKey(x => x.ShopEventId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.EventType).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Scope).HasMaxLength(10);
            e.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength();
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
            e.HasOne(x => x.Promoter).WithMany().HasForeignKey(x => x.PromoterId);
            e.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId);
        });

        model.Entity<EventSubmission>(e =>
        {
            e.ToTable("EventSubmission", "mkt");
            e.HasKey(x => x.EventSubmissionId);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Scope).HasMaxLength(10);
        });

        model.Entity<Signup>(e =>
        {
            e.ToTable("Signup", "crm");
            e.HasKey(x => x.SignupId);
            e.Property(x => x.FormKey).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.EmailNormalized).HasMaxLength(320);
            e.Property(x => x.SocialHandle).HasMaxLength(120);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.ExtraJson);
            e.Property(x => x.UtmSource).HasMaxLength(120);
            e.Property(x => x.UtmMedium).HasMaxLength(120);
            e.Property(x => x.UtmCampaign).HasMaxLength(120);
        });

        model.Entity<SignupFunnel>(e =>
        {
            e.ToTable("SignupFunnel", "crm");
            e.HasKey(x => x.SignupFunnelId);
            e.Property(x => x.HumanRole).HasMaxLength(40);
            e.Property(x => x.RolesJson).HasMaxLength(400);
            e.Property(x => x.CurrentStep).HasMaxLength(60);
            e.Property(x => x.Stage).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.OnboardingStatus).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.FirstName).HasMaxLength(80);
        });

        model.Entity<SignupStepEvent>(e =>
        {
            e.ToTable("SignupStepEvent", "crm");
            e.HasKey(x => x.SignupStepEventId);
            e.Property(x => x.StepKey).HasMaxLength(60);
            e.Property(x => x.Action).HasMaxLength(20);
            e.Property(x => x.Stage).HasMaxLength(20);
            e.Property(x => x.RoleKey).HasMaxLength(40);
        });

        model.Entity<OnboardingSubmission>(e =>
        {
            e.ToTable("OnboardingSubmission", "crm");
            e.HasKey(x => x.OnboardingSubmissionId);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Mode).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.SubmitterName).HasMaxLength(120);
            e.Property(x => x.SubmitterEmail).HasMaxLength(320);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ExtraJson).HasColumnType("nvarchar(max)");
        });

        model.Entity<Post>(e =>
        {
            e.ToTable("Post", "cms");
            e.HasKey(x => x.PostId);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Body).HasColumnType("nvarchar(max)");
            e.Property(x => x.BodyMarkdown).HasColumnType("nvarchar(max)");
        });

        model.Entity<IndustryFigure>(e =>
        {
            e.ToTable("IndustryFigure", "cms");
            e.HasKey(x => x.IndustryFigureId);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.FullName).HasMaxLength(160);
        });

        model.Entity<IndustryFigureSource>(e =>
        {
            e.ToTable("IndustryFigureSource", "cms");
            e.HasKey(x => x.IndustryFigureSourceId);
            e.Property(x => x.RetrievedOn).HasColumnType("date");
            e.HasOne(x => x.Figure).WithMany(x => x.Sources).HasForeignKey(x => x.IndustryFigureId);
        });

        model.Entity<GlossaryTerm>(e =>
        {
            e.ToTable("GlossaryTerm", "cms");
            e.HasKey(x => x.GlossaryTermId);
        });

        model.Entity<ConditionGrade>(e =>
        {
            e.ToTable("ConditionGrade", "cms");
            e.HasKey(x => x.ConditionGradeId);
        });

        model.Entity<StaticPage>(e =>
        {
            e.ToTable("StaticPage", "cms");
            e.HasKey(x => x.PageId);
        });

        model.Entity<GalleryItem>(e =>
        {
            e.ToTable("GalleryItem", "cms");
            e.HasKey(x => x.GalleryItemId);
        });

        model.Entity<AgentUaRule>(e =>
        {
            e.ToTable("AgentUaRule", "analytics");
            e.HasKey(x => x.AgentUaRuleId);
            e.Property(x => x.Token).HasMaxLength(80);
            e.Property(x => x.AssistantName).HasMaxLength(80);
            e.Property(x => x.Role).HasMaxLength(20);
        });

        model.Entity<AgentVisit>(e =>
        {
            e.ToTable("AgentVisit", "analytics");
            e.HasKey(x => x.AgentVisitId);
            e.Property(x => x.AssistantName).HasMaxLength(80);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.PagePath).HasMaxLength(200);
        });

        model.Entity<AgentInquiry>(e =>
        {
            e.ToTable("AgentInquiry", "analytics");
            e.HasKey(x => x.AgentInquiryId);
            e.Property(x => x.AssistantName).HasMaxLength(80);
            e.Property(x => x.HumanRole).HasMaxLength(40);
            e.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Zip3).HasMaxLength(3).IsFixedLength();
            e.Property(x => x.Interests).HasMaxLength(200);
            e.Property(x => x.QuestionExcerpt).HasMaxLength(240);
        });

        model.Entity<GooglePlaceMatch>(e =>
        {
            e.ToTable("GooglePlaceMatch", "dir");
            e.HasKey(x => x.ShopId);
            e.Property(x => x.GooglePlaceId).HasMaxLength(255);
            e.Property(x => x.MatchStatus).HasMaxLength(20);
            e.Property(x => x.Confidence).HasPrecision(5, 4);
            e.Property(x => x.CandidatePlaceId).HasMaxLength(255);
            e.Property(x => x.CandidateName).HasMaxLength(300);
            e.Property(x => x.CandidateAddress).HasMaxLength(400);
            e.Property(x => x.CandidateLat).HasPrecision(9, 6);
            e.Property(x => x.CandidateLng).HasPrecision(9, 6);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
        });

        model.Entity<GooglePlaceCache>(e =>
        {
            e.ToTable("GooglePlaceCache", "dir");
            e.HasKey(x => x.ShopId);
            e.Property(x => x.GooglePlaceId).HasMaxLength(255);
            e.Property(x => x.Rating).HasPrecision(2, 1);
            e.Property(x => x.GoogleMapsUri).HasMaxLength(500);
            e.Property(x => x.DisplayName).HasMaxLength(300);
            e.Property(x => x.FormattedAddress).HasMaxLength(400);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
        });

        model.Entity<GooglePlacePhoto>(e =>
        {
            e.ToTable("GooglePlacePhoto", "dir");
            e.HasKey(x => x.PhotoId);
            e.Property(x => x.GooglePlaceId).HasMaxLength(255);
            e.Property(x => x.PhotoName).HasMaxLength(400);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.Property(x => x.AuthorUri).HasMaxLength(400);
            e.Property(x => x.RelativePath).HasMaxLength(400);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId);
        });

        model.Entity<GoogleApiCallLog>(e =>
        {
            e.ToTable("GoogleApiCallLog", "analytics");
            e.HasKey(x => new { x.CallDate, x.Sku });
            e.Property(x => x.CallDate).HasColumnType("date");
            e.Property(x => x.Sku).HasMaxLength(40);
        });
    }
}
