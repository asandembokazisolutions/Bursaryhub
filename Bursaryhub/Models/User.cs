using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BursaryHub.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50), MinLength(2)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(255), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;

    [MaxLength(500)]
    public string? VerificationToken { get; set; }

    public DateTime? VerificationTokenExpiry { get; set; }

    [MaxLength(500)]
    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEndDate { get; set; }

    [ForeignKey(nameof(Role))]
    public int RoleId { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public ICollection<BursaryApplication> Applications { get; set; } = new List<BursaryApplication>();
    public ICollection<Bursary> CreatedBursaries { get; set; } = new List<Bursary>();

    // Computed
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [NotMapped]
    public bool IsLockedOut => LockoutEndDate.HasValue && LockoutEndDate > DateTime.UtcNow;
}
