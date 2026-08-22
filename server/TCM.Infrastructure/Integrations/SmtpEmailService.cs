using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Options;

namespace TCM.Infrastructure.Integrations;

/// <summary>
/// Sends the four transactional emails over Gmail SMTP (SPEC section 2). Registered only when
/// <see cref="GmailSettings.IsConfigured"/>; otherwise <see cref="LoggingEmailService"/> stands in.
/// </summary>
public class SmtpEmailService(
    IOptions<GmailSettings> settings,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(SendEmailRequest request, CancellationToken ct = default)
    {
        var config = settings.Value;

        try
        {
            using var client = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = true, // STARTTLS on 587
                Credentials = new NetworkCredential(config.SenderEmail, config.AppPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(config.SenderEmail, config.SenderName),
                Subject = request.Subject,
                Body = request.HtmlBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(request.ToEmail, request.ToName));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                request.TextBody, null, "text/plain"));

            await client.SendMailAsync(message, ct);

            logger.LogInformation("Sent \"{Subject}\" to {Recipient}.", request.Subject, request.ToEmail);
        }
        catch (Exception ex)
        {
            // Never rethrow. A coach must not lose a created training because SMTP timed out,
            // and a failed welcome email must not undo a registration.
            logger.LogError(ex, "Could not send \"{Subject}\" to {Recipient}.", request.Subject, request.ToEmail);
        }
    }
}
