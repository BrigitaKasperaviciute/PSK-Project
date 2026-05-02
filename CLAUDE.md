# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture

**WorthBoards** is a task/board management application (Trello-style). The .NET 8 solution has five production projects and one test project:

| Project | Role |
|---|---|
| `WorthBoards.Api` | ASP.NET Core Web API (port 5000) — controllers, auth, filters, middleware |
| `WorthBoards.Business` | Services, DTOs, AutoMapper, FluentValidation, email strategies |
| `WorthBoards.Data` | EF Core + PostgreSQL, repositories, Identity, Unit of Work |
| `WorthBoards.Domain` | Pure entity classes — no dependencies |
| `WorthBoards.Common` | Custom exceptions, enums (`UserRoleEnum`, `TaskStatusEnum`), constants |
| `WorthBoards.IntegrationTests` | xUnit + SQLite integration tests |

The frontend is a separate **Next.js 15 / React 19 / TypeScript** app in `client/` (port 3000).

## Commands

**Backend:**
```bash
cd WorthBoards.Api && dotnet run          # starts API on port 5000
dotnet build                               # builds all projects
dotnet test WorthBoards.IntegrationTests  # run all integration tests
dotnet test --filter "FullyQualifiedName~BoardControllerTests"  # single test class
dotnet test --filter "Method_Scenario_ExpectedResult"           # single test
```

**Frontend:**
```bash
cd client
npm install
npm run dev    # Next.js with Turbopack on port 3000
npm run build
npm run lint   # ESLint
```

**Required environment variables for the API:**
```
WBJwtKey        JWT signing secret (≥32 chars)
WBIssuer        JWT issuer
WBAudience      JWT audience
DATABASE_URL    PostgreSQL connection string (falls back to appsettings.json ConnectionStrings:WorthBoardsConnection)
```

## Key Patterns

### Authorization

`[AuthorizeRole(UserRoleEnum.OWNER|EDITOR|VIEWER)]` maps to `PermissionRequirement` → `PermissionHandler`. The handler queries `BoardOnUser` for the first route value whose key ends with `"id"` (typically `boardId`) and compares the user's role to the policy. Role hierarchy — lower value = more privilege: `OWNER(0) ≤ EDITOR(1) ≤ VIEWER(2)`.

### Exception Handling

`GlobalExceptionHandler` catches all `BaseException` subclasses and maps them to HTTP status codes. The business layer throws:

| Exception | HTTP |
|---|---|
| `BadRequestException` | 400 |
| `NotFoundException` | 404 |
| `UnauthorizedException` | 401 |
| `OptimisticLockException` | 409 |
| `FailedToSendEmailException` | 502 |

### Optimistic Concurrency

`Board`, `BoardTask`, `Comment`, and `BoardOnUser` have a `uint Version` property marked `IsRowVersion()` (maps to PostgreSQL `xmin`). Service methods call `_unitOfWork.EnsureConcurrencyTokenMatch(currentVersion, incomingVersion, entityName)` before saving, which throws `OptimisticLockException(409)` on mismatch. PUT/PATCH requests must include the current `Version`.

### Email

`IEmailService` is registered as `EmailContextService` and then decorated with `EmailLoggingDecorator` (via Scrutor). The context routes to `GmailServiceStrategy` or `OutlookServiceStrategy` based on `Email:Provider` config. **In integration tests, replace `IEmailService` with a no-op fake** to prevent real email calls.

### Unit of Work + Repository Pattern

All data access goes through `IUnitOfWork` which exposes typed repositories. Services call `_unitOfWork.SaveChangesAsync()` to commit transactions.

## Integration Test Infrastructure

Tests are in `WorthBoards.IntegrationTests/Controllers/` — one file per controller.

- `CustomWebApplicationFactory` — swaps PostgreSQL for SQLite (in-memory), replaces `IEmailService` / `IFileService` with fakes, injects JWT test config.
- `SqliteCompatibleModelCustomizer` — strips `ValueGeneratedOnAddOrUpdate` from `uint` version columns (SQLite has no `xmin`).
- `IntegrationTestBase` — abstract base with `RegisterAndLoginAsync()`, `CreateBoardAsync()`, `CreateTaskAsync()`, `CreateCommentAsync()`, `LinkUserToBoardDirectlyAsync()`.
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]` — sequential execution (shared in-memory DB).

Test naming: `Method_Scenario_ExpectedResult`.
