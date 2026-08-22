# CLAUDE.md — Taekwondo Club Management System (TCM)

Working notes for Claude Code on this repository. The requirements contract is [SPEC.md](SPEC.md); the build sequence is [plan.md](plan.md). Read the `tcm-spec` skill before starting any task here.

## Pinned versions

Checked **2026-08-22** against the official .NET release index and the npm registry.

| Component | Version | Why this one |
|---|---|---|
| .NET SDK | **10.0.400** | Current **LTS**, active support until 2028-11-14. Pinned in `global.json` with `rollForward: latestFeature` |
| ASP.NET Core | 10.0 | Ships with the SDK |
| Entity Framework Core | **10.0.11** | Latest stable, matches the .NET 10 line |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.11 | Same line |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-latest` | Local development container |
| Node.js | 24.18.0 | Satisfies Angular 22's engine requirement |
| npm | 11.16.0 | |
| Angular | **22.1.x** | Latest stable (`@angular/core` 22.1.3, `@angular/cli` 22.1.5) |
| Angular Material + CDK | 22.1.3 | Tracks the Angular major |
| TypeScript | 6.0.2 | What Angular 22 pins |
| Bootstrap | 5.3.8 | Spec requires Bootstrap 5, **grid and utilities only** |
| Docker | 27.3.1 | |

## Library decisions made during scaffolding

| Choice | Instead of | Why |
|---|---|---|
| Manual mapping extension methods | AutoMapper | AutoMapper went commercial at v15. Manual mapping is explicit, dependency-free and easier for the LSP to follow |
| xUnit asserts + **NSubstitute** | FluentAssertions | FluentAssertions v8 requires a paid licence for commercial use |
| **Swashbuckle.AspNetCore 10.2.3** | Built-in `AddOpenApi` | Swagger UI's Authorize button is how JWT auth gets exercised by hand, and phases 3–6 lean on it heavily |
| **Serilog.AspNetCore** | Built-in logging | Structured console output plus request logging, for very little cost |
| **FluentValidation** (core only) | DataAnnotations | Validation lives in the service layer here, so the ASP.NET model-binding integration is not needed |
| Bootstrap `bootstrap-grid` + `bootstrap-utilities` | Full Bootstrap CSS | Importing all of Bootstrap drags in button, form and typography resets that fight Angular Material for the same elements |

## Three surprises worth remembering

- **Angular 22 is zoneless** — there is no `zone.js` dependency. Change detection is signal-driven. Do not add `provideZoneChangeDetection`.
- **Angular 22 tests with Vitest + jsdom**, not Karma/Jasmine. Use `vi.fn()`, not `jasmine.createSpy()`. SPEC predates this.
- **Swashbuckle 10 pulls Microsoft.OpenApi v2**, which moved `OpenApiInfo` and friends out of `Microsoft.OpenApi.Models` into `Microsoft.OpenApi`, replaced the `Reference = new OpenApiReference{...}` pattern with `OpenApiSecuritySchemeReference`, and made `AddSecurityRequirement` take a factory. See `Program.cs`.

Superseded during Phase 0: .NET 9.0.316 was installed on this machine but is **STS in maintenance, EOL 2026-11-10**. .NET 10 LTS was installed instead. Older SDKs (5–9) remain side by side; `global.json` makes 10.0.400 the one this repo uses.

Before changing any of these, re-check with `context7` or `microsoft-docs` and update this table with a new date.

## Architecture

Monolith, split into an ASP.NET Core Web API server and an Angular client (SPEC §3).

Server layering is strict — **Controller → Service → Repository → EF Core → MSSQL**:

```
server/
  TCM.Domain/          entities, enums. No dependencies.
  TCM.Application/     service interfaces + implementations, DTOs, validation. Depends on Domain.
  TCM.Infrastructure/  DbContext, EF configurations, migrations, repositories, external services. Depends on Application + Domain.
  TCM.Api/             controllers, middleware, Program.cs, appsettings. Depends on all.
  TCM.Tests/           xUnit.
client/                Angular app (see SPEC §3.3 for the folder contract)
e2e/                   Playwright
```

References point inward only. Controllers never touch `DbContext`. Services never return entities across the API boundary. Repositories hold no business rules.

## Conventions

- Every controller action returns `ApiResponse<T>` and derives from `BaseController`.
- Every service and repository has an interface, registered via `AddApplication()` / `AddInfrastructure()`.
- Authorization is enforced server-side, twice: a role attribute **and** an ownership check in the service layer for member-scoped data. Client guards are UX, not security.
- Async all the way, with `CancellationToken` on I/O.
- Nothing environment-specific in source — no hosts, URLs, keys or connection strings. Configuration and user-secrets only (SPEC §9).
- Members are **deactivated**, never deleted; history depends on the row.
- Only the `data-model-agent` creates EF migrations, so history stays linear.

## Commands

```bash
# database container
docker start tcm-sql

# api
dotnet build
dotnet run --project server/TCM.Api
dotnet test

# migrations
dotnet ef migrations add <Name> -p server/TCM.Infrastructure -s server/TCM.Api
dotnet ef database update      -p server/TCM.Infrastructure -s server/TCM.Api

# client
cd client && npm start
cd client && npm run build
cd client && npm test
```

See the `tcm-run-local` skill for the full configuration key list, seeded accounts, and the first-run failure table.

## Build status

Phases 0–2 are complete: versions pinned, solution scaffolded, schema from SPEC section 4 applied to SQL Server, seeder verified idempotent. Next is Phase 3 (auth). See [plan.md](plan.md).
