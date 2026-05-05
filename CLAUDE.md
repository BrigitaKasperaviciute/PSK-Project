# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Run API:** From `WorthBoards.Api/`, run `dotnet run`. Listens on `http://localhost:5000`.

**Run frontend:** From `client/`, run `npm install && npm run dev`. Runs on port 3000.

**Build solution:** `dotnet build`

**Run all tests:** `dotnet test`

**Run single test:** `dotnet test --filter "FullyQualifiedName~TestMethodName"`

**Apply DB migrations:** From solution root, run `dotnet ef database update --project WorthBoards.Data --startup-project WorthBoards.Api`

## Architecture

Five-layer clean architecture, all .NET 8.0:

- **WorthBoards.Api** — ASP.NET Core Web API. Controllers, middleware, JWT auth/authz configuration, Serilog logging, FluentValidation wiring, CORS, Swagger.
- **WorthBoards.Business** — Service layer. AutoMapper profiles, FluentValidation validators, email strategy/decorator pattern, JWT token generation.
- **WorthBoards.Data** — EF Core 8 + Npgsql. `ApplicationDbContext` (extends `IdentityDbContext<ApplicationUser, ApplicationRole, int>`), typed repository implementations, `UnitOfWork`.
- **WorthBoards.Domain** — Entity classes: `Board`, `BoardTask`, `Comment`, `Notification`, `NotificationOnUser`, `BoardOnUser`, `TaskOnUser`.
- **WorthBoards.Common** — Shared enums (`UserRoleEnum`, `TaskStatusEnum`, `NotificationEventTypeEnum`), exception hierarchy, string constants.

## Key Patterns

**Authentication:** JWT Bearer. Config keys `WBJwtKey`, `WBIssuer`, `WBAudience` are read at startup from `IConfiguration` (env vars or appsettings). If any are missing, `Environment.Exit(1)` is called — set them before the host builds. Tokens use zero `ClockSkew` and `RequireExpirationTime = true`.

**Authorization:** Custom `PermissionHandler` resolves the first route param whose key ends with `"id"` as the board ID, then calls `IBoardService.GetUserRoleByBoardIdAndUserIdAsync`. Role hierarchy: OWNER=0 (highest), EDITOR=1, VIEWER=2 (lowest). The check is `userRole <= requiredRole`. Apply with `[AuthorizeRole(UserRoleEnum.EDITOR)]`.

**Repository + UnitOfWork:** All DB access goes through `IUnitOfWork` → typed repositories (e.g., `BoardRepository`, `CommentRepository`). Call `await _unitOfWork.SaveChangesAsync()` once per business operation. `EnsureConcurrencyTokenMatch(currentVersion, incomingVersion, entityName)` throws `OptimisticLockException` on mismatch.

**Concurrency tokens:** `Board`, `BoardOnUser`, `BoardTask`, and `Comment` each have a `uint Version` property configured via `IsRowVersion()` (backed by Postgres `xmin`). Clients must echo the current `Version` back in PUT/PATCH request bodies.

**Global exception handling:** `GlobalExceptionHandler` catches `BaseException` subclasses and maps them to `ErrorDetails { StatusCode, Title, Details }` JSON. Unhandled exceptions return 500.

**Email service:** `IEmailService` → `EmailContextService` (Gmail/Outlook strategy) → decorated by `EmailLoggingDecorator` via Scrutor's `services.Decorate<>()`. Replace `IEmailService` in tests with a no-op fake (remove all descriptors before re-registering).

**Board creation side-effect:** `CreateBoardAsync` automatically inserts a `BoardOnUser` row with `UserRoleEnum.OWNER` for the creating user.

**Notifications:** Created automatically on task creation, status changes, user assignments, and board invitations. `NotifyBoardInvitation` rejects invitations with `UserRoleEnum.OWNER` role.

## Integration Tests

Tests live in `WorthBoards.IntegrationTests/`. Run with `dotnet test`.

**Infrastructure:**
- `CustomWebApplicationFactory` — replaces PostgreSQL with SQLite in-memory (shared `SqliteConnection`), replaces `IEmailService` with `FakeEmailService` and `IFileService` with `FakeFileService`. JWT env vars (`WBJwtKey`, `WBIssuer`, `WBAudience`) are set in the static constructor so they exist before the host builds.
- `SqliteCompatibleModelCustomizer` — strips `ValueGeneratedOnAddOrUpdate` from `uint` concurrency-token columns (Postgres `xmin`/rowversion is not supported by SQLite).
- `IntegrationTestBase` — abstract base providing `RegisterAndLoginAsync`, `CreateBoardAsync`, `CreateTaskAsync`, `CreateCommentAsync`, `LinkUserToBoardDirectlyAsync`, `SetAuthToken`, `ClearAuthToken`, and `ClearDatabaseAsync`.
- Test parallelization is disabled via `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
- `HttpClient` uses `BaseAddress = new Uri("https://localhost")` and `AllowAutoRedirect = false` to avoid the HTTPS-redirect loop.
- Each test class clears all tables in `InitializeAsync` using `ExecuteDeleteAsync` in FK-safe order.

**Gotchas:**
- `UserUpdateRequest.ImageName` is non-nullable — always pass a non-null string.
- `BoardOnUserRepository.GetUsersByUserNameAsync` uses `EF.Functions.Like` with `.ToLower()` (not `ILike`) for SQLite compatibility.
- `Program` is exposed as `public partial class Program {}` at the bottom of `Program.cs` so the test project can reference it.
