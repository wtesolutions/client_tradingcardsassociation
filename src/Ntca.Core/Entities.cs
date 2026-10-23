namespace Ntca.Core;

public sealed class AppUser
{
    public long UserId { get; set; }
    public Guid PublicId { get; set; }
    public string Email { get; set; } = "";
    public DateTime? EmailVerifiedAt { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AccountStatus { get; set; } = "ACTIVE";
    public string KarmaStatus { get; set; } = "NORMAL";
    public string MembershipLevel { get; set; } = "SUPPORTER";
    public bool IsStaff { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<ShopMember> ShopMemberships { get; set; } = new List<ShopMember>();
}

public sealed class UserRole
{
    public long UserId { get; set; }
    public string RoleCode { get; set; } = "";
    public DateTime GrantedAt { get; set; }
    public AppUser? User { get; set; }
}

public sealed class VerificationCode
{
    public long VerificationCodeId { get; set; }
    public long? UserId { get; set; }
    public string Email { get; set; } = "";
    public string Purpose { get; set; } = "";
    public byte[] CodeHash { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
    public byte AttemptCount { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedFromIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SiteSetting
{
    public string SettingKey { get; set; } = "";
    public string SettingValue { get; set; } = "";
    public string ValueType { get; set; } = "STRING";
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Shop
{
    public long ShopId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "US";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Phone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? PublicEmail { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramHandle { get; set; }
    public string? Description { get; set; }
    public string? PrimarySpecialty { get; set; }
    public byte CurrentTierId { get; set; }
    public string ClaimStatus { get; set; } = "UNCLAIMED";
    public string ListingStatus { get; set; } = "PENDING";
    public string? Source { get; set; }
    public string? SourceRef { get; set; }
    public string? SourceSystem { get; set; }
    public string? ImportNotes { get; set; }
    public bool IsDestinationShop =>
        string.Equals(SourceSystem, "TOP100-2026-10-22", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrEmpty(ImportNotes)
            && ImportNotes.Contains("Destination shop", StringComparison.OrdinalIgnoreCase));
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ShopTier? Tier { get; set; }
    public ICollection<ShopHours> Hours { get; set; } = new List<ShopHours>();
}

public sealed class ShopTier
{
    public byte TierId { get; set; }
    public string TierCode { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class ShopHours
{
    public long ShopHoursId { get; set; }
    public long ShopId { get; set; }
    public byte DayOfWeek { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public bool IsClosed { get; set; }
    public Shop? Shop { get; set; }
}

public sealed class ShopReview
{
    public long ReviewId { get; set; }
    public long ShopId { get; set; }
    public byte Rating { get; set; }
    public string ModerationStatus { get; set; } = "PENDING";
}

public sealed class ShopDailyStat
{
    public long ShopId { get; set; }
    public DateTime StatDate { get; set; }
    public int ListingViews { get; set; }
    public int WebsiteClicks { get; set; }
    public int PhoneClicks { get; set; }
    public int DirectionsClicks { get; set; }
}

public sealed class ShopImpression
{
    public long ImpressionId { get; set; }
    public long ShopId { get; set; }
    public string Surface { get; set; } = "";
    public short? Position { get; set; }
    public string? PageUrl { get; set; }
    public byte[]? SessionHash { get; set; }
    public bool IsBot { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ShopDailyStats
{
    public long ShopId { get; set; }
    public DateTime StatDate { get; set; }
    public string Surface { get; set; } = "";
    public int Impressions { get; set; }
    public int Views { get; set; }
    public int Clicks { get; set; }
    public int BotEvents { get; set; }
    public DateTime RolledUpAt { get; set; }
}

public sealed class SessionSalt
{
    public DateTime SaltDate { get; set; }
    public byte[] Salt { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public sealed class EventExhibitor
{
    public long ShopEventId { get; set; }
    public long ShopId { get; set; }
    public string? TableLabel { get; set; }
    public string? Specialties { get; set; }
    public string Status { get; set; } = "CLAIMED";
    public DateTime CreatedAt { get; set; }
    public ShopEvent? ShopEvent { get; set; }
    public Shop? Shop { get; set; }
}

public sealed class ShopMember
{
    public long ShopMemberId { get; set; }
    public long ShopId { get; set; }
    public long UserId { get; set; }
    public string MemberRole { get; set; } = "OWNER";
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Shop? Shop { get; set; }
    public AppUser? User { get; set; }
}

public sealed class ShopEditRequest
{
    public long ShopEditRequestId { get; set; }
    public Guid PublicId { get; set; }
    public long ShopId { get; set; }
    public long RequestedByUserId { get; set; }
    public string ProposedJson { get; set; } = "{}";
    public string Status { get; set; } = "PENDING_REVIEW";
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Shop? Shop { get; set; }
}

public sealed class Manufacturer
{
    public long ManufacturerId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? CountryName { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? WikipediaTitle { get; set; }
    public string SourceSection { get; set; } = "Manufacturers";
    public string? SourceSystem { get; set; }
    public DateTime? RetrievedOn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ManufacturerCategoryMap> Categories { get; set; } = new List<ManufacturerCategoryMap>();
    public ICollection<ManufacturerTopic> Topics { get; set; } = new List<ManufacturerTopic>();
}

public sealed class ManufacturerCategory
{
    public string CategoryCode { get; set; } = "";
    public string Name { get; set; } = "";
    public byte SortOrder { get; set; }
}

public sealed class ManufacturerCategoryMap
{
    public long ManufacturerId { get; set; }
    public string CategoryCode { get; set; } = "";
    public Manufacturer? Manufacturer { get; set; }
    public ManufacturerCategory? Category { get; set; }
}

public sealed class ManufacturerTopic
{
    public long ManufacturerTopicId { get; set; }
    public long ManufacturerId { get; set; }
    public string TopicGroup { get; set; } = "";
    public string TopicName { get; set; } = "";
    public Manufacturer? Manufacturer { get; set; }
}

public sealed class Promoter
{
    public long PromoterId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? WebsiteUrl { get; set; }
    public bool IsVerified { get; set; }
}

public sealed class Venue
{
    public long VenueId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address1 { get; set; }
    public string City { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "US";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
}

public sealed class ShopEvent
{
    public long ShopEventId { get; set; }
    public Guid PublicId { get; set; }
    public long? ShopId { get; set; }
    public long? PromoterId { get; set; }
    public long? VenueId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string EventType { get; set; } = "CARD_SHOW";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
    public string Status { get; set; } = "DRAFT";
    public string SourceType { get; set; } = "MANUAL";
    public string Scope { get; set; } = "LOCAL";
    public int? TableCount { get; set; }
    public string? AdmissionText { get; set; }
    public string? HoursText { get; set; }
    public string? FocusTags { get; set; }
    public string? OrganizerUrl { get; set; }
    public string? TicketUrl { get; set; }
    public bool IsFeatured { get; set; }
    public string? StateCode { get; set; }
    public string? City { get; set; }
    public string? RecurrenceSeries { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Shop? Shop { get; set; }
    public Promoter? Promoter { get; set; }
    public Venue? Venue { get; set; }
}

public sealed class EventSubmission
{
    public long EventSubmissionId { get; set; }
    public Guid PublicId { get; set; }
    public long? SubmittedByUserId { get; set; }
    public string SubmitterName { get; set; } = "";
    public string SubmitterEmail { get; set; } = "";
    public long? ShopId { get; set; }
    public long? PromoterId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
    public string? VenueName { get; set; }
    public string? Address1 { get; set; }
    public string? City { get; set; }
    public string? StateCode { get; set; }
    public string? PostalCode { get; set; }
    public string Scope { get; set; } = "LOCAL";
    public int? TableCount { get; set; }
    public string? AdmissionText { get; set; }
    public string? HoursText { get; set; }
    public string? FocusTags { get; set; }
    public string? OrganizerUrl { get; set; }
    public string? PromoterName { get; set; }
    public string Status { get; set; } = "PENDING_REVIEW";
    public long? ShopEventId { get; set; }
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Signup
{
    public long SignupId { get; set; }
    public Guid PublicId { get; set; }
    public string FormKey { get; set; } = "PREVIEW";
    public string Email { get; set; } = "";
    public string EmailNormalized { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? HobbySegment { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "PENDING_CONFIRM";
    public DateTime? ConfirmedAt { get; set; }
    public string Channel { get; set; } = "WEB";
    public string? Referrer { get; set; }
    public string? LandingPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Post
{
    public long PostId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Excerpt { get; set; }
    public string? Body { get; set; }
    public string? BodyMarkdown { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorOrg { get; set; }
    public string Status { get; set; } = "PUBLISHED";
    public DateTime? PublishedAt { get; set; }
    public string? SourcePage { get; set; }
    public long? IndustryFigureId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class IndustryFigure
{
    public long IndustryFigureId { get; set; }
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? AlsoKnownAs { get; set; }
    public string? RoleTitle { get; set; }
    public string? Summary { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? SourceNotes { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<IndustryFigureSource> Sources { get; set; } = new List<IndustryFigureSource>();
}

public sealed class IndustryFigureSource
{
    public long IndustryFigureSourceId { get; set; }
    public long IndustryFigureId { get; set; }
    public short SortOrder { get; set; }
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime? RetrievedOn { get; set; }
    public IndustryFigure? Figure { get; set; }
}

public sealed class GlossaryTerm
{
    public long GlossaryTermId { get; set; }
    public string Slug { get; set; } = "";
    public string Term { get; set; } = "";
    public string Definition { get; set; } = "";
    public short SortOrder { get; set; }
    public string? SourcePage { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class ConditionGrade
{
    public long ConditionGradeId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public byte Rank { get; set; }
    public string Description { get; set; } = "";
    public string? SourcePage { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class StaticPage
{
    public int PageId { get; set; }
    public string Slug { get; set; } = "";
    public string LocPath { get; set; } = "";
    public string Title { get; set; } = "";
    public string? SourceFile { get; set; }
    public bool IsIndexed { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class GalleryItem
{
    public long GalleryItemId { get; set; }
    public string Slug { get; set; } = "";
    public string Caption { get; set; } = "";
    public string? AltText { get; set; }
    public string LegacyPath { get; set; } = "";
    public short DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class Organization
{
    public long OrganizationId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string OrgType { get; set; } = "";
    public string ListingStatus { get; set; } = "ACTIVE";
}

public sealed class AgentUaRule
{
    public long AgentUaRuleId { get; set; }
    public string Token { get; set; } = "";
    public string AssistantName { get; set; } = "";
    public string Role { get; set; } = "SEARCH";
    public bool IsEnabled { get; set; } = true;
    public short SortOrder { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AgentVisit
{
    public long AgentVisitId { get; set; }
    public string AssistantName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsVerified { get; set; }
    public string? PagePath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AgentInquiry
{
    public long AgentInquiryId { get; set; }
    public string? AssistantName { get; set; }
    public string? HumanRole { get; set; }
    public string? StateCode { get; set; }
    public string? Zip3 { get; set; }
    public string? Interests { get; set; }
    public string? QuestionExcerpt { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
