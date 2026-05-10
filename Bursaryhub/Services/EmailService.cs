using MailKit.Net.Smtp;
using MimeKit;

namespace BursaryHub.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string firstName, string verifyUrl);
    Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl);
    Task SendApplicationDecisionAsync(string toEmail, string firstName, string bursaryName, string decision, string? notes);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpServer   = _config["Email:SmtpServer"]   ?? "smtp.gmail.com";
            var smtpPort     = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var senderEmail  = _config["Email:SenderEmail"]  ?? "noreply@bursaryhub.com";
            var senderPass   = _config["Email:SenderPassword"] ?? string.Empty;
            var senderName   = _config["Email:SenderName"]   ?? "BursaryHub";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail, senderPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Don't crash the app if email fails – just log
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
        }
    }

    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verifyUrl)
    {
        var html = $"""
            <h2>Welcome to BursaryHub, {firstName}!</h2>
            <p>Please verify your email address to activate your account.</p>
            <p><a href="{verifyUrl}" style="background:#0d6efd;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;">Verify Email</a></p>
            <p>This link expires in 24 hours.</p>
            <hr/><p style="color:#888;">BursaryHub Team</p>
            """;
        return SendAsync(toEmail, "Verify Your BursaryHub Account", html);
    }

    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl)
    {
        var html = $"""
            <h2>Password Reset Request</h2>
            <p>Hi {firstName}, we received a request to reset your BursaryHub password.</p>
            <p><a href="{resetUrl}" style="background:#dc3545;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;">Reset Password</a></p>
            <p>This link expires in 1 hour. If you did not request a reset, ignore this email.</p>
            <hr/><p style="color:#888;">BursaryHub Team</p>
            """;
        return SendAsync(toEmail, "Reset Your BursaryHub Password", html);
    }

    public Task SendApplicationDecisionAsync(string toEmail, string firstName, string bursaryName, string decision, string? notes)
    {
        var color  = decision == "Approved" ? "#198754" : "#dc3545";
        var html = $"""
            <h2>Application Update</h2>
            <p>Hi {firstName}, your application for <strong>{bursaryName}</strong> has been reviewed.</p>
            <p>Decision: <span style="color:{color};font-weight:bold;">{decision}</span></p>
            {(string.IsNullOrWhiteSpace(notes) ? "" : $"<p>Reviewer notes: {notes}</p>")}
            <p>Log in to <a href="https://yourdomain.com">BursaryHub</a> for more details.</p>
            <hr/><p style="color:#888;">BursaryHub Team</p>
            """;
        return SendAsync(toEmail, $"BursaryHub: Application {decision}", html);
    }
}
