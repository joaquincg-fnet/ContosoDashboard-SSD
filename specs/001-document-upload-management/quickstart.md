# Quickstart: Document Upload and Management

**Feature**: `001-document-upload-management`  
**Audience**: Developer implementing the feature  
**Prerequisites**: Read `spec.md`, `research.md`, `data-model.md`, `contracts/IDocumentService.md`

---

## What you're building

A document management feature for ContosoDashboard with:

1. **Upload** — Blazor `InputFile` component with progress, validation, and GUID-based secure storage.
2. **Browse/Search/Filter** — paginated (50/page) "My Documents" view with EF Core `IQueryable` composition.
3. **Download/Preview** — minimal API endpoint serving files from outside `wwwroot` with authorization.
4. **Edit/Delete** — metadata editing and permanent deletion with storage cleanup.
5. **Project integration** — project documents section on `ProjectDetails.razor`.
6. **Task integration** — document attachment on task detail view via `TaskDocument` join entity.
7. **Sharing** — share with specific users, triggering existing `NotificationService`.
8. **Dashboard widgets** — recent documents list and document count card.

---

## New files to create

```
ContosoDashboard/
├── Models/
│   ├── Document.cs
│   ├── DocumentShare.cs
│   ├── TaskDocument.cs
│   └── DocumentActivityLog.cs
├── Services/
│   ├── IDocumentService.cs
│   ├── DocumentService.cs
│   ├── IFileStorageService.cs
│   └── LocalFileStorageService.cs
├── Pages/
│   └── Documents.razor
└── wwwroot/
    └── (no changes — files stored outside wwwroot)

AppData/
└── uploads/                ← created at runtime by LocalFileStorageService
```

---

## Files to modify

| File | Change |
|------|--------|
| `Data/ApplicationDbContext.cs` | Add 4 new `DbSet<>` properties; configure indexes, unique constraints, cascade rules |
| `Models/Notification.cs` | Add `DocumentShared` and `ProjectDocumentAdded` to `NotificationType` enum |
| `Program.cs` | Register `IDocumentService`, `IFileStorageService`; add minimal API `/files/{id}` and `/files/{id}/preview` endpoints; add `app.MapControllers()` if needed |
| `Pages/ProjectDetails.razor` | Add "Project Documents" section |
| `Pages/Tasks.razor` | Add document attachment panel |
| `Pages/Index.razor` | Add "Recent Documents" widget and document count card |
| `Shared/NavMenu.razor` | Add Documents nav link |
| `Pages/_Imports.razor` | No change expected |

---

## Implementation Order (follows user story priority)

### Phase 1 — Foundation (required before any story)
1. Create 4 model classes (`Document`, `DocumentShare`, `TaskDocument`, `DocumentActivityLog`)
2. Update `ApplicationDbContext` with DbSets, indexes, and EF config
3. Create `IFileStorageService` and `LocalFileStorageService` (stores to `AppData/uploads/`)
4. Create `IDocumentService` interface
5. Register services in `Program.cs`
6. Add download/preview minimal API endpoints in `Program.cs`
7. Add `DocumentShared` and `ProjectDocumentAdded` to `NotificationType` enum
8. Run app to verify DB is created with new tables (EnsureCreated will apply new schema)

### Phase 2 — US1: Upload (P1)
9. Create `Documents.razor` page with `[Authorize]`, upload form, and `InputFile` component
10. Implement `DocumentService.UploadDocumentAsync()` and `LocalFileStorageService.SaveAsync()`
11. Wire upload flow: validate → generate GUID path → save file → save DB record
12. Add nav link in `NavMenu.razor`

### Phase 3 — US2: Browse & Filter (P2)
13. Implement `DocumentService.GetDocumentsAsync()` with IQueryable composition (search, filter, sort, paginate)
14. Add document list table to `Documents.razor` with sort/filter controls and pagination

### Phase 4 — US3: Download, Edit, Delete (P3)
15. Implement `DocumentService.GetDocumentAsync()`, `UpdateDocumentMetadataAsync()`, `DeleteDocumentAsync()`, `ReplaceDocumentFileAsync()`
16. Add download link (`<a target="_blank">`) and preview link to document list
17. Add edit modal and delete confirmation dialog to `Documents.razor`

