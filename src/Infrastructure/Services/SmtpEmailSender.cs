using System.Net;
using System.Net.Mail;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableEmailSending || string.IsNullOrWhiteSpace(_options.SmtpServer))
        {
            _logger.LogInformation("[MOCK EMAIL] To: {Recipient}, Subject: {Subject} (Body content suppressed for security)", recipientEmail, subject);
            await Task.CompletedTask;
            return;
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_options.SenderEmail, _options.SenderName);
            message.To.Add(new MailAddress(recipientEmail));
            message.Subject = subject;
            message.Body = htmlMessage;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_options.SmtpServer, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                smtpClient.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'", recipientEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject '{Subject}'", recipientEmail, subject);
        }
    }
}
