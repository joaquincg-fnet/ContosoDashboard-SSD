# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/001-document-upload-management/spec.md`

## Summary

Enable Contoso employees to upload, organize, search, download, and share work-related documents within ContosoDashboard. Files are stored on the local filesystem outside `wwwroot` using GUID-based paths, served via an authorized minimal API endpoint, and managed through a `DocumentService` that enforces role-based access control and IDOR protection. An `IFileStorageService` abstraction enables future migration to Azure Blob Storage with no business logic changes.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0, ASP.NET Core 8  
**Primary Dependencies**: Blazor Server, Entity Framework Core 8, Bootstrap 5.3, Bootstrap Icons  
**Storage**: SQL Server LocalDB (EF Core, `EnsureCreated`) + local filesystem (`AppData/uploads/`)  
**Testing**: Manual (training project — no automated test framework)  
**Target Platform**: Web (Blazor Server, offline-capable, Windows/macOS/Linux local dev)  
**Project Type**: Single web application (existing `ContosoDashboard/` project)  
**Performance Goals**: Upload ≤30 s for 25 MB; list/search pages ≤2 s; preview open ≤3 s  
**Constraints**: Fully offline (no Azure/cloud), `IFileStorageService` abstraction required, integer PKs on all entities, files stored outside `wwwroot`  
**Scale/Scope**: ≤500 documents per user; 4 seed users; training environment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Requirement | Pre-design | Post-design |
|-----------|-------------|------------|-------------|
| I. Training Clarity | No unjustified complexity; all patterns teachable | ✅ | ✅ — `IFileStorageService`, chunk-based upload, `IQueryable` composition are standard teachable patterns |
| II. Security-by-Design | `[Authorize]` on page; IDOR protection in service; download endpoint enforces auth | ✅ | ✅ — Minimal API endpoint has `.RequireAuthorization()`; `DocumentService` checks ownership/membership before every data access |
| III. Offline-First | No cloud deps; `IFileStorageService` interface abstracts storage | ✅ | ✅ — `LocalFileStorageService` uses `System.IO` only; no Azure SDK |
| IV. Clean Architecture | No DbContext in Pages; business logic in `DocumentService` | ✅ | ✅ — `Documents.razor` injects `IDocumentService` only |
| V. SDD Workflow | spec → plan → tasks → implement | ✅ | ✅ |

**All gates pass. No violations to justify.**

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── IDocumentService.md   ← Phase 1 output
├── checklists/
│   └── requirements.md
└── tasks.md             ← Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (existing single project — files added to `ContosoDashboard/`)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                    ← NEW
│   ├── DocumentShare.cs               ← NEW
│   ├── TaskDocument.cs                ← NEW
│   └── DocumentActivityLog.cs         ← NEW
├── Services/
│   ├── IDocumentService.cs            ← NEW
│   ├── DocumentService.cs             ← NEW
│   ├── IFileStorageService.cs         ← NEW
│   └── LocalFileStorageService.cs     ← NEW
├── Pages/
│   └── Documents.razor                ← NEW
├── Data/
│   └── ApplicationDbContext.cs        ← MODIFIED (4 new DbSets, indexes, cascades)
├── Models/
│   └── Notification.cs                ← MODIFIED (2 new NotificationType enum values)
├── Shared/
│   └── NavMenu.razor                  ← MODIFIED (add Documents nav link)
├── Pages/
│   ├── Index.razor                    ← MODIFIED (recent docs widget + count card)
│   ├── ProjectDetails.razor           ← MODIFIED (project documents section)
│   └── Tasks.razor                    ← MODIFIED (task document attachment panel)
└── Program.cs                         ← MODIFIED (DI registrations + file serving endpoints)

AppData/
└── uploads/                           ← created at runtime (outside wwwroot)
```

**Structure Decision**: Single project (Option 1 equivalent). No new projects or layers added. All new code follows the existing `Models / Services / Pages` layering. The minimal API file-serving endpoints live in `Program.cs` consistent with the existing Blazor Server setup.
