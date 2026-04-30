# Data Model: Document Upload and Management

**Feature**: `001-document-upload-management`  
**Phase**: 1 — Design  
**Date**: 2026-04-30

---

## New Entities

### Document

Central entity representing an uploaded file and its metadata.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentId` | `int` | PK, auto-increment | Integer for consistency with User, Project, TaskItem |
| `Title` | `string` | Required, MaxLength(255) | User-supplied display name |
| `Description` | `string?` | MaxLength(2000) | Optional longer description |
| `Category` | `string` | Required, MaxLength(100) | Text value from predefined list (see below) |
| `Tags` | `string?` | MaxLength(500) | Comma-separated (e.g., `"budget,Q1,draft"`) |
| `FileName` | `string` | Required, MaxLength(255) | Original file name (for download Content-Disposition) |
| `FilePath` | `string` | Required, MaxLength(500) | GUID-based relative path: `{userId}/{projectId or "personal"}/{guid}.{ext}` |
| `FileSize` | `long` | Required | File size in bytes |
| `ContentType` | `string` | Required, MaxLength(255) | MIME type (e.g., `application/pdf`; 255 chars for Office types) |
| `UploadedAt` | `DateTime` | Required, default UtcNow | Upload timestamp (UTC) |
| `UploadedByUserId` | `int` | Required, FK → User | The uploader |
| `ProjectId` | `int?` | Optional, FK → Project | If associated with a project |

**Category values (predefined)**: `Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`, `Other`

**Navigation properties**:
- `UploadedByUser` → `User`
- `Project` → `Project?`
- `Shares` → `ICollection<DocumentShare>`
- `TaskDocuments` → `ICollection<TaskDocument>`
- `ActivityLogs` → `ICollection<DocumentActivityLog>`

**EF Core index**: `UploadedByUserId` (for "My Documents" query); `ProjectId` (for project documents query)

---

### DocumentShare

Join entity recording a sharing relationship between a document and a recipient user.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentShareId` | `int` | PK, auto-increment | |
| `DocumentId` | `int` | Required, FK → Document | |
| `SharedWithUserId` | `int` | Required, FK → User | Recipient |
| `SharedByUserId` | `int` | Required, FK → User | Owner who initiated the share |
| `SharedAt` | `DateTime` | Required, default UtcNow | Share timestamp (UTC) |

**Navigation properties**:
- `Document` → `Document`
- `SharedWithUser` → `User`
- `SharedByUser` → `User`

**Unique constraint**: (`DocumentId`, `SharedWithUserId`) — prevent duplicate shares to the same recipient

---

### TaskDocument

Many-to-many join entity linking documents to tasks.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `TaskDocumentId` | `int` | PK, auto-increment | |
| `TaskId` | `int` | Required, FK → TaskItem | |
| `DocumentId` | `int` | Required, FK → Document | |
| `AttachedAt` | `DateTime` | Required, default UtcNow | When the attachment was made |
| `AttachedByUserId` | `int` | Required, FK → User | Who attached it |

**Unique constraint**: (`TaskId`, `DocumentId`) — prevent duplicate attachments

---

### DocumentActivityLog

Audit log entry for document operations.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentActivityLogId` | `int` | PK, auto-increment | |
| `DocumentId` | `int` | Required, FK → Document | |
| `ActingUserId` | `int` | Required, FK → User | User who performed the action |
| `Action` | `string` | Required, MaxLength(50) | `Upload`, `Download`, `Edit`, `Delete`, `Share` |
| `OccurredAt` | `DateTime` | Required, default UtcNow | UTC timestamp |
| `Details` | `string?` | MaxLength(500) | Optional context (e.g., shared with user name) |

**EF Core index**: `DocumentId`, `OccurredAt DESC` (for audit report queries)

---

## NotificationType Enum Extension

Extend the existing `NotificationType` enum in `Notification.cs` with:

```
DocumentShared,          // A document was shared with the user
ProjectDocumentAdded     // A new document was added to a project the user belongs to
```

---

## Existing Entity Changes

### User
No structural changes. `Department` field (existing) is used to scope Team Lead document access.

### TaskItem
No structural changes. `TaskDocument` join entity references `TaskItem.TaskId`.

### ApplicationDbContext
Add `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<TaskDocument>`, `DbSet<DocumentActivityLog>`.  
Configure unique indexes and relationship cascades (see below).

---

## Relationships Summary

```
User (1) ──────────────── (N) Document            [UploadedByUserId]
Project (1) ────────────── (N) Document            [ProjectId, optional]
Document (1) ──────────── (N) DocumentShare        [DocumentId]
User (1) ──────────────── (N) DocumentShare        [SharedWithUserId]
User (1) ──────────────── (N) DocumentShare        [SharedByUserId]
Document (N) ────────────── (N) TaskItem           [via TaskDocument]
Document (1) ──────────── (N) DocumentActivityLog  [DocumentId]
User (1) ──────────────── (N) DocumentActivityLog  [ActingUserId]
```

---

## Cascade Delete Rules

| Relationship | On Delete |
|---|---|
| User deleted → Documents | Restrict (documents must be re-assigned or deleted first) |
| Project deleted → Documents | Set Null (documents remain, `ProjectId` becomes null) |
| Document deleted → DocumentShares | Cascade |
| Document deleted → TaskDocuments | Cascade |
| Document deleted → DocumentActivityLogs | Cascade |

---

## Validation Rules

- `Title`: Required, 1–255 characters, trimmed.
- `Category`: Must be one of the 6 predefined values.
- `FileSize`: Must be > 0 and ≤ 26,214,400 bytes (25 MB).
- `ContentType`: Must match the extension whitelist: PDF → `application/pdf`; DOCX → `application/vnd.openxmlformats-officedocument.wordprocessingml.document`; etc.
- Extension whitelist: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.jpg`, `.jpeg`, `.png`
- `FilePath`: Must be set before the database record is created (generated server-side, never from user input).

---

## State Transitions

Document has no formal lifecycle state, but the following transitions are relevant:

```
[File selected] → [Validated] → [Saved to disk] → [DB record created] → [Listed]
                                      ↓
                              [Disk write failed] → [No DB record created] (safe abort)
```

File replacement:
```
[New file validated] → [New file saved to disk] → [Old file deleted] → [DB record updated]
```

Document deletion:
```
[Confirmed] → [DB record deleted (cascades shares, task links, logs)] → [File deleted from disk]
```
