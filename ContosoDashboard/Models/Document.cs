using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Tags { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required]
    [MaxLength(255)]
    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int UploadedByUserId { get; set; }

    public int? ProjectId { get; set; }

    // Navigation properties
    [ForeignKey("UploadedByUserId")]
    public virtual User UploadedByUser { get; set; } = null!;

    [ForeignKey("ProjectId")]
    public virtual Project? Project { get; set; }

    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public virtual ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();
    public virtual ICollection<DocumentActivityLog> ActivityLogs { get; set; } = new List<DocumentActivityLog>();
}

public static class DocumentCategories
{
    public const string ProjectDocuments = "Project Documents";
    public const string TeamResources = "Team Resources";
    public const string PersonalFiles = "Personal Files";
    public const string Reports = "Reports";
    public const string Presentations = "Presentations";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        ProjectDocuments, TeamResources, PersonalFiles, Reports, Presentations, Other
    ];

    public static readonly string[] AllowedExtensions =
    [
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png"
    ];

    public const long MaxFileSizeBytes = 1024L * 1024 * 25; // 25 MB
}
