# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

WorthBoards is a full-stack task/project collaboration platform built with ASP.NET Core 8 (backend) and Next.js 15 (frontend).

## Development commands

### Backend (from `WorthBoards.Api/`)
```bash
dotnet run       # Start API on port 5000
dotnet build     # Build solution
dotnet test      # Run integration tests
```

### Frontend (from `client/`)
```bash
npm install      # Install dependencies (first time)
npm run dev      # Start dev server with Turbopack on port 3000
npm run build    # Production build
npm run lint     # Run ESLint
```

### Run a single test class
```bash
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
```

### Code coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Architecture

The backend follows N-tier layered architecture with these projects:

- **WorthBoards.Api** — Controllers, middleware, filters, auth/authorization config. Entry point. `Program.cs` wires all layers.
- **WorthBoards.Business** — Services (`IXxxService` → `XxxService`), DTOs (Requests/Responses), AutoMapper profiles, FluentValidation validators. Email uses a decorator+strategy pattern (`EmailLoggingDecorator` wraps `IEmailService`; Gmail/Outlook are strategies).
- **WorthBoards.Data** — EF Core `ApplicationDbContext` (PostgreSQL/Npgsql), generic repository base + specific repos, Unit of Work, ASP.NET Identity (`ApplicationUser`, `ApplicationRole`).
- **WorthBoards.Domain** — Entity classes with navigation properties. Entities carry a `uint Version` concurrency token mapped to PostgreSQL `xmin`.
- **WorthBoards.Common** — Enums (`UserRoleEnum`: OWNER/EDITOR/VIEWER, `TaskStatusEnum`: PENDING/IN_PROGRESS/COMPLETED/ARCHIVED), custom exception types.
- **WorthBoards.IntegrationTests** — xUnit tests using `WebApplicationFactory<Program>` with SQLite in-memory.

## Key patterns

**Board-level authorization**: `[AuthorizeRole(UserRoleEnum.X)]` on controller actions enforces membership. `PermissionHandler` extracts `boardId` from route values, queries the user's role, and compares with `userRole <= requiredRole` (OWNER=0 is most privileged). Standard `[Authorize]` only requires a valid JWT.

**Concurrency control**: All mutable entities have `uint Version`. Clients send the current version with updates; a mismatch throws `DbUpdateConcurrencyException` → `GlobalExceptionHandler` maps it to HTTP 409.

**Error handling**: `GlobalExceptionHandler` maps `NotFoundException → 404`, `UnauthorizedException → 401`, `DbUpdateConcurrencyException → 409`. All error responses use `{ statusCode, title, details }`.

**User ID extraction**: `UserHelper.GetUserId(User)` reads `ClaimTypes.NameIdentifier` from the JWT. JWT also contains `ClaimTypes.Email` (used by `change-password`).

## Integration tests

Tests use `WebApplicationFactory<Program>` (project `WorthBoards.IntegrationTests`) with:

- **SQLite in-memory** replaces PostgreSQL. `SqliteCompatibleModelCustomizer` strips `ValueGeneratedOnAddOrUpdate` from `uint` version columns so SQLite includes them in SQL statements while keeping them as concurrency tokens.
- **FakeEmailService** and **FakeFileService** replace production implementations.
- JWT credentials injected via environment variables `WBJwtKey`, `WBIssuer`, `WBAudience` (also set via `AddInMemoryCollection`).
- Tests run sequentially: `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
- `IntegrationTestBase` provides: `RegisterAndLoginAsync`, `CreateBoardAsync`, `CreateTaskAsync`, `CreateCommentAsync`, `InviteUserAndGetNotificationAsync`, `GetPasswordResetTokenAsync`.

## Required environment variables

| Variable | Purpose |
|---|---|
| `DATABASE_URL` | PostgreSQL connection string (production) |
| `WBJwtKey` | JWT HMAC signing key (≥ 32 chars) |
| `WBIssuer` | JWT issuer string |
| `WBAudience` | JWT audience string |

## Frontend

Next.js App Router (`app/` directory). Per-resource API classes handle HTTP calls. JWT tokens are stored in cookies; middleware protects routes by verifying tokens via the `jose` library.
