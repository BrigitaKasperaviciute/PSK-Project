# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WorthBoards is a task/board management app with an **ASP.NET Core 8.0 Web API** backend (`WorthBoards.*` projects) and a **Next.js** frontend (`client/`). Each runs independently.

## Commands

### Backend (API)

```bash
# Run the API (from repo root)
cd WorthBoards.Api && dotnet run

# Build entire solution
dotnet build WorthBoards.sln

# Run all tests
dotnet test WorthBoards.sln

# Run a specific test class
dotnet test WorthBoards.IntegrationTests --filter "ClassName=BoardControllerTests"

# Run a single test method
dotnet test WorthBoards.IntegrationTests --filter "FullyQualifiedName~GetBoardById_ReturnsOk"

# Apply EF Core migrations (from repo root)
dotnet ef database update --project WorthBoards.Data --startup-project WorthBoards.Api
```

### Frontend

```bash
cd client
npm install
npm run dev   # runs on port 3000
```

## Required Configuration

The API **will call `Environment.Exit(1)` at startup** if these keys are missing from configuration (`appsettings.json`, user secrets, or environment variables):

- `WBJwtKey` — JWT signing key (≥32 chars for HMAC-SHA256)
- `WBIssuer` — JWT issuer string
- `WBAudience` — JWT audience string

Database connection comes from `ConnectionStrings:WorthBoardsConnection` in `appsettings.json`, or from the `DATABASE_URL` environment variable (takes priority).

API runs on `http://localhost:5000` (configured via `BaseUrl` in `appsettings.json`).

## Architecture

### Backend Layer Structure

```
Controllers (WorthBoards.Api)
    → Service interfaces (WorthBoards.Business/Services/Interfaces/)
        → Implementations (WorthBoards.Business/Services/)
            → IUnitOfWork (WorthBoards.Data/Repositories/)
                → ApplicationDbContext (EF Core + PostgreSQL)
```

Domain entities live in `WorthBoards.Domain/Entities/`. Common exceptions, enums, and constants are in `WorthBoards.Common/`.

### Authorization System

The custom role system is the most non-obvious part of the codebase:

- `[AuthorizeRole(UserRoleEnum.OWNER/EDITOR/VIEWER)]` — defined in `WorthBoards.Api/Utils/AuthorizeRoleAttribute.cs`. Each role maps to an authorization policy (e.g., `"OWNER"`).
- `PermissionHandler` (`WorthBoards.Api/Utils/PermissionHandler.cs`) handles these policies. It extracts `boardId` from route values by finding the **first route key whose name ends with `"id"`** (case-insensitive), then calls `IBoardService.GetUserRoleByBoardIdAndUserIdAsync()` to check the user's role on that board.
- Roles are ordered: `OWNER(0) < EDITOR(1) < VIEWER(2)`. A user with role ≤ the required role passes. An OWNER can access VIEWER-protected routes.
- Plain `[Authorize]` (no role) uses standard JWT auth only — `PermissionHandler` is not invoked.
- JWT claims include: `NameIdentifier` (userId as int), `Name` (username), `Email`.

### Exception Handling

`GlobalExceptionHandler` (`WorthBoards.Api/Utils/ExceptionHandler/`) maps custom exceptions to HTTP status codes:
- `NotFoundException` → 404
- `BadRequestException` → 400
- `UnauthorizedException` → 401
- `OptimisticLockException` → 409
- Everything else → 500

Throw these from the service layer; controllers do not catch them (except `AuthController.LoginUserAsync` which has a local try/catch).

### Concurrency

Entities with a `Version` (uint) field use optimistic locking. `IUnitOfWork.EnsureConcurrencyTokenMatch()` compares the incoming version with the stored one and throws `OptimisticLockException` on mismatch. Update/Patch request DTOs include a `Version` field.

### Key Patterns

- **UnitOfWork**: all repositories accessed via `IUnitOfWork` in services. Call `SaveChangesAsync()` after mutations.
- **AutoMapper**: all entity↔DTO mappings in `WorthBoards.Business/AutoMapper/MappingProfile.cs`.
- **Scrutor decoration**: `IEmailService` is wrapped with `EmailLoggingDecorator` via `services.Decorate<IEmailService, EmailLoggingDecorator>()` in `AddBusinessServices`.
- **Notification side-effects**: `BoardTaskController`, `BoardOnUserController`, and `TaskOnUserController` call `INotificationService` methods after their primary operations (task created, user invited/removed, task assigned, status changed).
- **Static images**: served from a physical directory configured in `WorthBoards.Data/DependencyInjection.cs:ConfigureStaticImages()`. `ImageFiles` constants (in `WorthBoards.Common`) define the path and request path.

### Integration Tests

The `WorthBoards.IntegrationTests` project uses `WebApplicationFactory<Program>` with all business service interfaces replaced by Moq mocks. The `PermissionHandler` runs against the mocked `IBoardService`, so tests for role-restricted endpoints must set up `GetUserRoleByBoardIdAndUserIdAsync` to return the appropriate `UserRoleEnum?`. Provide JWT test config via `ConfigureAppConfiguration` in the factory to avoid the startup `Environment.Exit(1)` guard.