using System.ComponentModel.DataAnnotations;

namespace BursaryHub.Models.ViewModels;

// ─── Account ────────────────────────────────────────────────────────────────

public class RegisterViewModel
{
    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    [Display(Name = "Phone Number")]
    [RegularExpression(@"^[\d\s\-\(\)]{10,15}$", ErrorMessage = "Enter a valid phone number (10-15 digits)")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Password")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+\-]).{8,128}$",
        ErrorMessage = "Password must be 8-128 chars with uppercase, lowercase, digit, and special character.")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+\-]).{8,128}$",
        ErrorMessage = "Password must be 8-128 chars with uppercase, lowercase, digit, and special character.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "New Password")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+\-]).{8,128}$",
        ErrorMessage = "Password must be 8-128 chars with uppercase, lowercase, digit, and special character.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

// ─── Admin ──────────────────────────────────────────────────────────────────

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalBursaries { get; set; }
    public int TotalApplications { get; set; }
    public int PendingApplications { get; set; }
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
    public List<TopBursaryItem> TopBursaries { get; set; } = new();
    public Dictionary<string, int> ApplicationsByStatus { get; set; } = new();
    public Dictionary<string, int> BursariesByProvider { get; set; } = new();
}

public class RecentActivityItem
{
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = "bi-activity";
}

public class TopBursaryItem
{
    public string Name { get; set; } = string.Empty;
    public int ApplicationCount { get; set; }
}

public class EditUserViewModel
{
    public int UserId { get; set; }

    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public int RoleId { get; set; }

    public bool IsActive { get; set; }
    public List<Role> AvailableRoles { get; set; } = new();
}

// ─── Bursary ─────────────────────────────────────────────────────────────────

public class BursaryFormViewModel
{
    public int BursaryId { get; set; }

    [Required, MaxLength(200), MinLength(5)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2000), MinLength(10)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Provider { get; set; } = string.Empty;

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Amount must be > 0")]
    public decimal Amount { get; set; }

    [Required]
    [Display(Name = "Application Deadline")]
    public DateTime ApplicationDeadline { get; set; } = DateTime.Today.AddDays(30);

    [Required]
    [Display(Name = "Award Date")]
    public DateTime AwardDate { get; set; } = DateTime.Today.AddDays(60);

    [MaxLength(200)]
    [Display(Name = "Eligibility Criteria")]
    public string? EligibilityCriteria { get; set; }

    [MaxLength(500), Url]
    [Display(Name = "Application URL")]
    public string? ApplicationUrl { get; set; }
}

public class BursarySearchViewModel
{
    public string? Search { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? DeadlineFilter { get; set; } // "7days","30days","more30"
    public string? SortBy { get; set; } // "deadline","amount","provider","date"
    public int Page { get; set; } = 1;
}

public class ApplyBursaryViewModel
{
    [Required]
    public int BursaryId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Why are you applying?")]
    public string? ApplicationNotes { get; set; }
}

public class ReviewApplicationViewModel
{
    [Required]
    public int ApplicationId { get; set; }

    [Required(ErrorMessage = "Please select Approve or Reject")]
    public string Decision { get; set; } = string.Empty; // "Approved" or "Rejected"

    [MaxLength(500)]
    [Display(Name = "Review Notes")]
    public string? ReviewNotes { get; set; }
}
