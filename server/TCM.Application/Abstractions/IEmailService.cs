namespace TCM.Application.Abstractions;

/// <summary>SPEC section 3.1 — SendEmailRequest.</summary>
public record SendEmailRequest(string ToEmail, string ToName, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Sends the four transactional emails in SPEC section 2: registration confirmation, password
/// reset, training invitation and note notification.
/// </summary>
/// <remarks>
/// Implementations must never throw into the caller. A mail failure is logged and swallowed —
/// a coach must not lose a created training because SMTP timed out.
/// </remarks>
public interface IEmailService
{
    Task SendAsync(SendEmailRequest request, CancellationToken ct = default);
}
