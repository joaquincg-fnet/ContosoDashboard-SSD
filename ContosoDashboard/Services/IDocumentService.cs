using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

// ── Request / Response types ─────────────────────────────────────────────────

public record UploadDocumentRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,
    int? ProjectId,
    string FileName,
    string FilePath,
    long FileSize,
    string ContentType
);

public record UpdateDocumentMetadataRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags
);

public record ReplaceDocumentFileRequest(
    string FileName,
    string FilePath,
    long FileSize,
    string ContentType
);

public record DocumentSummary(
    int DocumentId,
    string Title,
    string Category,
    string? Tags,
    DateTime UploadedAt,
    long FileSize,
    string ContentType,
    string FileName,
    string UploaderName,
    string? ProjectName,
    bool CanEdit,
    bool CanDelete
);

public record DocumentQuery(
    string? Search = null,
    string? Category = null,
    int? ProjectId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string SortBy = "uploadedAt",
    bool Desc = true,
    int Page = 1
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

// ── Interface ────────────────────────────────────────────────────────────────

public interface IDocumentService
{
    // Upload
    Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int actingUserId);

    // Browse / Search
    Task<PagedResult<DocumentSummary>> GetDocumentsAsync(DocumentQuery query, int actingUserId);
    Task<List<DocumentSummary>> GetProjectDocumentsAsync(int projectId, int actingUserId);
    Task<List<DocumentSummary>> GetSharedWithMeAsync(int actingUserId);
    Task<List<DocumentSummary>> GetRecentDocumentsAsync(int actingUserId);
    Task<int> GetAccessibleDocumentCountAsync(int actingUserId);

    // Retrieve / Download
    Task<Document> GetDocumentAsync(int documentId, int actingUserId);

    // Edit
    Task UpdateDocumentMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int actingUserId);
    Task<string> ReplaceDocumentFileAsync(int documentId, ReplaceDocumentFileRequest request, int actingUserId);

    // Delete
    Task<string> DeleteDocumentAsync(int documentId, int actingUserId);

    // Task attachment
    Task AttachDocumentToTaskAsync(int documentId, int taskId, int actingUserId);
    Task<List<DocumentSummary>> GetTaskDocumentsAsync(int taskId, int actingUserId);

    // Sharing
    Task ShareDocumentAsync(int documentId, List<int> recipientUserIds, int actingUserId);

    // Audit
    Task<List<DocumentActivityLog>> GetActivityLogsAsync(int actingUserId);
}
