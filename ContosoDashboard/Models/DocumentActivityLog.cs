using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentActivityLog
{
    [Key]
    public int DocumentActivityLogId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    public int ActingUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Details { get; set; }

    // Navigation properties
    [ForeignKey("DocumentId")]
    public virtual Document Document { get; set; } = null!;

    [ForeignKey("ActingUserId")]
    public virtual User ActingUser { get; set; } = null!;
}
