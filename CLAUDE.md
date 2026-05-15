# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Backend** (ASP.NET Core 8, port 5000):
```powershell
cd WorthBoards.Api
dotnet run
```

**Frontend** (Next.js 15, port 3000):
```powershell
cd client
npm install
npm run dev
```

**Run all backend tests:**
```powershell
dotnet test
```

**Run a single test class:**
```powershell
dotnet test --filter "FullyQualifiedName~BoardControllerTests"
```

**Run EF Core migrations:**
```powershell
cd WorthBoards.Data
dotnet ef migrations add <MigrationName> --startup-project ..\WorthBoards.Api
dotnet ef database update --startup-project ..\WorthBoards.Api
```

## Architecture

Five C# projects in `WorthBoards.sln` with unidirectional dependencies:

```
WorthBoards.Api → WorthBoards.Business → WorthBoards.Data
                                       → WorthBoards.Domain
                ↘ WorthBoards.Common ↙
```

- **Api** – Controllers, middleware, JWT/authorization config, Serilog config, static-file serving. Entry point is `Program.cs` (top-level statements). Uses `AddNewtonsoftJson()` so controller responses are serialized with Newtonsoft.Json (camelCase by default).
- **Business** – Service interfaces and implementations, AutoMapper profiles, FluentValidation validators, email service (strategy + decorator pattern via Scrutor).
- **Data** – `ApplicationDbContext` (extends `IdentityDbContext`), EF Core migrations, generic `BaseRepository<T>`, `UnitOfWork`, entity linker/config.
- **Domain** – POCO entities only (`Board`, `BoardTask`, `Comment`, `Notification`, `NotificationOnUser`, `BoardOnUser`, `TaskOnUser`).
- **Common** – Custom exceptions (`BadRequestException` → 400, `NotFoundException` → 404, `UnauthorizedException` → 401), enums, constants.

## Authorization model

`[AuthorizeRole(UserRoleEnum.X)]` maps to a named policy handled by `PermissionHandler`. The handler reads `boardId` from route values by finding the **first** route key that ends with "id" (case-insensitive) — this resolves correctly for all current routes because `boardId` appears before other `*id` parameters in every route template. The handler then looks up `BoardOnUsers` for the current user and calls `context.Succeed()` only when `userRole <= requiredRole` (OWNER=0 ≤ EDITOR=1 ≤ VIEWER=2). Standard `[Authorize]` uses JWT claims only.

## Key configuration values

Required at startup — app calls `Environment.Exit(1)` if missing:
- `WBJwtKey`, `WBIssuer`, `WBAudience` — JWT signing
- `BaseUrl` — Kestrel bind URL
- `FrontendBaseUrl` — used in email reset links

Connection string resolved in priority order: `DATABASE_URL` env var → `ConnectionStrings:WorthBoardsConnection`.

Optional:
- `Logger:UseHttpLoggingMiddleware` / `Logger:UseControllerLoggingActionFilter` — Serilog per-request logs
- `Email:Provider` (`Gmail`/`Outlook`), `GmailOptions` section

## Integration tests

Tests are in `WorthBoards.IntegrationTests/` and use `WebApplicationFactory<Program>` backed by a Testcontainers PostgreSQL instance. Docker must be running. Only `IEmailService` (third-party SMTP) is mocked; all other application code runs real.

Each test class implements `IAsyncLifetime` and deletes all rows in FK-safe order in `InitializeAsync` to ensure isolation. Tests generate real JWT tokens via `JwtTokenHelper` using the same key injected into the factory. Seed data is created through `DatabaseSeeder` which uses `UserManager<ApplicationUser>` and direct `ApplicationDbContext` access.

**Test deserialization**: `ReadFromJsonAsync<T>()` uses `JsonSerializerOptions.Web` (case-insensitive, camelCase) in .NET 8, so it correctly reads Newtonsoft-serialized responses from the API even though property names differ in casing.

**Optimistic concurrency**: Entities with a `Version` (`uint`) property use PostgreSQL `xmin` for concurrency. Tests that update or patch an entity must first fetch the entity to obtain the current version, then include it in the request body.

**Notification invitation flow**: `NotifyBoardInvitation` creates a `Notification` with type `INVITATION` and a `NotificationOnUser` link directly to the invitee. `AcceptInvitation` (POST `/api/notifications/{id}/accept`) then creates the `BoardOnUser` entry and removes the notification.
