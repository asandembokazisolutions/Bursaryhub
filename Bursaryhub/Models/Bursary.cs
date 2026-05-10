using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BursaryHub.Models;

public class Bursary
{
    [Key]
    public int BursaryId { get; set; }

    [Required, MaxLength(200), MinLength(5)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2000), MinLength(10)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Provider { get; set; } = string.Empty;

    [Required, Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [Display(Name = "Application Deadline")]
    public DateTime ApplicationDeadline { get; set; }

    [Required]
    [Display(Name = "Award Date")]
    public DateTime AwardDate { get; set; }

    [MaxLength(200)]
    [Display(Name = "Eligibility Criteria")]
    public string? EligibilityCriteria { get; set; }

    [MaxLength(500), Url]
    [Display(Name = "Application URL")]
    public string? ApplicationUrl { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Closed, Expired

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsScraped { get; set; } = false;

    public int? CreatedByUserId { get; set; }

    // Navigation
    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedByUser { get; set; }
    public ICollection<BursaryApplication> Applications { get; set; } = new List<BursaryApplication>();

    // Computed
    [NotMapped]
    public bool IsDeadlinePassed => ApplicationDeadline < DateTime.UtcNow;

    [NotMapped]
    public int DaysUntilDeadline => Math.Max(0, (int)(ApplicationDeadline - DateTime.UtcNow).TotalDays);

    [NotMapped]
    public int ApplicationCount => Applications?.Count ?? 0;

    [NotMapped]
    public string StatusBadge => DaysUntilDeadline == 0
        ? "Closed"
        : DaysUntilDeadline <= 7 ? "Closing Soon" : "Active";
}
