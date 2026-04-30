# Service Contract: IDocumentService

**Feature**: `001-document-upload-management`  
**Layer**: Business Logic / Service  
**Implementation**: `DocumentService : IDocumentService`

This document defines the public interface contract for all document business logic. Blazor pages and any future API layer interact exclusively through this interface — never directly via `ApplicationDbContext`.

---

## Interface Definition

```csharp
namespace ContosoDashboard.Services;

public interface IDocumentService
{
    // ── Upload ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Validates, stores, and records a new document upload.
    /// File must be saved to disk BEFORE calling this method.
    /// Returns the created Document on success.
    /// Throws ArgumentException on validation failure.
    /// Throws UnauthorizedAccessException if user is not authorized to upload to the given project.
    /// </summary>
    Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int actingUserId);

    // ── Browse / Search ──────────────────────────────────────────────────────
    /// <summary>
    /// Returns paginated documents visible to the acting user (own + shared + project).
    /// Applies search, filter, and sort from the query object.
    /// Page size is fixed at 50.
    /// </summary>
    Task<PagedResult<DocumentSummary>> GetDocumentsAsync(DocumentQuery query, int actingUserId);

    /// <summary>
    /// Returns all documents associated with a specific project.
    /// Acting user must be a project member, Project Manager of the project, or Administrator.
    /// Throws UnauthorizedAccessException if not authorized.
    /// </summary>
    Task<List<DocumentSummary>> GetProjectDocumentsAsync(int projectId, int actingUserId);

    /// <summary>
    /// Returns documents shared directly with the acting user ("Shared with Me").
    /// </summary>
    Task<List<DocumentSummary>> GetSharedWithMeAsync(int actingUserId);

    /// <summary>
    /// Returns the 5 most recently uploaded documents for the acting user (dashboard widget).
    /// </summary>
    Task<List<DocumentSummary>> GetRecentDocumentsAsync(int actingUserId);

    /// <summary>
    /// Returns the total count of documents accessible to the acting user (dashboard card).
    /// </summary>
    Task<int> GetAccessibleDocumentCountAsync(int actingUserId);

    // ── Retrieve / Download ──────────────────────────────────────────────────
    /// <summary>
    /// Returns full document details for display or download.
    /// Throws UnauthorizedAccessException if the acting user is not authorized to access this document.
    /// Throws KeyNotFoundException if the document does not exist.
    /// </summary>
    Task<Document> GetDocumentAsync(int documentId, int actingUserId);

    // ── Edit ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Updates document metadata (title, description, category, tags).
    /// Acting user must be the document owner.
    /// Throws UnauthorizedAccessException if not authorized.
    /// Throws ArgumentException on validation failure.
    /// </summary>
    Task UpdateDocumentMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int actingUserId);

    /// <summary>
    /// Replaces the file of an existing document record.
    /// New file must be saved to disk BEFORE calling this method.
    /// The old file path is returned so the caller can delete it from storage.
    /// Acting user must be the document owner.
    /// </summary>
    Task<string> ReplaceDocumentFileAsync(int documentId, ReplaceDocumentFileRequest request, int actingUserId);

    // ── Delete ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Permanently deletes a document record and returns the file path for storage cleanup.
    /// Acting user must be the document owner OR a Project Manager of the associated project OR an Administrator.
    /// Throws UnauthorizedAccessException if not authorized.
    /// </summary>
    Task<string> DeleteDocumentAsync(int documentId, int actingUserId);

    // ── Task Attachment ──────────────────────────────────────────────────────
    /// <summary>
    /// Attaches a document to a task (many-to-many via TaskDocument).
    /// Acting user must have access to both the document and the task.
    /// If the task has a project, the document's ProjectId is updated to match (if not already set).
    /// </summary>
    Task AttachDocumentToTaskAsync(int documentId, int taskId, int actingUserId);

    /// <summary>
    /// Returns all documents attached to a specific task.
    /// Acting user must be the task's assigned user, creator, or a project member.
    /// </summary>
    Task<List<DocumentSummary>> GetTaskDocumentsAsync(int taskId, int actingUserId);

    // ── Sharing ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Shares a document with one or more users.
    /// Triggers an in-app notification to each recipient.
    /// Acting user must be the document owner.
    /// </summary>
    Task ShareDocumentAsync(int documentId, List<int> recipientUserIds, int actingUserId);

    // ── Audit ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns activity logs for all documents (Administrator only).
    /// Throws UnauthorizedAccessException if acting user is not Administrator.
    /// </summary>
    Task<List<DocumentActivityLog>> GetActivityLogsAsync(int actingUserId);
}
```

---

## Request / Response Types

```csharp
public record UploadDocumentRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,
    int? ProjectId,
    string FileName,       // original file name (for Content-Disposition)
    string FilePath,       // GUID-based relative path (generated before DB insert)
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
    bool CanEdit,     // true if acting user is owner
    bool CanDelete    // true if acting user is owner, PM of project, or Admin
);

public record DocumentQuery(
    string? Search,
    string? Category,
    int? ProjectId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string SortBy,    // "title" | "uploadedAt" | "fileSize" | "category"
    bool Desc,
    int Page          // 1-based; page size is fixed at 50
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

---

## IFileStorageService Contract

```csharp
namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a stream to the storage backend.
    /// Returns the relative file path (to be stored in the Document record).
    /// Path is generated by the caller (GUID-based) and passed in — not generated here.
    /// </summary>
    Task SaveAsync(Stream fileStream, string relativePath);

    /// <summary>
    /// Opens a read stream for a file at the given relative path.
    /// Throws FileNotFoundException if the file does not exist.
    /// </summary>
    Task<Stream> OpenReadAsync(string relativePath);

    /// <summary>
    /// Returns the absolute physical path for a relative path.
    /// Used by the download endpoint to serve the file via PhysicalFile().
    /// Validates that the resolved path is within the storage root (path traversal guard).
    /// </summary>
    string ResolveAbsolutePath(string relativePath);

    /// <summary>
    /// Deletes a file at the given relative path.
    /// No-op if the file does not exist.
    /// </summary>
    Task DeleteAsync(string relativePath);
}
```

---

## Authorization Rules (enforced in DocumentService)

| Operation | Allowed roles / conditions |
|-----------|---------------------------|
| Upload to personal | Any authenticated user |
| Upload to project | Project member, Project Manager of that project, or Administrator |
| View own documents | Document uploader |
| View project documents | Any project team member, PM, or Administrator |
| View shared document | Recipient of an explicit share |
| View dept documents (Team Lead) | Team Lead sees documents from all users in same department |
| View all documents | Administrator only |
| Edit metadata | Document owner only |
| Replace file | Document owner only |
| Delete | Document owner, Project Manager of associated project, or Administrator |
| Share | Document owner only |
| Attach to task | User with access to both document and task |
| View audit logs | Administrator only |
