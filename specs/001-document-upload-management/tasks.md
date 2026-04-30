---
description: "Task list for Document Upload and Management feature"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-upload-management/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/IDocumentService.md ✅, quickstart.md ✅

**Tests**: Not requested — no test tasks included.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- All paths are relative to the `ContosoDashboard/` project folder unless noted

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project and folder structure initialization — no logic yet.

- [X] T001 Verify `AppData/uploads/` directory path is outside `wwwroot/` and confirm `LocalFileStorageService` will create it at runtime (no manual step required — document in quickstart.md)
- [X] T002 [P] Confirm `ContosoDashboard.csproj` targets .NET 8.0 and no new NuGet packages are required (all needed APIs are in-box)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data layer, storage abstraction, service interface, and file-serving endpoints — MUST be complete before any user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Models

- [X] T003 [P] Create `ContosoDashboard/Models/Document.cs` with all fields from data-model.md: `DocumentId` (int PK), `Title`, `Description`, `Category`, `Tags`, `FileName`, `FilePath`, `FileSize`, `ContentType`, `UploadedAt`, `UploadedByUserId` (FK), `ProjectId` (FK nullable), and navigation properties (`UploadedByUser`, `Project`, `Shares`, `TaskDocuments`, `ActivityLogs`)
- [X] T004 [P] Create `ContosoDashboard/Models/DocumentShare.cs` with fields: `DocumentShareId` (int PK), `DocumentId` (FK), `SharedWithUserId` (FK), `SharedByUserId` (FK), `SharedAt`, and navigation properties
- [X] T005 [P] Create `ContosoDashboard/Models/TaskDocument.cs` with fields: `TaskDocumentId` (int PK), `TaskId` (FK → TaskItem), `DocumentId` (FK), `AttachedAt`, `AttachedByUserId` (FK), and navigation properties
- [X] T006 [P] Create `ContosoDashboard/Models/DocumentActivityLog.cs` with fields: `DocumentActivityLogId` (int PK), `DocumentId` (FK), `ActingUserId` (FK), `Action` (MaxLength 50), `OccurredAt`, `Details` (nullable), and navigation properties
- [X] T007 Extend `NotificationType` enum in `ContosoDashboard/Models/Notification.cs` with two new values: `DocumentShared` and `ProjectDocumentAdded`

### Database Context

- [X] T008 Update `ContosoDashboard/Data/ApplicationDbContext.cs`: add `DbSet<Document> Documents`, `DbSet<DocumentShare> DocumentShares`, `DbSet<TaskDocument> TaskDocuments`, `DbSet<DocumentActivityLog> DocumentActivityLogs`; configure EF Core indexes on `Document.UploadedByUserId` and `Document.ProjectId`; configure unique index on `(DocumentShare.DocumentId, DocumentShare.SharedWithUserId)`; configure unique index on `(TaskDocument.TaskId, TaskDocument.DocumentId)`; configure cascade delete rules from data-model.md (Document→Shares cascade, Document→TaskDocuments cascade, Document→ActivityLogs cascade, Project→Documents SetNull, User→Documents Restrict)

### Storage Abstraction

- [X] T009 [P] Create `ContosoDashboard/Services/IFileStorageService.cs` with methods: `SaveAsync(Stream fileStream, string relativePath)`, `OpenReadAsync(string relativePath)`, `ResolveAbsolutePath(string relativePath)` (with path-traversal guard), `DeleteAsync(string relativePath)`
- [X] T010 Create `ContosoDashboard/Services/LocalFileStorageService.cs` implementing `IFileStorageService` using `System.IO`: storage root is `Path.Combine(Directory.GetCurrentDirectory(), "AppData", "uploads")`; `SaveAsync` creates directory structure and writes stream; `ResolveAbsolutePath` validates resolved path starts with storage root before returning; `DeleteAsync` is no-op if file not found; directory created on first write

### Service Interface

