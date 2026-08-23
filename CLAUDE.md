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
| Chart.js | 4.5.1 | Charts for SPEC §6.2 and §6.4. Used directly, not via an Angular wrapper — wrappers pin a peer range against the Angular major and lag a new release |
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

## Version-drift traps already hit (do not rediscover these)

- **Angular 22 is zoneless** — there is no `zone.js` dependency. Change detection is signal-driven. Do not add `provideZoneChangeDetection`.
- **Angular Material 22 dropped `@angular/animations`** from its peer dependencies and animates with CSS. `provideAnimationsAsync()` fails the build on a package that is not installed — do not add it back.
- **`ng test` watches by default.** Use `npm run test:ci` (`ng test --watch=false`) for a single pass; `--run` is not a valid flag.
- **Angular 22 tests with Vitest + jsdom**, not Karma/Jasmine. Use `vi.fn()`, not `jasmine.createSpy()`. SPEC predates this.
- **EF Core 10 cannot translate `DateTimeOffset.Year` / `.Month` in a `GroupBy` key** — on SQL Server *or* SQLite. The dashboard's trainings-per-month chart needs exactly that, so `Training.Date`, `Attendance.Date`, `Payment.PaymentDate` and `Note.CreatedAt` are **UTC `DateTime`**, not `DateTimeOffset`. Store UTC; the club runs in one time zone.
- **EF cannot project straight into a record's constructor from inside a `GroupBy`.** Group into an anonymous type, then map after materialising.
- **`WebApplicationFactory` + minimal hosting:** use `builder.UseSetting(...)`, not `ConfigureAppConfiguration` — `Program.cs` reads configuration while the host is still being built, before those callbacks run.
- **EF Core 9+ registers `IDbContextOptionsConfiguration<T>` separately.** Swapping the provider in tests means removing those descriptors too, or EF refuses to start with two providers.
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

Phases 0–9 are complete. `dotnet build` is clean, **170 endpoint tests pass**, `ng build` is clean and **97 client tests pass**.

- **0–1** versions pinned (.NET 10 LTS, Angular 22), solution scaffolded, client builds.
- **2** schema from SPEC §4 applied, seeder idempotent.
- **3** auth: login, coach-only registration, password reset, JWT validation.
- **4** generic repository, FluentValidation at the service boundary, Common reference slice.
- **5** photos in the database, Stripe behind `Stripe:Enabled`, Gmail SMTP sender.
- **6** all four backend domains — Members, Trainings/Attendance, Payments, Notes — built in parallel, merged, and audited. Five security findings from that audit are fixed.
- **7** Angular foundation: `_models` mirroring every server DTO, typed `AuthService`, JWT and error interceptors, `authGuard`/`coachGuard`, the shell with its two role-dependent menus, lazy routing, and the shared `StatePanel` / `ConfirmDialog` / `ChartComponent`.
- **8** auth screens: login, forgot password, reset password and the coach-only registration form, plus `CommonService` (belts/roles/club numbers, cached per session), `guestGuard`, the shared `AuthCard` / `FormAlert` / `Trim` pieces, and the password-policy and field-match validators. Verified end to end against the running API.

- **8b** the design system: a custom M3 palette, `src/styles/_tokens.scss` as the single source of colour/space/shape/motion, light and dark from one `color-scheme` property, the reworked shell (icon rail, theme toggle, page title), and the shared `PageHeader` / `StatusChip` / `BeltSwatch` / `Skeleton` / `BrandMark` / chart theme. Contrast verified by `npm run check:contrast`; reviewed at 360/768/1024/1440 in both themes.

- **9** every screen of SPEC 6.2–6.8: the club dashboard, the member list and three-tab profile, trainings as table *and* calendar, training details with attendance and scoring, club-wide payments, and club-wide notes — plus the five typed services, `MemberAvatar`, `NoteCard`, `MembershipBanner`, `StatCard` and the Stripe return landing. Reviewed against real seeded data at 1440 and 390 in both themes, with no console errors and no 4xx/5xx.

Next is Phase 10 (the member experience), then 11 and 12. See [plan.md](plan.md).

### Client conventions set in Phase 9

