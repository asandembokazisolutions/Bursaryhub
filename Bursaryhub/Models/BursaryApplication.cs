using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BursaryHub.Models;

public class BursaryApplication
{
    [Key]
    public int ApplicationId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int BursaryId { get; set; }

    public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Display(Name = "Why are you applying?")]
    public string? ApplicationNotes { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Withdrawn

    public DateTime? ReviewedDate { get; set; }

    public int? ReviewedByUserId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Review Notes")]
    public string? ReviewNotes { get; set; }

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(BursaryId))]
    public Bursary Bursary { get; set; } = null!;

    [ForeignKey(nameof(ReviewedByUserId))]
    public User? ReviewedByUser { get; set; }
}
