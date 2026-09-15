namespace FlowDesk.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string recipientEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default);
}
