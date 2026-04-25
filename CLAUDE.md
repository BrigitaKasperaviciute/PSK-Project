# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (.NET)
```bash
# Run the API
dotnet run --project WorthBoards.Api

# Build solution
dotnet build

# Run all integration tests
dotnet test WorthBoards.IntegrationTests

# Run a single test class
dotnet test WorthBoards.IntegrationTests --filter "FullyQualifiedName~BoardControllerTests"

# Run a single test method
dotnet test WorthBoards.IntegrationTests --filter "FullyQualifiedName~CreateBoard_ValidRequest_ReturnsCreated"
```

### Frontend (Next.js, run from `client/`)
```bash
npm run dev     # development server on port 3000 (Turbopack)
npm run build   # production build
npm run lint    # ESLint
```

## Architecture

### Solution structure
- **WorthBoards.Api** – ASP.NET Core 8 web API: controllers, middleware, JWT/auth configuration, DI wiring
- **WorthBoards.Business** – Services, DTOs, AutoMapper profiles, FluentValidation validators, email utilities
- **WorthBoards.Data** – EF Core `ApplicationDbContext`, ASP.NET Identity (`ApplicationUser`), repository pattern, migrations (Npgsql/PostgreSQL)
- **WorthBoards.Domain** – Entity classes only: `Board`, `BoardTask`, `Comment`, `BoardOnUser`, `TaskOnUser`, `Notification`, `NotificationOnUser`
- **WorthBoards.Common** – Enums (`UserRoleEnum`, `TaskStatusEnum`, `NotificationEventTypeEnum`), exception types, constants
- **WorthBoards.IntegrationTests** – xUnit integration tests using `WebApplicationFactory<Program>` with EF Core InMemory

### Authorization model
`[AuthorizeRole(UserRoleEnum.X)]` (at `WorthBoards.Api/Utils/AuthorizeRoleAttribute.cs`) maps to a named policy handled by `PermissionHandler`. The handler extracts the board ID from route values (finds the first key ending with "id") and queries `BoardOnUsers` for the user's role. Role hierarchy: `OWNER=0 < EDITOR=1 < VIEWER=2` — lower number means higher privilege.

### Notification / invitation flow
1. `POST api/boards/{boardId}/invite` → `NotificationService.NotifyBoardInvitation` creates an `INVITATION` Notification record (no board link yet)
2. `POST api/notifications/{notificationId}/accept` → `NotificationService.AcceptInvitation` creates the `BoardOnUser` record and fires a `USER_ADDED_TO_BOARD` notification

### Email service
Decorator pattern via Scrutor: `EmailLoggingDecorator` wraps `EmailContextService` which delegates to `GmailServiceStrategy` or `OutlookServiceStrategy`. Selected by `Email:Provider` config key. In integration tests, `IEmailService` is replaced with a `Moq.Mock` to avoid SMTP calls.

### Concurrency
Entities with `uint Version` use `IsRowVersion()` (maps to PostgreSQL `xmin`). Update/Patch endpoints call `UnitOfWork.EnsureConcurrencyTokenMatch` and throw `OptimisticLockException` (→ 409) on mismatch. In InMemory tests, `Version` stays at `0`, so always pass `Version = 0` in update request bodies.

### Required configuration (not committed)
Provide via `appsettings.Development.json` or environment variables:
- `WBJwtKey`, `WBIssuer`, `WBAudience` — JWT signing
- `ConnectionStrings:WorthBoardsConnection` or `DATABASE_URL` env var — PostgreSQL
- `Gmail:*` section — email credentials

### Frontend
Next.js 15 App Router under `client/src/app/`. Auth is cookie-based JWT checked in `client/src/middleware.ts`. API client calls live in `client/src/api/`.
