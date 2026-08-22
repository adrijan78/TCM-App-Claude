---
name: tcm-backend-slice
description: The recipe for adding a backend vertical slice to the TCM API — DTO, repository, service, controller, DI registration and tests, in that order, with the naming, ApiResponse envelope, validation and authorization conventions this codebase uses. Use whenever adding or extending an API endpoint or domain area.
---

# Adding a backend slice

Layering from `SPEC.md` section 3.1 is strict: **Controller → Service → Repository → EF Core → MSSQL**. Controllers never see `DbContext`. Services never return entities. Repositories hold no business rules.

## Build order

### 1. DTOs — `TCM.Application/Dtos/`

One record or class per direction. Never expose an entity. Existing names from the spec: `ApiResponse`, `BeltDto`, `ClubNumbersInfoDto`, `EditTrainingDto`, `ForgotPasswordDto`, `LoginMemberDto`, `MemberDto`, `MemberRegisterDto`, `MemberRoleDto`, `MemberTokenDto`, `MemberTrainingDto`, `NoteDto`, `PaymentsDto`, `PhotoDto`, `ResetPasswordDto`, `RoleDto`, `TrainingDetailsDto`, `TrainingDto`. Extend this vocabulary rather than inventing a parallel one.

### 2. Repository — `TCM.Infrastructure/Repositories/`

Start from the generic `IRepository<T>` (`GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Remove`, `SaveChangesAsync`). Add a specific repository only for queries the generic one cannot express — filtered lists, includes, aggregates:

```csharp
public interface ITrainingRepository : IRepository<Training>
{
    Task<IReadOnlyList<Training>> GetForClubAsync(int clubId, int? year, TrainingStatus? status, CancellationToken ct);
}
```

Rules: `AsNoTracking()` on every read-only query. Project to what you need instead of loading whole graphs. Filter in SQL, never in memory — no `ToListAsync()` followed by `.Where(...)`.

### 3. Service — `TCM.Application/Services/`

Interface plus implementation. This is where validation, authorization and mapping live.

```csharp
public async Task<ApiResponse<TrainingDto>> GetAsync(int id, string callerId, bool isCoach, CancellationToken ct)
{
    var training = await _repo.GetByIdAsync(id, ct);
    if (training is null) return ApiResponse<TrainingDto>.Fail("Training not found.");
    if (!isCoach && !training.Attendances.Any(a => a.MemberId == callerId))
        return ApiResponse<TrainingDto>.Fail("Not permitted.");
    return ApiResponse<TrainingDto>.Ok(training.ToDto());
}
```

Ownership checks belong here, not in the controller — see `tcm-auth`. Return failures as an unsuccessful `ApiResponse<T>` with a message safe to show a user; save exceptions for genuinely exceptional cases and let the error middleware turn those into a 500 with no stack trace.

### 4. Controller — `TCM.Api/Controllers/`

Thin. Derives from `BaseController`. Reads identity from `User`, calls one service method, returns the result.

```csharp
[HttpGet("{id:int}")]
[Authorize]
public async Task<ActionResult<ApiResponse<TrainingDto>>> Get(int id, CancellationToken ct)
    => HandleResult(await _trainingService.GetAsync(id, CallerId, IsCoach, ct));
```

Route convention `api/[controller]`. Coach-only actions carry `[Authorize(Roles = "Coach")]`. Every action returns `ApiResponse<T>`.

### 5. Register in DI

`services.AddScoped<ITrainingService, TrainingService>();` and the repository alongside it. Group registrations by layer in extension methods (`AddApplication()`, `AddInfrastructure()`) rather than piling into `Program.cs`.

### 6. Tests

Service tests with a faked repository for the business rules; a `WebApplicationFactory` integration test per endpoint for the authorization outcome. See `tcm-testing`.

## Finish

```bash
dotnet build && dotnet test
```

Report the exact routes you added, with their method, path and required role.

## Tooling

`context7` and `microsoft-docs` for EF Core and ASP.NET Core APIs on the pinned version — never guess a signature. `csharp-lsp` for references and rename safety.
