# Project Guidelines

## Code Style
- Preserve existing style in each project area rather than reformatting unrelated files.
- Backend uses C# with dependency injection, async methods, and cancellation tokens in service/controller flows.
- Frontend uses TypeScript + React (Next.js App Router) and follows the ESLint config in [client/eslint.config.mjs](client/eslint.config.mjs): single quotes, no semicolons, prefer const.
- Prefer existing API wrappers in [client/src/api](client/src/api) instead of adding ad hoc fetch logic in components.

## Architecture
- This is a full-stack workspace with two deployable apps:
- Backend: [WorthBoards.Api](WorthBoards.Api) (ASP.NET Core host/controllers/configuration).
- Frontend: [client](client) (Next.js UI).
- Backend layering:
- API layer in [WorthBoards.Api](WorthBoards.Api).
- Business logic in [WorthBoards.Business](WorthBoards.Business).
- Data access and EF repositories in [WorthBoards.Data](WorthBoards.Data).
- Domain entities in [WorthBoards.Domain](WorthBoards.Domain).
- Shared enums/constants/exceptions in [WorthBoards.Common](WorthBoards.Common).
- Keep responsibilities in their current layer; avoid moving data logic into controllers or UI logic into API wrappers.

## Build and Test
- Backend restore/build (workspace root):
- `dotnet restore`
- `dotnet build WorthBoards.sln`
- Run API:
- `dotnet run --project WorthBoards.Api/WorthBoards.Api.csproj`
- Frontend:
- `cd client`
- `npm install`
- `npm run dev`
- Integration tests:
- `dotnet test WorthBoards.IntegrationTests/WorthBoards.IntegrationTests.csproj`
- API-scoped coverage:
- `dotnet test WorthBoards.IntegrationTests/WorthBoards.IntegrationTests.csproj --settings WorthBoards.IntegrationTests/coverage.runsettings --collect:"XPlat Code Coverage"`

## Conventions
- Reuse existing integration test helpers and factory in [WorthBoards.IntegrationTests/Experiment1](WorthBoards.IntegrationTests/Experiment1) when adding API integration tests.
- Keep integration tests scenario-oriented with happy and negative flows.
- For auth/identity-dependent integration tests, set required JWT settings in test host (already handled in test factory).
- Prefer small, targeted changes and avoid broad refactors unless explicitly requested.

## Environment Gotchas
- API startup requires JWT configuration values (`WBJwtKey`, `WBIssuer`, `WBAudience`). Missing values can cause startup failure.
- Backend expects PostgreSQL settings in normal runtime; test infrastructure uses an in-memory DB override.
- Frontend assumes local API base URL from [client/src/constants/api.ts](client/src/constants/api.ts) and runs on localhost port 3000.

## References
- Root project setup notes: [README.md](README.md)
- Frontend default Next.js notes: [client/README.md](client/README.md)