- [X] T011 [P] Create `ContosoDashboard/Services/IDocumentService.cs` with all methods from `contracts/IDocumentService.md`: `UploadDocumentAsync`, `GetDocumentsAsync`, `GetProjectDocumentsAsync`, `GetSharedWithMeAsync`, `GetRecentDocumentsAsync`, `GetAccessibleDocumentCountAsync`, `GetDocumentAsync`, `UpdateDocumentMetadataAsync`, `ReplaceDocumentFileAsync`, `DeleteDocumentAsync`, `AttachDocumentToTaskAsync`, `GetTaskDocumentsAsync`, `ShareDocumentAsync`, `GetActivityLogsAsync`; include request/response record types `UploadDocumentRequest`, `UpdateDocumentMetadataRequest`, `ReplaceDocumentFileRequest`, `DocumentSummary`, `DocumentQuery`, `PagedResult<T>` in the same file or a companion `DocumentServiceModels.cs`

### DI Registration & File Serving Endpoints

- [X] T012 Update `ContosoDashboard/Program.cs`: register `builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>()` and `builder.Services.AddScoped<IDocumentService, DocumentService>()` (DocumentService implemented later — register interface now, add implementation stub); add `GET /files/{documentId:int}` minimal API endpoint that requires authorization, calls `IDocumentService.GetDocumentAsync`, calls `IFileStorageService.ResolveAbsolutePath`, and returns `Results.File(absPath, contentType, fileDownloadName: fileName)` with `Content-Disposition: attachment`; add `GET /files/{documentId:int}/preview` minimal API endpoint that does the same but sets `Content-Disposition: inline` (PDF and images only, fall back to download for other types); register both endpoints before `app.MapFallbackToPage("/_Host")`

### Stub Implementation

- [X] T013 Create `ContosoDashboard/Services/DocumentService.cs` as a stub class implementing `IDocumentService` — all methods throw `NotImplementedException` for now; this allows the app to build and run for foundation validation

**Checkpoint**: Run `dotnet run` — app must start, navigate to any existing page without error, and the new DB tables must exist (visible via SQL Server Object Explorer or by checking `ApplicationDbContext` includes new DbSets). Foundation ready.

---

## Phase 3: User Story 1 — Upload a Document (Priority: P1) 🎯 MVP

**Goal**: A logged-in user can navigate to `/documents`, upload a supported file with a title and category, see a progress indicator, receive a success message, and find their document listed on the page.

**Independent Test**: Login as any user → navigate to `/documents` → upload a PDF ≤25 MB with a title and category → verify success message appears → verify document appears in the list with correct title, category, file size, and upload date. Upload a `.exe` → verify rejection. Upload a file without a title → verify validation error.

### Implementation for User Story 1

