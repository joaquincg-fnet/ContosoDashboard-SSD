using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        INotificationService notificationService)
    {
        _context = context;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DocumentSummary ToSummary(Document d, int actingUserId, UserRole actingUserRole, int? actingUserProjectManagerId = null)
    {
        var isOwner = d.UploadedByUserId == actingUserId;
        var isAdmin = actingUserRole == UserRole.Administrator;
        var isPmOfProject = d.ProjectId.HasValue && actingUserProjectManagerId == actingUserId;

        return new DocumentSummary(
            d.DocumentId,
            d.Title,
            d.Category,
            d.Tags,
            d.UploadedAt,
            d.FileSize,
            d.ContentType,
            d.FileName,
            d.UploadedByUser?.DisplayName ?? "",
            d.Project?.Name,
            CanEdit: isOwner,
            CanDelete: isOwner || isAdmin || isPmOfProject
        );
    }

    private async Task<(int UserId, UserRole Role, string? Department)> GetActingUserInfoAsync(int actingUserId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == actingUserId)
            .Select(u => new { u.UserId, u.Role, u.Department })
            .FirstOrDefaultAsync()
            ?? throw new UnauthorizedAccessException("User not found.");
        return (user.UserId, user.Role, user.Department);
    }

    private async Task LogActivityAsync(int documentId, int actingUserId, string action, string? details = null)
    {
        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = documentId,
            ActingUserId = actingUserId,
            Action = action,
            OccurredAt = DateTime.UtcNow,
            Details = details
        });
        // Saved as part of the outer SaveChangesAsync call, or standalone below
        await _context.SaveChangesAsync();
    }

    private IQueryable<Document> BuildAccessibleQuery(int actingUserId, UserRole role, string? department)
    {
        var q = _context.Documents
            .AsNoTracking()
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project);

        if (role == UserRole.Administrator)
            return q;

        // Own documents
        var own = q.Where(d => d.UploadedByUserId == actingUserId);

        // Shared with me
        var shared = q.Where(d => d.Shares.Any(s => s.SharedWithUserId == actingUserId));

        // Project documents (user is member or PM)
        var projectDocs = q.Where(d => d.ProjectId != null &&
            (d.Project!.ProjectManagerId == actingUserId ||
             d.Project.ProjectMembers.Any(pm => pm.UserId == actingUserId)));

        // Team Lead: same department
        if (role == UserRole.TeamLead && department != null)
        {
            var deptDocs = q.Where(d => d.UploadedByUser.Department == department);
            return own.Union(shared).Union(projectDocs).Union(deptDocs);
        }

        return own.Union(shared).Union(projectDocs);
    }

    // ── Upload ───────────────────────────────────────────────────────────────

    public async Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int actingUserId)
    {
        // Validate category
        if (!DocumentCategories.All.Contains(request.Category))
            throw new ArgumentException($"Invalid category: {request.Category}");

        // Validate file size
        if (request.FileSize > DocumentCategories.MaxFileSizeBytes)
            throw new ArgumentException($"File exceeds maximum size of 25 MB.");

        // Validate extension
        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!DocumentCategories.AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not supported.");

        // Validate project authorization
        if (request.ProjectId.HasValue)
        {
            var (_, role, _) = await GetActingUserInfoAsync(actingUserId);
            if (role != UserRole.Administrator)
            {
                var isMemberOrPm = await _context.Projects
                    .AnyAsync(p => p.ProjectId == request.ProjectId &&
                        (p.ProjectManagerId == actingUserId ||
                         p.ProjectMembers.Any(pm => pm.UserId == actingUserId)));
                if (!isMemberOrPm)
                    throw new UnauthorizedAccessException("You are not a member of that project.");
            }
        }

        var document = new Document
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category,
            Tags = request.Tags?.Trim(),
            FileName = request.FileName,
            FilePath = request.FilePath,
            FileSize = request.FileSize,
            ContentType = request.ContentType,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = actingUserId,
            ProjectId = request.ProjectId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Log activity
        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = document.DocumentId,
            ActingUserId = actingUserId,
            Action = "Upload",
            OccurredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Notify project members if project-associated
        if (document.ProjectId.HasValue)
        {
            var memberIds = await _context.Projects
                .Where(p => p.ProjectId == document.ProjectId)
                .SelectMany(p => p.ProjectMembers.Select(pm => pm.UserId))
                .Union(_context.Projects
                    .Where(p => p.ProjectId == document.ProjectId)
                    .Select(p => p.ProjectManagerId))
                .Where(uid => uid != actingUserId)
                .Distinct()
                .ToListAsync();

            var projectName = await _context.Projects
                .Where(p => p.ProjectId == document.ProjectId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync() ?? "your project";

            foreach (var memberId in memberIds)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = memberId,
                    Title = "New Project Document",
                    Message = $"A new document \"{document.Title}\" was added to {projectName}.",
                    Type = NotificationType.ProjectDocumentAdded,
                    Priority = NotificationPriority.Informational
                });
            }
        }

        return document;
    }

    // ── Browse / Search ──────────────────────────────────────────────────────

    public async Task<PagedResult<DocumentSummary>> GetDocumentsAsync(DocumentQuery query, int actingUserId)
    {
        var (_, role, department) = await GetActingUserInfoAsync(actingUserId);
        var q = BuildAccessibleQuery(actingUserId, role, department);

        // Search
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(d =>
                EF.Functions.Like(d.Title, pattern) ||
                (d.Description != null && EF.Functions.Like(d.Description, pattern)) ||
                (d.Tags != null && EF.Functions.Like(d.Tags, pattern)) ||
                EF.Functions.Like(d.UploadedByUser.DisplayName, pattern));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(d => d.Category == query.Category);

        if (query.ProjectId.HasValue)
            q = q.Where(d => d.ProjectId == query.ProjectId);

        if (query.DateFrom.HasValue)
            q = q.Where(d => d.UploadedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            q = q.Where(d => d.UploadedAt <= query.DateTo.Value);

        var totalCount = await q.CountAsync();

        // Sort
        q = query.SortBy switch
        {
            "title" => query.Desc ? q.OrderByDescending(d => d.Title) : q.OrderBy(d => d.Title),
            "fileSize" => query.Desc ? q.OrderByDescending(d => d.FileSize) : q.OrderBy(d => d.FileSize),
            "category" => query.Desc ? q.OrderByDescending(d => d.Category) : q.OrderBy(d => d.Category),
            _ => query.Desc ? q.OrderByDescending(d => d.UploadedAt) : q.OrderBy(d => d.UploadedAt)
        };

        var items = await q
            .Skip((query.Page - 1) * 50)
            .Take(50)
            .ToListAsync();

        var summaries = items.Select(d => ToSummary(d, actingUserId, role)).ToList();
        return new PagedResult<DocumentSummary>(summaries, totalCount, query.Page, 50);
    }

    public async Task<List<DocumentSummary>> GetProjectDocumentsAsync(int projectId, int actingUserId)
    {
        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);
        if (role != UserRole.Administrator)
        {
            var authorized = await _context.Projects
                .AnyAsync(p => p.ProjectId == projectId &&
                    (p.ProjectManagerId == actingUserId ||
                     p.ProjectMembers.Any(pm => pm.UserId == actingUserId)));
            if (!authorized)
                throw new UnauthorizedAccessException("You are not authorized to view documents for this project.");
        }

        var docs = await _context.Documents
            .AsNoTracking()
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return docs.Select(d => ToSummary(d, actingUserId, role)).ToList();
    }

    public async Task<List<DocumentSummary>> GetSharedWithMeAsync(int actingUserId)
    {
        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);
        var docs = await _context.DocumentShares
            .AsNoTracking()
            .Where(s => s.SharedWithUserId == actingUserId)
            .OrderByDescending(s => s.SharedAt)
            .Include(s => s.Document).ThenInclude(d => d.UploadedByUser)
            .Include(s => s.Document).ThenInclude(d => d.Project)
            .Select(s => s.Document)
            .ToListAsync();

        return docs.Select(d => ToSummary(d, actingUserId, role)).ToList();
    }

    public async Task<List<DocumentSummary>> GetRecentDocumentsAsync(int actingUserId)
    {
        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);
        var docs = await _context.Documents
            .AsNoTracking()
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.UploadedByUserId == actingUserId)
            .OrderByDescending(d => d.UploadedAt)
            .Take(5)
            .ToListAsync();

        return docs.Select(d => ToSummary(d, actingUserId, role)).ToList();
    }

    public async Task<int> GetAccessibleDocumentCountAsync(int actingUserId)
    {
        var (_, role, department) = await GetActingUserInfoAsync(actingUserId);
        return await BuildAccessibleQuery(actingUserId, role, department).CountAsync();
    }

    // ── Retrieve / Download ──────────────────────────────────────────────────

    public async Task<Document> GetDocumentAsync(int documentId, int actingUserId)
    {
        var doc = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project).ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var (_, role, department) = await GetActingUserInfoAsync(actingUserId);
        if (!CanAccess(doc, actingUserId, role, department))
            throw new UnauthorizedAccessException("You are not authorized to access this document.");

        // Log download activity (fire and forget style — best effort)
        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = documentId,
            ActingUserId = actingUserId,
            Action = "Download",
            OccurredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return doc;
    }

    private static bool CanAccess(Document doc, int actingUserId, UserRole role, string? department)
    {
        if (role == UserRole.Administrator) return true;
        if (doc.UploadedByUserId == actingUserId) return true;
        if (doc.Shares.Any(s => s.SharedWithUserId == actingUserId)) return true;
        if (doc.Project != null &&
            (doc.Project.ProjectManagerId == actingUserId ||
             doc.Project.ProjectMembers.Any(pm => pm.UserId == actingUserId)))
            return true;
        if (role == UserRole.TeamLead && department != null &&
            doc.UploadedByUser?.Department == department)
            return true;
        return false;
    }

    // ── Edit ─────────────────────────────────────────────────────────────────

    public async Task UpdateDocumentMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int actingUserId)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        if (doc.UploadedByUserId != actingUserId)
            throw new UnauthorizedAccessException("Only the document owner can edit metadata.");

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255)
            throw new ArgumentException("Title is required and must be 255 characters or fewer.");

        if (!DocumentCategories.All.Contains(request.Category))
            throw new ArgumentException($"Invalid category: {request.Category}");

        doc.Title = request.Title.Trim();
        doc.Description = request.Description?.Trim();
        doc.Category = request.Category;
        doc.Tags = request.Tags?.Trim();

        await _context.SaveChangesAsync();
        await LogActivityAsync(documentId, actingUserId, "Edit");
    }

    public async Task<string> ReplaceDocumentFileAsync(int documentId, ReplaceDocumentFileRequest request, int actingUserId)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        if (doc.UploadedByUserId != actingUserId)
            throw new UnauthorizedAccessException("Only the document owner can replace the file.");

        var oldPath = doc.FilePath;

        doc.FileName = request.FileName;
        doc.FilePath = request.FilePath;
        doc.FileSize = request.FileSize;
        doc.ContentType = request.ContentType;
        doc.UploadedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await LogActivityAsync(documentId, actingUserId, "Edit", $"File replaced: {request.FileName}");

        return oldPath;
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    public async Task<string> DeleteDocumentAsync(int documentId, int actingUserId)
    {
        var doc = await _context.Documents
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);

        var isOwner = doc.UploadedByUserId == actingUserId;
        var isAdmin = role == UserRole.Administrator;
        var isPm = role == UserRole.ProjectManager &&
                   doc.ProjectId.HasValue &&
                   doc.Project?.ProjectManagerId == actingUserId;

        if (!isOwner && !isAdmin && !isPm)
            throw new UnauthorizedAccessException("You are not authorized to delete this document.");

        var filePath = doc.FilePath;

        _context.Documents.Remove(doc); // cascades Shares, TaskDocuments, ActivityLogs
        await _context.SaveChangesAsync();

        return filePath;
    }

    // ── Task attachment ──────────────────────────────────────────────────────

    public async Task AttachDocumentToTaskAsync(int documentId, int taskId, int actingUserId)
    {
        var (_, role, department) = await GetActingUserInfoAsync(actingUserId);

        var doc = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project).ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        if (!CanAccess(doc, actingUserId, role, department))
            throw new UnauthorizedAccessException("You are not authorized to access this document.");

        var task = await _context.Tasks
            .Include(t => t.Project).ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(t => t.TaskId == taskId)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        // User must have access to the task
        var taskAccessible = task.AssignedUserId == actingUserId ||
                             task.CreatedByUserId == actingUserId ||
                             role == UserRole.Administrator ||
                             (task.Project != null &&
                              (task.Project.ProjectManagerId == actingUserId ||
                               task.Project.ProjectMembers.Any(pm => pm.UserId == actingUserId)));
        if (!taskAccessible)
            throw new UnauthorizedAccessException("You are not authorized to access this task.");

        // Skip if already attached
        var exists = await _context.TaskDocuments
            .AnyAsync(td => td.TaskId == taskId && td.DocumentId == documentId);
        if (!exists)
        {
            _context.TaskDocuments.Add(new TaskDocument
            {
                TaskId = taskId,
                DocumentId = documentId,
                AttachedAt = DateTime.UtcNow,
                AttachedByUserId = actingUserId
            });
        }

        // Inherit project association from task if document has none
        if (task.ProjectId.HasValue && !doc.ProjectId.HasValue)
        {
            var docToUpdate = await _context.Documents.FindAsync(documentId);
            if (docToUpdate != null)
                docToUpdate.ProjectId = task.ProjectId;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<DocumentSummary>> GetTaskDocumentsAsync(int taskId, int actingUserId)
    {
        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);

        var task = await _context.Tasks
            .Include(t => t.Project).ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(t => t.TaskId == taskId)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        var taskAccessible = task.AssignedUserId == actingUserId ||
                             task.CreatedByUserId == actingUserId ||
                             role == UserRole.Administrator ||
                             (task.Project != null &&
                              (task.Project.ProjectManagerId == actingUserId ||
                               task.Project.ProjectMembers.Any(pm => pm.UserId == actingUserId)));
        if (!taskAccessible)
            throw new UnauthorizedAccessException("You are not authorized to access this task.");

        var docs = await _context.TaskDocuments
            .AsNoTracking()
            .Where(td => td.TaskId == taskId)
            .OrderByDescending(td => td.AttachedAt)
            .Include(td => td.Document).ThenInclude(d => d.UploadedByUser)
            .Include(td => td.Document).ThenInclude(d => d.Project)
            .Select(td => td.Document)
            .ToListAsync();

        return docs.Select(d => ToSummary(d, actingUserId, role)).ToList();
    }

    // ── Sharing ──────────────────────────────────────────────────────────────

    public async Task ShareDocumentAsync(int documentId, List<int> recipientUserIds, int actingUserId)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        if (doc.UploadedByUserId != actingUserId)
            throw new UnauthorizedAccessException("Only the document owner can share this document.");

        foreach (var recipientId in recipientUserIds.Distinct())
        {
            if (recipientId == actingUserId) continue;

            var alreadyShared = await _context.DocumentShares
                .AnyAsync(s => s.DocumentId == documentId && s.SharedWithUserId == recipientId);
            if (alreadyShared) continue;

            _context.DocumentShares.Add(new DocumentShare
            {
                DocumentId = documentId,
                SharedWithUserId = recipientId,
                SharedByUserId = actingUserId,
                SharedAt = DateTime.UtcNow
            });

            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = recipientId,
                Title = "Document Shared With You",
                Message = $"A document \"{doc.Title}\" has been shared with you.",
                Type = NotificationType.DocumentShared,
                Priority = NotificationPriority.Informational
            });
        }

        await _context.SaveChangesAsync();

        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = documentId,
            ActingUserId = actingUserId,
            Action = "Share",
            OccurredAt = DateTime.UtcNow,
            Details = $"Shared with {recipientUserIds.Count} user(s)"
        });
        await _context.SaveChangesAsync();
    }

    // ── Audit ────────────────────────────────────────────────────────────────

    public async Task<List<DocumentActivityLog>> GetActivityLogsAsync(int actingUserId)
    {
        var (_, role, _) = await GetActingUserInfoAsync(actingUserId);
        if (role != UserRole.Administrator)
            throw new UnauthorizedAccessException("Only Administrators can view activity logs.");

        return await _context.DocumentActivityLogs
            .AsNoTracking()
            .Include(al => al.ActingUser)
            .Include(al => al.Document)
            .OrderByDescending(al => al.OccurredAt)
            .ToListAsync();
    }
}
