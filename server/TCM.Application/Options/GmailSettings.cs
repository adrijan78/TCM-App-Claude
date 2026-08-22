namespace TCM.Application.Options;

/// <summary>
/// SPEC section 3.1 — GmailSettings. Gmail SMTP needs an app password on an account with 2FA;
/// a normal account password will not authenticate.
/// </summary>
public class GmailSettings
{
    public const string SectionName = "Gmail";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Taekwondo Club";
    public string AppPassword { get; set; } = string.Empty;

    /// <summary>True only when there is enough here to actually send.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(SenderEmail)
        && !string.IsNullOrWhiteSpace(AppPassword);
}
