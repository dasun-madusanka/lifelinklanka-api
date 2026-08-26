using LifeLinkLanka.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LifeLinkLanka.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    public EmailService(ILogger<EmailService> logger) => _logger = logger;

    public Task SendEmailConfirmationAsync(string toEmail, Guid userId, string token)
    {
        // Local/dev stub: logs the link instead of sending real email.
        // Replace with MailKit SMTP send once you have real credentials.
        var link = $"http://localhost:5152/api/v1/auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("EMAIL CONFIRMATION LINK for {Email}: {Link}", toEmail, link);
        return Task.CompletedTask;
    }
}