- **A signal input is not readable in the constructor.** Load from an `effect()` that reads the input and wraps the side effects in `untracked()` — see `member-profile.ts` and `training-details.ts`. This also handles the router reusing a component when only the route parameter changes, which the note-notification email does.
- **`untracked()` is not optional around signal writes in an effect.** `MemberAvatar` read `objectUrl` inside its own effect (via `release()`) and wrote it from the fetch, so the effect depended on its own output and refetched the photo forever. A spec caught it; the shape to copy is "read the one trigger, `untracked()` everything else".
- **Chart colours must be resolved through a probe element.** `getComputedStyle().getPropertyValue('--tcm-chart-1')` returns the *specified* value, so a `light-dark(...)` token arrives at Chart.js as a literal string and it silently draws black. `chart-theme.ts` assigns the token to a real `color` property on a hidden span and reads that back.
- **`<app-member-avatar>` is the only way to show a member photo.** The endpoint is authenticated, so an `img src` cannot reach it; the component fetches the blob and — importantly — revokes the object URL on destroy and on change.
- **Material's `mat-button-toggle-group` draws its own selection checkmark.** Add `hideSingleSelectionIndicator` when the buttons already carry icons.
- **Each profile tab fetches only when first opened.** A coach checking a belt history should not pay for three charts and a payment history.

### The design system (Phase 8b)

- **`src/styles/_tokens.scss` is the only place a colour, radius, spacing step, duration or shadow is declared.** A component reaches for the variable. A hex code or a bare `rem` inside a component means the token is missing — add it there instead.
- **Never write a second palette for dark mode.** Colours are `light-dark()` pairs resolved against `color-scheme` on `<html>`, which `ThemeService` owns. Material's own system variables already work this way, so one property switches everything.
- **`npm run check:contrast`** parses the *compiled* stylesheet and measures every pair against WCAG AA in both themes. Run it after touching a colour. It is what caught the Okabe-Ito chart palette failing at ~2.2:1 on white.
- **State is never colour alone.** `<app-status-chip>` requires an icon, and `_shared/status-presentation.ts` holds the one mapping from each domain enum to its tone and glyph. Add a state there, once.
- **`<app-chart>` themes itself.** It merges `baseChartOptions()`, assigns series colours from the shared palette, and rebuilds on a theme change — so callers pass labels and numbers, not colours.
- **Motion is CSS only** (Material 22 has no `@angular/animations`); route transitions come from `withViewTransitions()`. Everything is disabled under `prefers-reduced-motion` in `_motion.scss`.
- **Material's button has two icon slots.** A trailing icon needs `iconPositionEnd` **on the `<mat-icon>`**, not on the button — otherwise it renders before the label regardless of DOM order.
- **The route title comes from the router snapshot, not `Title.getTitle()`.** Angular's title strategy is itself a `NavigationEnd` subscriber, so reading `Title` in another subscriber can return the previous page's title.
- **Use `.tcm-visually-hidden`, not `.cdk-visually-hidden`.** The CDK a11y stylesheet is not imported, so the CDK class renders as plain visible text.

### Client conventions set in Phase 8

- **The error interceptor stays quiet for 400s and for 401s from the three anonymous account endpoints.** A rejected login or a spent reset token is the screen's story, not a global snackbar — those screens render it inline with `<app-form-alert>`. `/account/register` is excluded from that exemption because it is coach-authenticated, so a 401 there is a genuinely lost session.
- **`apiErrorParts(error)` in `_services/unwrap.ts`** is the single place an `HttpErrorResponse`, a network failure or an `unwrap` throw becomes readable text. It returns the message and the field errors separately, because a form renders them separately; `apiErrorMessage()` joins them for a snackbar.
- **Dates on the wire are `DateOnly` strings** built by `toDateOnly()` from *local* date parts. `toISOString()` would shift a birthday across midnight for anyone west of Greenwich.
- **`appTrim`** on every email input. `Validators.email` rejects a pasted `" ana@example.test "`, and the user cannot see the difference.
- **Signal inputs receive query parameters** — `withComponentInputBinding()` is on, so `?returnUrl=`, `?email=` and `?token=` arrive as `input()`s rather than through `ActivatedRoute`. A `returnUrl` is followed only when it starts with a single `/`.

### Known items carried forward to Phase 12

- Deleting an online payment frees its Stripe session id, so the id could be replayed to recreate it. Needs a voided-session record (a migration).
- Deactivating a member does not revoke an already-issued JWT; they stay in until it expires. Needs security-stamp validation.
- Two email deep links assume Angular routes that do not exist yet: `/dashboard/trainings/{id}` and `/dashboard/members/{id}`. Reconcile in Phase 7.
