namespace Ntca.Core;

public sealed class SocketLabsOptions
{
    public int ServerId { get; set; }
    public string ApiKey { get; set; } = "";
}

public sealed class EmailOptions
{
    public string FromAddress { get; set; } = "reports@wte.net";
    public string FromName { get; set; } = "National Trading Card Association";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}

public sealed class AuthOptions
{
    /// <summary>Development-only. Ignored when the host environment is Production.</summary>
    public bool DevBypassCode { get; set; }
}

public sealed record EmailSendResult(bool Delivered, string Channel, string UserMessage, string? PickupPath = null);

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string? S3Bucket { get; set; }
    public string? Bucket { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? PublicBaseUrl { get; set; }
    public string? Profile { get; set; }
    public string LocalRoot { get; set; } = "wwwroot/uploads";
    public string? LegacyRoot { get; set; }

    public string ResolvedBucket =>
        !string.IsNullOrWhiteSpace(Bucket) ? Bucket
        : (S3Bucket ?? "");

    public bool UseS3 =>
        string.Equals(Provider, "S3", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ResolvedBucket);
}

public sealed class GooglePlacesOptions
{
    public string ApiKey { get; set; } = "";
    public int DailyCallLimit { get; set; } = 300;
    public int MonthlyCallLimit { get; set; } = 3000;
    public int PhotoMaxWidthPx { get; set; } = 800;
    public int PhotosPerShop { get; set; } = 3;
    public int NightlyRefreshBatch { get; set; } = 0;
    public bool Enabled { get; set; } = true;

    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
}

public interface IAppEmailSender
{
    bool IsConfigured { get; }
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken ct = default);
}

public interface IFileStorage
{
    bool UsesObjectStorage { get; }
    string PublicUrl(string relativePath);
    Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default);
}

public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);
