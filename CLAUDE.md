# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**WorthBoards** — a Kanban-style board management app. Two separate apps that must be started independently:

| App | Location | Port |
|-----|----------|------|
| Next.js frontend | `client/` | 3000 |
| ASP.NET Core API | `WorthBoards.Api/` | 5000 |

## Commands

### Frontend (`client/`)
```bash
npm run dev      # Dev server with turbopack
npm run build    # Production build
npm run lint     # ESLint
```

### Backend (solution root or `WorthBoards.Api/`)
```bash
dotnet run --project WorthBoards.Api     # Start API
dotnet build                              # Build solution
dotnet clean                              # Clean artifacts
```

### Integration Tests
```bash
dotnet test WorthBoards.IntegrationTests  # Run all integration tests
dotnet test --collect:"XPlat Code Coverage"  # With coverage report
```

## Required Environment Variables (Backend)

The API will exit on startup if these are missing — set them in user secrets or environment:

```
WBJwtKey        JWT signing secret (≥ 32 chars)
WBIssuer        JWT issuer string
WBAudience      JWT audience string
DATABASE_URL    PostgreSQL connection string (overrides appsettings ConnectionStrings:WorthBoardsConnection)
```

Gmail settings (optional — only needed for email features):
```
Gmail:SenderEmail
Gmail:SenderPassword
```

## Architecture

### Backend Layer Structure

```
WorthBoards.Api/        Controllers, middleware, filters, DI wiring
WorthBoards.Business/   Services, DTOs, validators, AutoMapper profiles
WorthBoards.Data/       EF Core DbContext, repositories, migrations
WorthBoards.Domain/     Entity classes only (no logic)
WorthBoards.Common/     Shared enums, exceptions, constants
```

**Request flow:** Controller → Service (Business) → Repository (Data) → PostgreSQL

### Authorization System

Two attributes are used on controller endpoints:

- `[Authorize]` — requires a valid JWT (any authenticated user)
- `[AuthorizeRole(UserRoleEnum.X)]` — requires the user to have at least role X on the board

`UserRoleEnum` hierarchy (lower value = more privilege): `OWNER(0) < EDITOR(1) < VIEWER(2)`

At runtime, `PermissionHandler` extracts the first route value whose key ends with `"id"` (the `boardId`), then queries `BoardOnUsers` to find the caller's role. If the caller's role ≤ the required role, access is granted. Users not in `BoardOnUsers` get 403.

When a user **creates** a board they are automatically assigned `OWNER`.

### Database

- ORM: EF Core 8 with PostgreSQL (Npgsql)
- Identity: ASP.NET Core Identity (`ApplicationUser`, `ApplicationRole`)
- Optimistic concurrency via `[Timestamp] uint Version` on `Board`, `BoardTask`, `BoardOnUser`, and `Comment` entities
- `EnsureConcurrencyTokenMatch` in `UnitOfWork` throws `OptimisticLockException` (→ 409) if versions mismatch
- Migrations live in `WorthBoards.Data` (`dotnet ef migrations add <Name> --project WorthBoards.Data --startup-project WorthBoards.Api`)

### Frontend

- Next.js App Router (`client/src/app/`)
- `@/*` path alias maps to `client/src/`
- Auth state carried via HTTP-only cookies (JWT); `middleware.ts` protects routes
- API calls in `client/src/api/`; custom hooks in `client/src/hooks/`
- ESLint enforces single quotes and no semicolons

## Integration Tests

Tests use **xunit + SQLite in-memory + `WebApplicationFactory<Program>`**.

`CustomWebApplicationFactory`:
- Sets `WBJwtKey`/`WBIssuer`/`WBAudience` via environment variables before the host boots
- Swaps the PostgreSQL `DbContext` for SQLite (shared in-memory connection)
- Replaces `IEmailService` with a no-op `FakeEmailService`
- Calls `EnsureCreated()` to build the SQLite schema

Each test creates unique users/boards (Guid-based names) to avoid cross-test state pollution.