- [X] T014 [US1] Implement `DocumentService.UploadDocumentAsync()` in `ContosoDashboard/Services/DocumentService.cs`: validate extension against whitelist (`.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`); validate file size ≤ 26,214,400 bytes; if `ProjectId` supplied, verify acting user is project member or PM or Admin (query `ProjectMember` table); generate relative path `{userId}/{projectIdStr}/{Guid.NewGuid()}.{ext}`; call `IFileStorageService.SaveAsync()`; create `Document` entity and save to `ApplicationDbContext`; create `DocumentActivityLog` entry with `Action = "Upload"`; return created `Document`
- [X] T015 [US1] Create `ContosoDashboard/Pages/Documents.razor`: add `@page "/documents"` and `[Authorize]` attribute; inject `IDocumentService`, `AuthenticationStateProvider`; add upload form with `InputFile` (using `@key="inputFileKey"`), title text input (required), description textarea (optional), category dropdown (6 predefined values), optional tags input, optional project association dropdown (populated from user's projects); implement chunk-based `ReadAsync` progress loop extracting `Name/Size/ContentType` from `IBrowserFile` before opening stream, copying to `MemoryStream`, clearing reference and incrementing `inputFileKey` after upload; display `progressPercent` as Bootstrap progress bar during upload; show success alert on completion and error alert on failure; on success call `LoadDocuments()` to refresh the list
- [X] T016 [US1] Add Documents link to `ContosoDashboard/Shared/NavMenu.razor` with Bootstrap Icons `bi-file-earmark-text` icon, pointing to `/documents`
- [X] T017 [US1] Implement `DocumentService.GetRecentDocumentsAsync()` (returns 5 most recent uploads for acting user, `AsNoTracking`, ordered by `UploadedAt DESC`) and wire it into `Documents.razor` to render the initial document list on page load — displays title, category, upload date, file size (formatted as KB/MB), and uploader name

**Checkpoint**: US1 fully testable. Upload works end-to-end: file on disk in `AppData/uploads/`, record in DB, listed on page.

---

## Phase 4: User Story 2 — Browse and Filter My Documents (Priority: P2)

**Goal**: A logged-in user can see all their documents in a sortable, filterable, paginated list and find a specific document by searching across title, description, tags, and uploader name.

**Independent Test**: Login as any user with uploaded documents → open `/documents` → apply category filter → verify only matching documents shown → change sort to "Title A→Z" → verify correct order → enter search term → verify matching results returned → verify pagination controls appear when >50 documents exist (can be tested with seed data).

### Implementation for User Story 2

- [X] T018 [US2] Implement `DocumentService.GetDocumentsAsync()` in `ContosoDashboard/Services/DocumentService.cs`: build `IQueryable<Document>` with `AsNoTracking().Include(d => d.UploadedByUser).Include(d => d.Project)`; scope results to acting user's accessible documents (own + shared + project member); apply `EF.Functions.Like` search across `Title`, `Description`, `Tags`, and `UploadedByUser.DisplayName` when `DocumentQuery.Search` is non-empty; apply category filter, `ProjectId` filter, `DateFrom`/`DateTo` filters when provided; call `CountAsync()` on filtered query before paginating; apply sort (`Title`, `UploadedAt`, `FileSize`, `Category` — default `UploadedAt DESC`); apply `Skip((page-1)*50).Take(50)`; return `PagedResult<DocumentSummary>` with `TotalCount`, `Page`, `PageSize=50`
- [X] T019 [US2] Add browse/filter/search UI to `ContosoDashboard/Pages/Documents.razor`: category filter dropdown, project filter dropdown, date-from/date-to date inputs, search text input (debounced 300 ms), sort-by dropdown with asc/desc toggle; results table showing title, category, upload date, file size, project name; pagination controls (Previous/Next + "Page X of Y") bound to `currentPage` state; trigger `LoadDocuments()` on any filter/sort/page change; show empty-state message ("No documents yet — upload your first document above") when result count is 0

**Checkpoint**: US2 fully testable. Filtering, sorting, search, and pagination all work independently of US1's upload flow.

---

## Phase 5: User Story 3 — Download and Manage Documents (Priority: P3)

**Goal**: A user can download or preview any document they can access. The document owner can edit metadata, replace the file, or permanently delete the document with confirmation.

**Independent Test**: Login → open `/documents` → click Download on an existing document → file downloads with correct filename → click Preview (PDF) → file opens in new browser tab → click Edit → change title → save → verify updated title in list → click Delete → confirm → document removed from list and file deleted from `AppData/uploads/`.

### Implementation for User Story 3

- [X] T020 [US3] Implement `DocumentService.GetDocumentAsync()` in `ContosoDashboard/Services/DocumentService.cs`: load document with navigation properties; check authorization (owner, project member, shared recipient, Team Lead same department, or Admin); throw `UnauthorizedAccessException` if not authorized; `KeyNotFoundException` if not found; log `Action = "Download"` to `DocumentActivityLog`
- [X] T021 [US3] Implement `DocumentService.UpdateDocumentMetadataAsync()` in `ContosoDashboard/Services/DocumentService.cs`: validate acting user is owner; validate title non-empty ≤255 chars; validate category is one of 6 predefined values; update fields; save; log `Action = "Edit"`
- [X] T022 [US3] Implement `DocumentService.DeleteDocumentAsync()` in `ContosoDashboard/Services/DocumentService.cs`: validate acting user is owner, or PM of associated project, or Admin; capture `FilePath` before deletion; delete `Document` record (cascades shares, task links, activity logs); return `FilePath` for caller to delete from storage; caller (`Documents.razor`) calls `IFileStorageService.DeleteAsync(filePath)` after service call
- [X] T023 [US3] Implement `DocumentService.ReplaceDocumentFileAsync()` in `ContosoDashboard/Services/DocumentService.cs`: validate acting user is owner; capture old `FilePath`; update `Document` fields (`FileName`, `FilePath`, `FileSize`, `ContentType`, `UploadedAt = UtcNow`); save; log `Action = "Edit"`; return old `FilePath` for caller to delete from storage
- [X] T024 [US3] Add download link, preview link, edit modal, replace-file panel, and delete confirmation dialog to `ContosoDashboard/Pages/Documents.razor`: download uses `<a href="/files/@doc.DocumentId" download>` anchor; preview (PDF/image) uses `<a href="/files/@doc.DocumentId/preview" target="_blank">`; edit button opens Bootstrap modal with pre-filled title, description, category, tags fields — calls `UpdateDocumentMetadataAsync` on save; replace-file button shows second `InputFile` within edit modal — calls `ReplaceDocumentFileAsync` then `IFileStorageService.DeleteAsync(oldPath)` on success; delete button shows confirmation modal with document title — calls `DeleteDocumentAsync` then `IFileStorageService.DeleteAsync(filePath)` on confirm; edit/delete controls are only rendered when `doc.CanEdit` or `doc.CanDelete` is true

**Checkpoint**: US3 fully testable. Download, preview, edit, replace, and delete all work. IDOR protection verified by attempting download of another user's document via direct URL.

---

## Phase 6: User Story 4 — Project Document Association (Priority: P4)

**Goal**: Project team members see all documents associated with their project on the Project Details page. Users can attach documents to tasks. Documents attached to tasks inherit the task's project association.

**Independent Test**: Login as Project Manager → upload document with project selected → login as team member → open Project Details → verify "Project Documents" section shows the document → login as non-member → attempt `/files/{id}` of a project document → verify 403. Login as any project member → open task detail → attach a document → verify document appears in task's document list.

### Implementation for User Story 4

- [X] T025 [US4] Implement `DocumentService.GetProjectDocumentsAsync()` in `ContosoDashboard/Services/DocumentService.cs`: verify acting user is project member, PM of project, or Admin; return all documents where `ProjectId == projectId`, ordered by `UploadedAt DESC`, with uploader and project navigation loaded; throw `UnauthorizedAccessException` if not authorized
- [X] T026 [US4] Implement `DocumentService.AttachDocumentToTaskAsync()` in `ContosoDashboard/Services/DocumentService.cs`: verify acting user has access to the document; verify acting user has access to the task (assigned user, creator, or project member); check for existing `TaskDocument` record (skip if duplicate); create `TaskDocument` entry; if task has a `ProjectId` and document's `ProjectId` is null, update document's `ProjectId` to match task's project; save changes
- [X] T027 [US4] Implement `DocumentService.GetTaskDocumentsAsync()` in `ContosoDashboard/Services/DocumentService.cs`: verify acting user has access to the task; return all documents linked via `TaskDocument` for the given task, ordered by `TaskDocument.AttachedAt DESC`
- [X] T028 [P] [US4] Add "Project Documents" section to `ContosoDashboard/Pages/ProjectDetails.razor`: inject `IDocumentService`; on `OnInitializedAsync` call `GetProjectDocumentsAsync(projectId, userId)`; render Bootstrap card section "Project Documents" with a table showing title, category, upload date, file size, uploader name; each row has Download and Preview links; show empty-state when no documents; handle `UnauthorizedAccessException` gracefully (hide section)
- [X] T029 [P] [US4] Add document attachment panel to `ContosoDashboard/Pages/Tasks.razor`: inject `IDocumentService`; for the selected/open task call `GetTaskDocumentsAsync(taskId, userId)` and render attached documents list; add "Attach Document" button that opens a modal with a searchable dropdown of the user's accessible documents (call `GetDocumentsAsync` with empty query); on selection call `AttachDocumentToTaskAsync` and refresh the list

**Checkpoint**: US4 fully testable. Project Documents section visible to members, hidden/denied to non-members. Task attachment creates `TaskDocument` record and inherits project association.

---

## Phase 7: User Story 5 — Document Sharing and Notifications (Priority: P5)

**Goal**: A document owner can share their document with specific users. Recipients are notified via in-app notification and can find shared documents in a "Shared with Me" section.

**Independent Test**: Login as Ni Kang → upload a document → share it with Floris Kregel → login as Floris → check Notifications → verify "Document shared with you" notification → navigate to `/documents` → click "Shared with Me" tab → verify Ni Kang's document is listed → click Download → verify download succeeds.

### Implementation for User Story 5

- [X] T030 [US5] Implement `DocumentService.ShareDocumentAsync()` in `ContosoDashboard/Services/DocumentService.cs`: validate acting user is document owner; for each `recipientUserId` in the list, check no existing `DocumentShare` for this pair (skip duplicates); create `DocumentShare` record; call `INotificationService.CreateNotificationAsync()` (or equivalent existing method) to send `NotificationType.DocumentShared` notification to recipient with message "Shared document: {title}" and link to `/documents`; log `Action = "Share"` to `DocumentActivityLog`
- [X] T031 [US5] Implement `DocumentService.GetSharedWithMeAsync()` in `ContosoDashboard/Services/DocumentService.cs`: return all documents where a `DocumentShare` record exists with `SharedWithUserId == actingUserId`, ordered by `SharedAt DESC`, including sharer name and share date in `DocumentSummary`
- [X] T032 [US5] Add "Shared with Me" tab to `ContosoDashboard/Pages/Documents.razor`: add Bootstrap tab navigation with "My Documents" and "Shared with Me" tabs; "Shared with Me" tab calls `GetSharedWithMeAsync` and renders documents table with title, category, sharer name, share date, file size; each row has Download and Preview links (no edit/delete for shared documents); show empty-state "No documents have been shared with you yet" when empty
- [X] T033 [US5] Add "Share" button to document list rows in `ContosoDashboard/Pages/Documents.razor` (visible only when `doc.CanEdit` is true): opens Bootstrap modal with a multi-select user list (all users except owner, loaded from `IUserService.GetAllUsersAsync()`); on confirm calls `ShareDocumentAsync`; shows success toast with recipient names

**Checkpoint**: US5 fully testable. Share flow creates `DocumentShare` record, triggers notification, document appears in recipient's "Shared with Me" tab.

---

## Phase 8: Dashboard Integration (Cross-Cutting)

**Goal**: The dashboard home page shows the 5 most recent documents and a document count card for the logged-in user.

**Independent Test**: Login → navigate to `/` (dashboard) → verify "Recent Documents" widget lists up to 5 documents with title and upload date → verify document count card shows correct number → upload a new document → return to dashboard → verify widget updates.

- [X] T034 Implement `DocumentService.GetAccessibleDocumentCountAsync()` in `ContosoDashboard/Services/DocumentService.cs`: count all documents accessible to acting user (own + shared + project member) using a single `CountAsync()` query
- [X] T035 Add "Recent Documents" widget and document count card to `ContosoDashboard/Pages/Index.razor`: inject `IDocumentService`; on `OnInitializedAsync` call `GetRecentDocumentsAsync` (returns 5 most recent) and `GetAccessibleDocumentCountAsync`; render a Bootstrap card "Recent Documents" with a list of document title + upload date, each linking to `/documents`; add document count to the existing summary cards row alongside tasks/projects/notifications count; handle gracefully if no documents uploaded yet (show empty state in widget)

**Checkpoint**: Dashboard widgets show correct data. Count card increments after upload.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Audit reporting, error handling hardening, and final validation.

- [X] T036 [P] Implement `DocumentService.GetActivityLogsAsync()` in `ContosoDashboard/Services/DocumentService.cs`: throw `UnauthorizedAccessException` if acting user role is not `Administrator`; return all `DocumentActivityLog` records ordered by `OccurredAt DESC`, including acting user display name and document title
- [X] T037 [P] Add path-traversal guard unit test path: verify `LocalFileStorageService.ResolveAbsolutePath()` throws `UnauthorizedAccessException` (or `InvalidOperationException`) when a path containing `..` is resolved outside the storage root
- [X] T038 Validate all 9 quickstart.md test scenarios manually and confirm each passes; document any failures
- [X] T039 Drop and recreate the local database to confirm `EnsureCreated` applies the full schema cleanly from scratch: run `sqllocaldb stop mssqllocaldb && sqllocaldb delete mssqllocaldb` then `dotnet run`; verify all tables created and existing seed data intact

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3–7 (User Stories)**: All depend on Phase 2 completion; can proceed in priority order or in parallel if staffed
- **Phase 8 (Dashboard)**: Depends on Phase 3 (`GetRecentDocumentsAsync` stub) and Phase 2 foundation
- **Phase 9 (Polish)**: Depends on all desired user stories being complete

### User Story Dependencies

| Story | Can start after | Depends on other stories? |
|-------|----------------|---------------------------|
| US1 Upload (P1) | Phase 2 complete | None |
| US2 Browse/Filter (P2) | Phase 2 complete | None (testable with seed data) |
| US3 Download/Manage (P3) | Phase 2 complete | None (testable with seed data) |
| US4 Project/Task Integration (P4) | Phase 2 complete | None (testable independently) |
| US5 Sharing/Notifications (P5) | Phase 2 + US1 (needs uploaded documents) | US1 for meaningful test |
| Dashboard Integration | Phase 2 + US1 | US1 for meaningful data |

### Within Each User Story

- Service method implementation before Razor page wiring
- Models (Phase 2) before any service implementation
- Foundation checkpoint (Phase 2) verified before starting Phase 3

### Parallel Opportunities Per Story

- **Phase 2**: T003, T004, T005, T006 (four model files) run in parallel; T009, T011 (IFileStorageService, IDocumentService interfaces) run in parallel with models
- **Phase 3**: T014 (service) and T015 (page) can start in parallel — T015 calls T014's method but can be scaffolded while T014 is in progress
- **Phase 4**: T025, T026, T027 (three service methods) can be implemented in parallel (different methods, no internal dependencies)
- **Phase 5**: T020, T021, T022, T023 (four service methods) can be implemented in parallel
- **Phase 6**: T028 (`ProjectDetails.razor`) and T029 (`Tasks.razor`) are different files — fully parallel
- **Phase 9**: T036 and T037 are independent — parallel

---

## Parallel Execution Examples

### Phase 2 — Foundation parallelism
```
Parallel group A (models — all different files):
  T003 Document.cs
  T004 DocumentShare.cs
  T005 TaskDocument.cs
  T006 DocumentActivityLog.cs

Parallel group B (interfaces — different files):
  T009 IFileStorageService.cs
  T011 IDocumentService.cs

Sequential after A+B:
  T007 → T008 → T010 → T012 → T013
```

### Phase 6 — US4 project/task integration parallelism
```
Parallel after T025 service methods:
  T028 ProjectDetails.razor changes
  T029 Tasks.razor changes
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational — all T003–T013 (**CRITICAL**)
3. Complete Phase 3: User Story 1 — T014–T017
4. **STOP and VALIDATE**: Upload a document, see it listed, confirm file on disk and DB record
5. Demo / share as MVP

### Incremental Delivery

| Increment | Phases | What users can do |
|-----------|--------|-------------------|
| MVP | 1 + 2 + 3 | Upload documents and see them listed |
| Increment 2 | + 4 | Search, filter, sort, paginate documents |
| Increment 3 | + 5 | Download, preview, edit, delete documents |
| Increment 4 | + 6 | See project documents; attach docs to tasks |
| Increment 5 | + 7 | Share documents; receive notifications |
| Full | + 8 + 9 | Dashboard widgets; audit logs; polish |

---

## Task Count Summary

| Phase | Tasks | Notes |
|-------|-------|-------|
| Phase 1: Setup | 2 | T001–T002 |
| Phase 2: Foundational | 11 | T003–T013 |
| Phase 3: US1 Upload | 4 | T014–T017 |
| Phase 4: US2 Browse/Filter | 2 | T018–T019 |
| Phase 5: US3 Download/Manage | 5 | T020–T024 |
| Phase 6: US4 Project/Task | 5 | T025–T029 |
| Phase 7: US5 Sharing | 4 | T030–T033 |
| Phase 8: Dashboard | 2 | T034–T035 |
| Phase 9: Polish | 4 | T036–T039 |
| **Total** | **39** | |
