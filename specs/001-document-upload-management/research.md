# Research: Document Upload and Management

**Feature**: `001-document-upload-management`  
**Phase**: 0 — Pre-design research  
**Date**: 2026-04-30

---

## Topic 1: Blazor Server File Upload Patterns

### Decision
Use `InputFile` component with chunk-based `ReadAsync` loop into `MemoryStream`, enforcing a server-side `maxFileSize` constant of `1024 * 1024 * 25` (25 MB). Use `@key="inputFileKey"` with key increment after upload to reset the component. Show progress via `progressPercent` decimal updated after each chunk and `StateHasChanged()`.

### Key patterns
- **IBrowserFile disposal safe**: Copy stream to `MemoryStream` inside the `OnChange` handler before it returns. Extract `Name`, `Size`, `ContentType` into local variables *before* opening the stream.
- **Progress**: Manual `ReadAsync` loop with 10 KB buffer; update `progressPercent = totalRead / file.Size` and call `StateHasChanged()` each iteration.
- **Reset InputFile**: Increment `inputFileKey` after upload completes — Blazor destroys/recreates the element.
- **Size limit**: Pass explicit `maxAllowedSize` constant to `OpenReadStream`. Never use `file.Size` (client-supplied, unsafe).
- **Multi-file**: Single-file upload for initial release; multi-file patterns require `LazyBrowserFileStream` to avoid SignalR stream disposal.

### Rationale
Chunk-based progress pattern is the official ASP.NET Core recommendation for Blazor Server large-file uploads and is the most teachable pattern for training.

### Alternatives considered
- Streaming directly to disk (bypasses MemoryStream) — suitable for very large files but adds complexity; MemoryStream is fine for ≤25 MB.
- JS-based XMLHttpRequest upload — avoids Blazor SignalR limits but breaks clean separation of concerns.

---

## Topic 2: Secure File Serving from Outside wwwroot

### Decision
Add a minimal API endpoint in `Program.cs` at `/files/{documentId}` that performs authorization, resolves the physical path via `IFileStorageService`, and returns `Results.File()` with `Content-Disposition: inline` (preview) or `attachment` (download). Open preview in a new tab from Blazor via an `<a target="_blank">` anchor pointing to `/files/{id}/preview`.

### Key patterns
- **Authorization**: `.RequireAuthorization()` on the endpoint + service-level ownership/membership check before path resolution.
- **Path traversal guard**: Resolve path through `IFileStorageService`; validate resolved absolute path starts with the storage root before opening.
- **Download**: `Results.File(path, mimeType, fileDownloadName: originalName)` → `Content-Disposition: attachment`.
- **Preview (new tab)**: Set `Content-Disposition: inline`; use `<a href="/files/{id}/preview" target="_blank">` in Blazor (avoids popup-blocker issues vs JSInterop `window.open`).
- **Wiring in Program.cs**: Register `app.MapGet("/files/...")` before `app.MapFallbackToPage("/_Host")`.
- **JSInterop fallback**: `JS.InvokeVoidAsync("openInNewTab", url)` only inside direct button click handlers if anchor approach is insufficient.

### Rationale
Minimal API endpoints integrate cleanly with existing Blazor Server `Program.cs` without adding a controller project. The anchor-tag preview approach avoids popup blocker issues.

### Alternatives considered
- MVC Controller — works but adds `AddControllers()` and a Controllers folder; overkill for two endpoints.
- Static file middleware with a custom path — doesn't support per-request authorization checks.

---

## Topic 3: EF Core Pagination and Combined Search/Filter/Sort

### Decision
Use `IQueryable` composition with `EF.Functions.Like` for search, chained `.Where()` for filters, `.OrderBy()`/`.OrderByDescending()` for sort, and `Skip`/`Take` for 50-item pagination. Execute two queries per request: `CountAsync()` on the filtered query, then `ToListAsync()` on the paged query.

### Key patterns
```csharp
// Search: use EF.Functions.Like, not .Contains() — keeps queries sargable
var pattern = $"%{search}%";
q = q.Where(d =>
    EF.Functions.Like(d.Title, pattern) ||
    EF.Functions.Like(d.Description, pattern) ||
    EF.Functions.Like(d.Tags, pattern) ||
    EF.Functions.Like(d.Uploader.FullName, pattern));

// Count (on filtered, un-paged query)
int total = await q.CountAsync();

// Sort then page
q = q.OrderByDescending(d => d.UploadedAt);
var items = await q.Skip((page - 1) * 50).Take(50).ToListAsync();
```
- Always `AsNoTracking()` on read-only queries.
- Always `OrderBy` before `Skip` — required by SQL Server.
- `DocumentQuery` parameter object holds: `Search`, `Category`, `ProjectId`, `DateFrom`, `DateTo`, `SortBy`, `Desc`, `Page`.

### Rationale
`IQueryable` composition gives compile-time safety, automatic parameterization (no SQL injection risk), and clean composability for the dynamic filter combinations required. Raw SQL adds no benefit here.

### Alternatives considered
- `.Contains()` for search — translates to `CHARINDEX` or `LOWER()` in SQL Server, defeating indexes.
- Raw SQL / stored procedures — full control but loses type safety; not appropriate for training.
- In-memory filtering (load all, filter in C#) — fails the 2-second performance target at 500 documents.

---

## Topic 4: Notification Integration Path (Deferred from Clarify)

### Decision
Reuse the existing `NotificationService` for document notifications. `DocumentService` will call `INotificationService.CreateNotificationAsync()` after successful upload/share operations — the same pattern used by `TaskService` and `ProjectService`.

### Rationale
The existing notification system already handles in-app notifications with recipient targeting. Introducing a separate notification mechanism would violate Constitution Principle IV (Clean Architecture) and add unnecessary complexity.

### Alternatives considered
- New `DocumentNotificationService` — redundant; `INotificationService` already covers the required behavior.
- Direct DbContext calls from `DocumentService` for notifications — violates service layer separation.
