using System.Security.Claims;
using BursaryHub.Data;
using BursaryHub.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Services;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(string firstName, string lastName, string email,
        string phone, string password, HttpContext httpContext);

    Task<(bool Success, string Message)> LoginAsync(string email, string password, bool rememberMe,
        HttpContext httpContext, string ipAddress);

    Task LogoutAsync(HttpContext httpContext);

    Task<bool> VerifyEmailAsync(string token);

    Task<(bool Success, string Message)> ForgotPasswordAsync(string email, HttpContext httpContext);

    Task<(bool Success, string Message)> ResetPasswordAsync(string token, string email, string newPassword);

    Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailService _email;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _config;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes    = 30;

    public AuthService(ApplicationDbContext db, IPasswordHasher hasher,
        IEmailService email, ILogger<AuthService> logger, IConfiguration config)
    {
        _db     = db;
        _hasher = hasher;
        _email  = email;
        _logger = logger;
        _config = config;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string firstName, string lastName, string email,
        string phone, string password, HttpContext httpContext)
    {
        email = email.ToLowerInvariant().Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "Email already registered.");

        var token = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName               = firstName.Trim(),
            LastName                = lastName.Trim(),
            Email                   = email,
            PhoneNumber             = phone.Trim(),
            PasswordHash            = _hasher.Hash(password),
            RoleId                  = 3,
            IsActive                = true,
            IsEmailVerified         = false, // ✅ requires email verification
            VerificationToken       = token,
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            CreatedDate             = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var verifyUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/Account/VerifyEmail?token={token}";
        await _email.SendEmailVerificationAsync(email, firstName, verifyUrl);

        _logger.LogInformation("New user registered: {Email}", email);
        return (true, "Registration successful! Please check your email to verify your account.");
    }

    public async Task<(bool Success, string Message)> LoginAsync(
        string email, string password, bool rememberMe,
        HttpContext httpContext, string ipAddress)
    {
        email = email.ToLowerInvariant().Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogWarning("Failed login – email not found: {Email} IP:{IP}", email, ipAddress);
            return (false, "Invalid email or password.");
        }

        if (user.IsLockedOut)
        {
            _logger.LogWarning("Locked account login attempt: {Email} IP:{IP}", email, ipAddress);
            return (false, $"Account is temporarily locked. Please try again after {user.LockoutEndDate?.ToLocalTime():hh:mm tt}.");
        }

        if (!user.IsActive)
            return (false, "Invalid email or password.");

        if (!user.IsEmailVerified)
            return (false, "Please verify your email address before logging in.");

        if (!_hasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEndDate = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("Account locked after {Attempts} attempts: {Email}", MaxFailedAttempts, email);
            }
            await _db.SaveChangesAsync();
            _logger.LogWarning("Failed login attempt {Attempts}: {Email} IP:{IP}", user.FailedLoginAttempts, email, ipAddress);
            return (false, "Invalid email or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndDate      = null;
        user.LastLoginDate       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name,           user.FullName),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Role,           user.Role.RoleName),
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props     = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc   = DateTimeOffset.UtcNow.AddMinutes(
                rememberMe ? 10080 : int.Parse(_config["Authentication:CookieExpirationMinutes"] ?? "30")),
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        _logger.LogInformation("User logged in: {Email}", email);
        return (true, "Login successful.");
    }

    public async Task LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.VerificationToken == token &&
            u.VerificationTokenExpiry > DateTime.UtcNow);

        if (user == null) return false;

        user.IsEmailVerified        = true;
        user.VerificationToken      = null;
        user.VerificationTokenExpiry = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email, HttpContext httpContext)
    {
        email = email.ToLowerInvariant().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        const string msg = "If an account with that email exists, a reset link has been sent.";
        if (user == null) return (true, msg);

        var token = Guid.NewGuid().ToString("N");
        user.PasswordResetToken       = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var resetUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/Account/ResetPassword?token={token}&email={Uri.EscapeDataString(email)}";
        await _email.SendPasswordResetAsync(email, user.FirstName, resetUrl);

        return (true, msg);
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        string token, string email, string newPassword)
    {
        email = email.ToLowerInvariant().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == email &&
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null) return (false, "Invalid or expired reset link.");

        user.PasswordHash             = _hasher.Hash(newPassword);
        user.PasswordResetToken       = null;
        user.PasswordResetTokenExpiry = null;
        user.FailedLoginAttempts      = 0;
        user.LockoutEndDate           = null;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset completed for: {Email}", email);
        return (true, "Password reset successfully. You can now log in.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        if (!_hasher.Verify(currentPassword, user.PasswordHash))
            return (false, "Current password is incorrect.");

        user.PasswordHash = _hasher.Hash(newPassword);
        await _db.SaveChangesAsync();
        return (true, "Password changed successfully.");
    }
}
