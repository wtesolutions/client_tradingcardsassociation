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
}

public sealed class StorageOptions
{
    public string? S3Bucket { get; set; }
    public string LocalRoot { get; set; } = "wwwroot/uploads";
}

public interface IAppEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken ct = default);
}

public interface IFileStorage
{
    bool UsesObjectStorage { get; }
    Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default);
}

public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);
