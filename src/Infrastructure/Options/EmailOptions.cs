namespace FlowDesk.Infrastructure.Options;

public class EmailOptions
{
    public const string SectionName = "EmailSettings";

    public bool EnableEmailSending { get; set; } = false;
    public string SmtpServer { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public bool EnableSsl { get; set; } = false;
    public string SenderName { get; set; } = "FlowDesk System";
    public string SenderEmail { get; set; } = "noreply@flowdesk.local";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
