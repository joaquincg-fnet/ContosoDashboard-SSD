# ContosoDashboard Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-30

## Active Technologies

- **Runtime**: C# 12 / .NET 8.0, ASP.NET Core 8
- **UI**: Blazor Server (`AddServerSideBlazor`, `MapBlazorHub`, `MapFallbackToPage("/_Host")`)
- **Database**: SQL Server LocalDB, Entity Framework Core 8 (`EnsureCreated`, no migrations)
- **Styling**: Bootstrap 5.3, Bootstrap Icons
- **Auth**: Cookie-based mock authentication, claims-based identity, `[Authorize]` attributes
- **File storage**: Local filesystem (`AppData/uploads/`) via `IFileStorageService` / `LocalFileStorageService`
- **File serving**: Minimal API endpoints in `Program.cs` (`/files/{id}`, `/files/{id}/preview`)

## Project Structure

```text
ContosoDashboard/
├── Data/ApplicationDbContext.cs     # EF Core DbContext — only place DB is accessed
├── Models/                          # Entity classes with Data Annotations
├── Services/                        # Business logic — all interfaces (IXxxService)
├── Pages/                           # Blazor .razor pages + Razor Pages (.cshtml)
├── Shared/                          # Layout, NavMenu, shared components
├── wwwroot/css/site.css             # Custom styles only (no inline styles)
└── Program.cs                       # DI registration + minimal API endpoints

specs/
└── 001-document-upload-management/  # Feature 001 spec artifacts
    ├── spec.md
    ├── plan.md
    ├── research.md
    ├── data-model.md
    ├── quickstart.md
    └── contracts/IDocumentService.md
```

## Commands

```powershell
# Run the application (auto-creates and seeds DB)
cd ContosoDashboard
dotnet run

# Drop and recreate database (use when schema changes cause issues)
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
# Then: dotnet run (recreates automatically)

# Alternative DB drop
dotnet ef database drop --force
```

## Code Style

- **Async/await**: All service methods and DB calls use `async`/`await`. Never `.Result` or `.Wait()`.
- **AsNoTracking**: All read-only EF Core queries use `.AsNoTracking()`.
- **EF.Functions.Like**: Use for search queries (not `.Contains()`) to keep queries sargable.
- **Service authorization**: Every `DocumentService` method checks authorization before data access.
- **Integer PKs**: All entities use `int` primary keys named `{EntityName}Id`.
- **Navigation properties**: Declared as `virtual` for lazy loading compatibility.
- **File paths**: Always GUID-based (`{userId}/{scope}/{guid}.{ext}`); never user-supplied filenames.
- **IBrowserFile**: Extract `Name`, `Size`, `ContentType` before opening stream; copy to `MemoryStream` within the `OnChange` handler; clear reference after copy.

## Recent Changes

### Feature 001 — Document Upload and Management (2026-04-30)
**Added**:
- `Document`, `DocumentShare`, `TaskDocument`, `DocumentActivityLog` models
- `IDocumentService` / `DocumentService` — full document lifecycle with RBAC
- `IFileStorageService` / `LocalFileStorageService` — file storage abstraction
- `Documents.razor` — upload, browse, filter, edit, delete, share UI
- Minimal API endpoints: `GET /files/{id}` (download) and `GET /files/{id}/preview` (inline)
- `NotificationType.DocumentShared`, `NotificationType.ProjectDocumentAdded` enum values
- Project Documents section in `ProjectDetails.razor`
- Task document attachment panel in `Tasks.razor`
- Recent Documents widget + document count card in `Index.razor`

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
