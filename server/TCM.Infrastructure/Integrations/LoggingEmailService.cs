using Microsoft.Extensions.Logging;
using TCM.Application.Abstractions;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// Stand-in for the real Gmail SMTP sender, used whenever mail is not configured. It logs what
/// would have been sent so the whole app — including password reset — can be exercised locally
/// with no credentials. Phase 5 adds the SMTP implementation alongside it.
/// </summary>
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(SendEmailRequest request, CancellationToken ct = default)
    {
        // The body is logged at Debug because a password-reset link is a bearer credential:
        // useful on a developer machine, not something to leave in an Information-level log.
        logger.LogInformation(
            "Email suppressed (no SMTP configured): to {Recipient}, subject {Subject}",
            request.ToEmail, request.Subject);

        logger.LogDebug("Suppressed email body:\n{Body}", request.TextBody);

        return Task.CompletedTask;
    }
}
