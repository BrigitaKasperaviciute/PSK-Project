# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

### Backend
```bash
dotnet run --project WorthBoards.Api          # API on port 5000
dotnet build WorthBoards.sln
dotnet test WorthBoards.IntegrationTests      # all integration tests
dotnet test --filter "FullyQualifiedName~BoardControllerTests"  # single test class
dotnet test --filter "Method_Scenario~Create"  # tests matching name pattern
```

### Frontend
```bash
cd client
npm run dev     # dev server on port 3000 (Turbopack)
npm run build
npm run lint
```

## Architecture

Layered .NET 8 + Next.js 15 monorepo called **WorthBoards** (Kanban board management).

### Backend layers (bottom → top)

| Project | Role |
|---|---|
| `WorthBoards.Domain` | Pure entities (`Board`, `BoardTask`, `Comment`, `Notification`, etc.) with EF annotations |
| `WorthBoards.Common` | Enums (`UserRoleEnum`, `TaskStatusEnum`), constants, exceptions |
| `WorthBoards.Data` | EF Core `ApplicationDbContext`, ASP.NET Identity, generic `Repository<T>`, Unit of Work |
| `WorthBoards.Business` | Services, DTOs, FluentValidation validators, AutoMapper profiles, email/token utilities |
| `WorthBoards.Api` | ASP.NET Core 8 controllers, JWT auth, `AuthorizeRole` attribute, global exception handler |

### Authorization model

`AuthorizeRole(UserRoleEnum.X)` maps to an ASP.NET policy. `PermissionHandler` resolves it at runtime by querying `BoardsOnUsers` for the authenticated user's role on the `boardId` route parameter. Roles: `OWNER=0`, `EDITOR=1`, `VIEWER=2` — lower value = higher privilege; check is `userRole <= requiredRole`.

### Key entities

- `Board` ↔ `BoardOnUser` (composite PK: `BoardId`+`UserId`) — joins users to boards with `UserRoleEnum`
- `BoardTask` — belongs to a board; status: `PENDING`, `IN_PROGRESS`, `COMPLETED`, `ARCHIVED`
- `Comment` — belongs to a `BoardTask` and a user
- `Notification` / `NotificationOnUser` — event-driven notifications routed to board members

### Configuration

JWT settings (`WBJwtKey`, `WBIssuer`, `WBAudience`) must be present as environment variables or in `appsettings.json` — missing values cause `Environment.Exit(1)`. Database reads from `DATABASE_URL` env var first, then `ConnectionStrings:WorthBoardsConnection`. Email and file services are third-party integrations; always mock these in tests.

### Frontend

Next.js 15 App Router in `client/src/app/`. API calls in `client/src/api/`. Components in `client/src/components/pages/`.

## Integration Tests

Project: `WorthBoards.IntegrationTests` (xUnit + FluentAssertions + SQLite in-memory)

### Infrastructure

- `CustomWebApplicationFactory` — uses SQLite in-memory (`SqliteConnection` shared across the factory lifetime), replaces `IEmailService` with `FakeEmailService` and `IFileService` with `FakeFileService`, sets JWT env vars (`WBJwtKey`, `WBIssuer`, `WBAudience`) in a static constructor before the host builds.
- `SqliteCompatibleModelCustomizer` — removes `ValueGeneratedOnAddOrUpdate` from `uint` Version columns (PostgreSQL `xmin` not supported in SQLite) while keeping `IsConcurrencyToken = true`.
- `IntegrationTestBase` — abstract base with helpers: `RegisterAndLoginAsync`, `CreateBoardAsync`, `CreateTaskAsync`, `CreateCommentAsync`, `LinkUserToBoardDirectlyAsync`, `SetAuthToken`, `ClearAuthToken`.
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `AssemblySetup.cs`.
- `HttpClient` must use `BaseAddress = new Uri("https://localhost")` to avoid HTTPS redirect loops.
- Each test class clears all tables in `InitializeAsync` for full isolation.

### SQLite compatibility

`BoardOnUserRepository.GetUsersByUserNameAsync` uses `EF.Functions.Like(u.UserName!.ToLower(), ...)` instead of `EF.Functions.ILike` (PostgreSQL-only function not supported by SQLite).

### Program visibility

`public partial class Program {}` is appended at the end of `WorthBoards.Api/Program.cs` to allow `WebApplicationFactory<Program>` to reference it from the external test assembly.

### Test conventions

- One file per controller in `Controllers/`
- Naming: `Method_Scenario_ExpectedResult` (e.g., `CreateBoard_WithValidRequest_ReturnsCreatedAndPersistsBoard`)
- Every endpoint has at least one happy path + one negative (401/403/404) test pair
- Each test is independent; use `RegisterAndLoginAsync()` without a suffix to create unique GUID-based users per test
- Use `LinkUserToBoardDirectlyAsync` for role setup, bypassing the invitation flow
