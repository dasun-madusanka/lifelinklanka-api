namespace LifeLinkLanka.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, Guid userId, string token);
}