### Phase 5 — US4: Project & Task Integration (P4)
18. Implement `GetProjectDocumentsAsync()` and `AttachDocumentToTaskAsync()`
19. Add "Project Documents" section to `ProjectDetails.razor`
20. Add document attachment panel to task detail view

### Phase 6 — US5: Sharing & Notifications (P5)
21. Implement `ShareDocumentAsync()` — calls `INotificationService` for each recipient
22. Implement `GetSharedWithMeAsync()`
23. Add "Shared with Me" tab/section to `Documents.razor`

### Phase 7 — Dashboard Integration
24. Implement `GetRecentDocumentsAsync()` and `GetAccessibleDocumentCountAsync()`
25. Add "Recent Documents" widget and count card to `Index.razor`

---

## Key implementation notes

### File storage path pattern
```
AppData/uploads/{userId}/{projectId or "personal"}/{guid}.{extension}
```
- Generate the GUID path **before** database insertion.
- Save file to disk first, then create DB record.
- Never use the original filename in the path (path traversal risk).
- Store the original filename in `Document.FileName` for the download Content-Disposition header.

### Upload sequence in DocumentService
```csharp
// 1. Validate extension and size (throw ArgumentException if invalid)
// 2. Check project authorization if ProjectId specified (throw UnauthorizedAccessException)
// 3. Generate relative path: $"{userId}/{projectIdStr}/{Guid.NewGuid()}.{ext}"
// 4. await _fileStorage.SaveAsync(stream, relativePath)  ← disk first
// 5. Create Document entity and save to DB              ← DB second
// 6. Log DocumentActivityLog (Action = "Upload")
// 7. If projectId set, notify project members via INotificationService
```

### Download endpoint in Program.cs
```csharp
app.MapGet("/files/{documentId:int}", async (int documentId, ...) => {
    // 1. Authenticate check
    // 2. Call documentService.GetDocumentAsync() — throws on unauthorized
    // 3. var absPath = fileStorage.ResolveAbsolutePath(doc.FilePath)
    // 4. Log download activity
    // 5. return Results.File(absPath, doc.ContentType, fileDownloadName: doc.FileName)
}).RequireAuthorization();

app.MapGet("/files/{documentId:int}/preview", async (int documentId, ...) => {
    // Same but: Response.Headers.ContentDisposition = "inline"
    // Only serve PDF and image content types inline
}).RequireAuthorization();
```

### Blazor IBrowserFile pattern
```csharp
// Extract metadata BEFORE opening stream
var fileName = SelectedFile.Name;
var fileSize = SelectedFile.Size;
var contentType = SelectedFile.ContentType;

using var memoryStream = new MemoryStream();
using (var stream = SelectedFile.OpenReadStream(maxFileSize: 1024 * 1024 * 25))
{
    await stream.CopyToAsync(memoryStream);
}
memoryStream.Position = 0;
SelectedFile = null;  // clear reference
inputFileKey++;       // reset InputFile component
```

### Pagination in DocumentService
```csharp
// Always AsNoTracking for read-only queries
// Count BEFORE Skip/Take
// Always OrderBy BEFORE Skip
// Use EF.Functions.Like for search (not .Contains())
```

---

## Testing the feature

1. Run app (`dotnet run`) — new tables created automatically via `EnsureCreated`.
2. Login as any user and navigate to `/documents`.
3. Upload a PDF ≤ 25 MB with a title and category → verify it appears in the list.
4. Upload a `.exe` → verify rejection with error message.
5. Upload a file > 25 MB → verify rejection with size error.
6. Filter by category → verify filtered results.
7. Click Download → verify file downloads with correct filename.
8. Click Preview (PDF/image) → verify file opens in new browser tab.
9. Edit title → verify updated title in list.
10. Delete document → verify removal from list and from `AppData/uploads/`.
11. Login as a Project Manager, upload a document to a project → login as team member and verify document appears in Project Details.
12. Login as Ni Kang, try to access a document uploaded by admin via direct URL → verify 403/redirect.

---

## Cloud migration path (for reference)

When migrating to Azure Blob Storage, only swap the DI registration:

```csharp
// Training (current):
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Production (future):
builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
```

No changes to `DocumentService`, `Documents.razor`, `ApplicationDbContext`, or the download endpoint.
