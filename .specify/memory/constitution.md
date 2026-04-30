<!--
  SYNC IMPACT REPORT
  Version change: (template) → 1.0.0
  Modified principles: All — initial population from template placeholders
  Added sections: Core Principles (5), Technology Standards, Development Workflow, Governance
  Removed sections: N/A (first-time fill)
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ aligned (Constitution Check gate references principles by name)
    - .specify/templates/spec-template.md ✅ aligned (no constitution-specific sections required)
    - .specify/templates/tasks-template.md ✅ aligned (task phases reflect clean architecture principle)
  Deferred TODOs: None
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training Clarity (NON-NEGOTIABLE)

All code and architecture decisions MUST prioritize clarity and educational value over production
optimization. Every implementation choice MUST be explainable to a developer learning
Spec-Driven Development. Shortcuts that obscure learning outcomes are prohibited.
Complexity MUST be justified in comments or documentation when it cannot be simplified.

**Rationale**: ContosoDashboard exists solely to teach SDD with the GitHub Spec Kit. Code that
is hard to follow defeats the educational purpose of the repository.

### II. Security-by-Design (Patterns, Not Production)

All features MUST implement correct security patterns — authentication enforcement,
authorization checks, IDOR protection, and defense in depth — even though the auth
system is a training mock. Security concepts demonstrated MUST reflect production
standards; only the identity provider implementation is simplified for offline use.
Pages MUST carry `[Authorize]` attributes. Service methods MUST enforce authorization
independently of the UI layer.

**Rationale**: Students learn security patterns from this codebase. Demonstrating correct
patterns with a simplified implementation teaches the "what" and "why" without
requiring cloud dependencies.

### III. Offline-First with Cloud Migration Path

The application MUST remain fully functional without external network connectivity or
cloud subscriptions. All infrastructure dependencies (database, file storage,
authentication) MUST be abstracted behind interfaces to enable production swap-outs
via dependency injection with zero business-logic changes. The local implementations
(SQL Server LocalDB, filesystem, mock auth) and their intended cloud counterparts
MUST be documented.

**Rationale**: Training sessions are held in varied environments. Offline operation
maximizes availability. Interface abstractions teach the industry-standard pattern for
cloud-ready design.

### IV. Clean Architecture & Separation of Concerns

The codebase MUST maintain strict layering: Models, Data (EF Core DbContext), Services,
and Pages/Shared. Business logic MUST reside in service classes, not in Razor components.
Services MUST be injected via interfaces. Database access MUST only occur through the
service layer (Pages MUST NOT reference `ApplicationDbContext` directly).

**Rationale**: Clean separation demonstrates industry best practices, makes the codebase
testable, and ensures each layer can be taught and modified in isolation.

### V. Spec-Driven Development Workflow

All new features and significant changes MUST follow the GitHub Spec Kit workflow:
Specify → Plan → Tasks → Implement. Feature work MUST begin with a spec in
`specs/[###-feature-name]/spec.md`. Implementation MUST not start before the plan and
task list are approved. Specs MUST include prioritized user stories with independent
acceptance scenarios.

**Rationale**: ContosoDashboard is the training ground for SDD itself. Practicing the
workflow on the training app reinforces the methodology for students.

## Technology Standards

**Runtime**: .NET 8.0 (ASP.NET Core) — MUST NOT be upgraded mid-training without a
documented migration plan.

**UI Framework**: Blazor Server — interactive components MUST use Blazor patterns
(EventCallback, cascading parameters) rather than JavaScript where Blazor equivalents
exist.

**Database**: SQL Server LocalDB with Entity Framework Core — `EnsureCreated()` is
acceptable for training; migrations MUST be used for any schema changes introduced in
feature specs. N+1 queries MUST be prevented via `.Include()` eager loading.

**Styling**: Bootstrap 5.3 with Bootstrap Icons — custom CSS MUST be placed in
`wwwroot/css/site.css`; inline styles are prohibited except for dynamic values
(e.g., progress bar widths).

**Async**: All service methods and database calls MUST use `async`/`await`; synchronous
blocking calls (`.Result`, `.Wait()`) are prohibited.

**Roles**: Employee → TeamLead → ProjectManager → Administrator. New features MUST
respect this hierarchy and document which roles are granted access.

## Development Workflow

New features MUST follow this sequence:

1. **Specify** (`/speckit.specify`): Create `specs/[###-feature-name]/spec.md` with
   prioritized user stories and acceptance scenarios.
2. **Plan** (`/speckit.plan`): Produce `plan.md`, `research.md`, `data-model.md`, and
   API contracts. Constitution Check gate MUST pass.
3. **Tasks** (`/speckit.tasks`): Generate `tasks.md` organized by user story.
4. **Implement** (`/speckit.implement`): Implement tasks in order, one user story at a
   time to enable independent testing.

Code review MUST verify:
- Principles I–V are satisfied.
- No direct DbContext references in Pages.
- `[Authorize]` attribute present on all new protected pages.
- Service-level authorization checks present for all data-access methods.
- New public-facing strings and UI labels are training-appropriate (fictional Contoso data).

Known limitations documented in `README.md` (mock auth, no rate limiting, no audit
logging) MUST NOT be silently "fixed" in feature branches without a corresponding spec
and an update to the Known Limitations section.

## Governance

This constitution supersedes all other development guidelines for ContosoDashboard.
Amendments require:

1. A pull request updating `.specify/memory/constitution.md`.
2. Version bump per semantic versioning:
   - **MAJOR**: Principle removal, redefinition, or backward-incompatible governance change.
   - **MINOR**: New principle or section added, or material expansion of existing guidance.
   - **PATCH**: Clarification, wording improvement, or non-semantic refinement.
3. Update of any affected templates in `.specify/templates/` within the same PR.
4. A one-line summary in the Sync Impact Report comment at the top of this file.

All feature plans MUST include a Constitution Check section referencing the principles
relevant to the feature. Compliance is verified during plan review before implementation
begins. Refer to `README.md` for runtime development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-04-30 | **Last Amended**: 2026-04-30
