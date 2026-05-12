using System.Text;
using System.Text.Json;

namespace BursaryHub.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string firstName, string verifyUrl);
    Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl);
    Task SendApplicationDecisionAsync(string toEmail, string firstName, string bursaryName, string decision, string? notes);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger    = logger;
        _apiKey    = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? string.Empty;
        _fromEmail = config["Email:SenderEmail"] ?? "onboarding@resend.dev";
        _fromName  = config["Email:SenderName"]  ?? "BursaryHub";
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var payload = new
            {
                from    = $"{_fromName} <{_fromEmail}>",
                to      = new[] { toEmail },
                subject = subject,
                html    = htmlBody
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://api.resend.com/emails", content);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Resend API error for {Email}: {Error}", toEmail, error);
            }
        }
        catch (Exception ex)
        {
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
        var color = decision == "Approved" ? "#198754" : "#dc3545";
        var html = $"""
            <h2>Application Update</h2>
            <p>Hi {firstName}, your application for <strong>{bursaryName}</strong> has been reviewed.</p>
            <p>Decision: <span style="color:{color};font-weight:bold;">{decision}</span></p>
            {(string.IsNullOrWhiteSpace(notes) ? "" : $"<p>Reviewer notes: {notes}</p>")}
            <p>Log in to <a href="https://bursaryhub-1.onrender.com">BursaryHub</a> for more details.</p>
            <hr/><p style="color:#888;">BursaryHub Team</p>
            """;
        return SendAsync(toEmail, $"BursaryHub: Application {decision}", html);
    }
}
