# plan.md — Building the Taekwondo Club Management System

A step-by-step build plan for the application described in [SPEC.md](SPEC.md), together with the Claude Code tooling (plugins, skills, subagents) installed to support it.

**Starting point:** the repository contains `SPEC.md` and nothing else. That resolves the open question in spec section 9 — this is a **greenfield build from the spec**, not a continuation of an existing codebase.

**Verified local toolchain:** .NET SDK 9.0.316, Node 24.18.0, npm 11.16.0, Docker 27.3.1. Phase 0 checks whether a newer LTS .NET is current and installs it if so.

---

## 1. Tooling installed for this project

### 1.1 Plugins (project scope, all enabled)

| Plugin | What it gives us | Where it is used |
|---|---|---|
| **context7** | Version-specific docs pulled straight from source repos, for whichever .NET / EF Core / Angular / Stripe versions we pin | Every phase. The defence against version drift |
| **microsoft-docs** | Official ASP.NET Core, EF Core, Identity and SQL Server docs (`microsoft-learn` MCP) | Phases 2–6 |
| **csharp-lsp** | C# symbol resolution, references, safe renames | Phases 2–6 |
| **typescript-lsp** | TypeScript/Angular code intelligence | Phases 7–10 |
| **stripe** | Official Stripe plugin — `stripe-best-practices`, `stripe-docs`, `test-cards`, `explain-error`, plus the Stripe MCP server | Phase 5, Phase 9 |
| **playwright** | Browser automation MCP for end-to-end tests and screenshots | Phase 11, and any visual check |
| **modern-web-guidance** | Current web-platform and Angular best practice, kept fresh | Phases 7–10 |
| **frontend-design** | Production-grade UI generation that avoids generic AI aesthetics | Phases 7–10 |
| **qodo** | Shift-left review: `qodo-get-rules` before commit, `qodo-pr-resolver` for PR feedback | After every phase |
| **gitkraken** | Real git/PR/issue context across providers (needs one-time auth) | Phase 1 onward |

Together these add roughly 3.1k always-on tokens per session; the rest is paid only when a skill actually fires.

**Removed 2026-08-22:** `firebase`. Photos are stored in SQL Server as `varbinary(max)` rather than in Firebase Storage (see *Decisions taken during the build* below), so the plugin had nothing left to do.

**Deliberately not installed:** `azure-sql-developer` (17 skills, ~4.9k always-on tokens, and it steers toward a preview container image rather than the plain SQL Server the spec names — the `tcm-database` skill covers the same ground for free). `duende-skills` (IdentityServer-specific; the spec uses plain ASP.NET Identity + JWT). Add either later with `claude plugin install <name>@claude-plugins-official -s project` if a need appears.

The spec's section 10 also listed "Engineering" and "Design" bundles. Those names do not exist in the official marketplace; their function is covered by `frontend-design`, `qodo`, and Claude Code's built-in `/code-review`, `/security-review` and `/simplify` skills.

### 1.2 Subagents (`.claude/agents/`)

| Agent | Owns | Runs in parallel with |
|---|---|---|
| **backend-architect** | Solution layout, layering, DTO contracts, DI wiring, cross-cutting concerns | Nothing — it goes first |
| **data-model-agent** | Entities, DbContext, Fluent API, migrations, seeding. **Sole owner of migrations** | Nothing during a schema change |
| **api-feature-builder** | One backend vertical slice: DTO → repository → service → controller → DI → tests | Other instances, one domain each |
| **angular-feature-builder** | One Angular feature module: models → service → routes → components → specs | Other instances, one module each |
| **integrations-agent** | Stripe, database-backed photo storage, Gmail SMTP | Backend slices |
| **qa-test-agent** | xUnit, Jasmine/Karma, Playwright | Follows a slice |
| **security-reviewer** | Read-only audit against spec sections 5 and 7 | Gates every auth/payment change |

Slice ownership is what makes parallelism safe: two `api-feature-builder` instances working on Members and Trainings touch disjoint files.

### 1.3 Skills (`.claude/skills/`)

| Skill | Covers | Names the plugins for |
|---|---|---|
| **tcm-spec** | Spec map, settled decisions, repo layout, plugin routing table | Everything — read first |
| **tcm-database** | SQL Server in Docker, `dotnet ef` workflow, Fluent API conventions, the cascade-path trap, seeding | microsoft-docs, context7, csharp-lsp |
| **tcm-auth** | Identity, JWT, the role matrix, coach-only registration, password reset, guards and interceptor | microsoft-docs, context7 |
| **tcm-backend-slice** | The six-step recipe for an API slice, with `ApiResponse` and authorization conventions | context7, microsoft-docs, csharp-lsp |
| **tcm-angular-feature** | Folder layout, typed services, Material vs Bootstrap, loading/empty/error, screen-specific notes | context7, modern-web-guidance, frontend-design |
| **tcm-stripe-payments** | Checkout session flow, server-side verification, cash payments, payment screens | stripe plugin + MCP |
| **tcm-notifications** | The four Gmail SMTP emails and database-backed photo handling | — |
| **tcm-testing** | Test strategy, the role-matrix suite, commands, reporting rules | playwright, qodo |
| **tcm-run-local** | Starting all three processes, config keys, seeded accounts, first-run failure table | playwright |

---

## 2. The build plan

Each phase states its goal, the work, who does it, and what must be true before moving on. Do not start a phase whose exit criteria upstream are unmet.

### Phase 0 — Version decisions and prerequisites

**Goal:** know exactly which versions we are building on, and record them.

1. Check the current stable/LTS .NET release and install the SDK if newer than 9.0.316. Pin it in `global.json`.
2. Check the current stable Angular, Angular Material and EF Core versions.
3. Confirm the SQL Server container image and Docker are working.
4. Record every pinned version in `CLAUDE.md`, with the date checked.

**Agent:** main session. **Skills:** `tcm-spec`. **Plugins:** `context7`, `microsoft-docs`.
**Exit:** `global.json`, `CLAUDE.md` and a written version table exist. No version is left to a later guess.

### Phase 1 — Repository and solution scaffolding

**Goal:** an empty but building solution with the right shape.

1. `git init`, `.gitignore` for .NET + Node + `*serviceAccount*.json` + `appsettings.*.local.json`.
2. Solution with `TCM.Api`, `TCM.Application`, `TCM.Domain`, `TCM.Infrastructure`, `TCM.Tests` and the project references pointing inward.
3. `ng new` the client into `client/`, with Angular Material and Bootstrap 5 wired in.
4. `ApiResponse<T>`, `BaseController`, global exception middleware, Serilog or built-in structured logging, Swagger, CORS from configuration, HTTPS redirection.
5. `CLAUDE.md` recording stack, layout, conventions and commands.

**Agent:** `backend-architect`. **Skills:** `tcm-spec`, `tcm-backend-slice`.
**Exit:** `dotnet build` and `ng build` both clean. API starts, Swagger loads, client serves a placeholder page.

### Phase 2 — Data model and migrations

**Goal:** the schema from spec section 4, in the database.

1. Entities and enums in `TCM.Domain`: `ApplicationUser`, `Club`, `Belt`, `MemberBelt`, `Payment`, `Attendance`, `Training`, `Note`, `Photo`; enums `TrainingType`, `TrainingStatus`, `NotePriority`, `AttendanceStatus`.
2. `ApplicationDbContext : IdentityDbContext<ApplicationUser>` plus one `IEntityTypeConfiguration<T>` per entity.
3. `DeleteBehavior.Restrict` on the multi-FK paths — `Notes.FromMemberId`/`ToMemberId`, `AspNetUserRoles.MemberId`, `Attendances`. Expect the cascade-path error otherwise.
4. Indexes on the FKs we filter on constantly.
5. Initial migration; apply against the local container.
6. Idempotent seeder: roles, belts, one club, one coach account from configuration.

**Agent:** `data-model-agent`. **Skills:** `tcm-database`. **Plugins:** `microsoft-docs`, `context7`.
**Exit:** `dotnet ef database update` clean; every table and column in spec section 4 exists; the seeder is safe to re-run.

### Phase 3 — Authentication and authorization

**Goal:** login works, roles are enforced server-side, password reset works.

1. Identity configuration, password policy, `TokenService`/`ITokenService`, JWT bearer validation from configuration.
2. `AccountController`: login, coach-only register, forgot password, reset password.
3. Role and ownership authorization helpers, applied in the service layer so no controller can skip them.
4. `RolesController` for role listing/assignment.
5. Endpoint tests covering the four authorization outcomes per route.

**Agent:** `backend-architect` designs; `api-feature-builder` implements; `security-reviewer` gates.
**Skills:** `tcm-auth`, `tcm-backend-slice`, `tcm-testing`.
**Exit:** A coach token opens coach routes, a member token is refused on them, an anonymous call gets 401, and a member cannot reach another member's id. Security review clean.

### Phase 4 — Backend core plumbing

**Goal:** the shared machinery every slice reuses.

1. Generic `IRepository<T>` / `Repository<T>` with async CRUD and `SaveChangesAsync`.
2. Mapping between entities and DTOs — one approach chosen and used everywhere.
3. Validation approach chosen (FluentValidation or DataAnnotations) and applied at the service boundary.
4. DI extension methods `AddApplication()` and `AddInfrastructure()`.
5. `CommonService`/`CommonController` for lookups: belts, roles, club numbers (`ClubNumbersInfoDto`).

**Agent:** `backend-architect`. **Skills:** `tcm-backend-slice`.
**Exit:** One reference slice (Common) exists end to end and sets house style for every slice after it.

### Phase 5 — External integrations

**Goal:** Stripe, photo storage and email working, with fakes so the app runs on no third-party credentials.

1. `EmailService` + `GmailSettings` + `SendEmailRequest`, four HTML templates, non-fatal failure handling, logging fallback when SMTP is unconfigured.
2. `PhotoService` storing member and club photos **as bytes in the `Photos` table**; content-type and size validation; an authenticated endpoint that serves them.
3. `StripeService` + `StripeController`: customer creation at registration, Checkout Session creation, **server-side verification before any `Payments` row is written**, environment-based success/cancel URLs, idempotency on session id — all behind a `Stripe:Enabled` flag, with a local fake active while it is off.

**Agent:** `integrations-agent`. **Skills:** `tcm-stripe-payments`, `tcm-notifications`. **Plugins:** `stripe`.
**Exit:** App starts and every feature works with no third-party credentials configured. Photos round-trip through the database. With `Stripe:Enabled=true` and test keys, a checkout session URL is returned and a verified payment writes exactly one row. Each of the four emails fires once.

### Phase 6 — Backend vertical slices *(parallelizable)*

**Goal:** every endpoint spec section 6 implies.

Run as four `api-feature-builder` instances, one domain each:

| Slice | Serves | Key endpoints |
|---|---|---|
| **Members** | 6.1, 6.3, 6.4 | list with filters (name/email, belt, age group), register, deactivate, edit, profile, belts add/delete |
| **Trainings & Attendance** | 6.5, 6.6 | training CRUD, invitees, table and calendar feeds, attendance/absence reporting, per-member performance |
| **Payments** | 6.4, 6.7 | member payment history, club-wide list with year/month/member/method filters, cash logging, delete |
| **Notes** | 6.4, 6.8 | notes about a member, club-wide list, priority ordering (High first), search by title, add/delete with the self-delete rule |

**Skills:** `tcm-backend-slice`, `tcm-auth`. **Follow each with:** `qa-test-agent`, then `/code-review`.
**Exit:** Every screen in section 6 has the API it needs. Full route list documented. All tests green.

### Phase 7 — Angular foundation

**Goal:** the client skeleton from spec section 3.3.

1. `_guards/`, `_interceptors/`, `_models/`, `_services/`, `_shared/` created with the JWT interceptor, error interceptor, auth guard and coach guard.
2. `environment.ts` files — API base URL and every environment-specific value, nothing hardcoded.
3. Routing shell with lazy-loaded features, `not-found/`, and role-aware navigation.
4. Angular Material theme plus Bootstrap 5 layout; a shared layout component with the two side-menu variants (coach and member).

**Agent:** `angular-feature-builder`. **Skills:** `tcm-angular-feature`. **Plugins:** `modern-web-guidance`, `frontend-design`, `typescript-lsp`.
**Exit:** `ng build` clean, a protected route redirects to login, the interceptor attaches the token.

### Phase 8 — Auth screens

**Goal:** spec section 6.1 in the browser.

Login, forgot password, reset password (reading email and token from the URL), and the coach-only register-member form with all its fields — first name, last name, email, password, height, weight, date of birth, belt, role.

**Agent:** `angular-feature-builder`. **Skills:** `tcm-angular-feature`, `tcm-auth`.
**Exit:** Full round trip works against the real API: log in as the seeded coach, register a member, reset that member's password by email link.

### Phase 8b — Visual design pass

**Goal:** an app that looks designed rather than scaffolded, and **one visual language that every screen after this inherits**. This sits between phases 8 and 9 on purpose: the five feature modules of phase 9 are the bulk of the UI, and they should be built *on* the design system rather than restyled afterwards.

1. **Theme and tokens.** Replace the CLI's default azure palette with a club identity — a Material 3 palette from a chosen seed colour, plus semantic tokens for the states this app repeats over and over: training finished / active / cancelled, note priority High / Medium / Low, attendance present / absent / invited, membership paid / due / overdue. Defined once in `styles.scss` as CSS custom properties, so no component ever invents a colour.
2. **Type and spacing scale.** One type ramp and one spacing scale, both expressed as tokens. Ad-hoc `rem` values come out of the component styles written in phases 7 and 8.
3. **Light and dark.** `color-scheme: light dark`, both palettes checked for contrast — WCAG AA on text, 3:1 on UI borders and chart marks — and a toggle in the shell that remembers the choice.
4. **Chrome.** Rework the shell: an icon rail that collapses on narrow screens, a real club mark, a consistent page-header pattern (title, subtitle, primary action), and an account menu showing the member's photo.
5. **Component styling.** Shared card, table, chip and badge treatments — statuses and priorities become chips, not bare text — plus skeleton loaders for table and card screens, replacing `StatePanel`'s bare spinner where a shape can be predicted.
6. **Chart theme.** One Chart.js defaults object — palette, fonts, grid, tooltip — so the charts in 6.2 and 6.4 read as one family. Colour is never the only encoding.
7. **Motion.** Route and list transitions in **CSS only** (Material 22 dropped `@angular/animations`; see the version-drift traps in `CLAUDE.md`), short, and switched off under `prefers-reduced-motion`.
8. **Empty and error states with character.** Per-screen wording and imagery in `StatePanel` instead of one generic icon — an empty member list and a failed payment load should not look identical.
9. **Responsive sweep** at 360, 768, 1024 and 1440 px, with Playwright screenshots recorded as the visual baseline.
10. **Retro-fit** all of the above onto what exists at this point: the shell, the four auth screens, `not-found`.

**Agent:** `angular-feature-builder`. **Skills:** `tcm-angular-feature`.
**Plugins:** `frontend-design` (leads this phase), `modern-web-guidance`, `playwright` for the screenshots.
**Exit:** `ng build` and `npm run test:ci` clean; the token file is the only place a colour, radius or spacing value is declared; both themes pass contrast; the shell and every phase-8 screen use the new system; screenshots recorded at four widths. Phase 9 then builds on this system rather than beside it.

**Contrast is checked, not asserted.** `npm run check:contrast` reads the *compiled* stylesheet, pulls both halves out of every `light-dark()` declaration and measures each pair. Run it after any token change — it is the only thing standing between a palette that looks fine on the machine it was picked on and one that works.

### Phase 9 — Feature screens *(parallelizable)*

**Goal:** spec sections 6.2 – 6.8 in the browser.

| Module | Screen | Notes |
|---|---|---|
| **dashboard/club-details** | 6.2 | Stat cards, trainings-per-month chart, colour-coded calendar, countdown to next training, quick member search, reactive year/month filters |
| **dashboard/members** | 6.3, 6.4 | Member table with filters, deactivate with confirmation, and the three-tab profile (attendance/performance charts, membership, belts and notes) |
| **dashboard/trainings** | 6.5, 6.6 | Table and calendar views, add/edit form with invitees, training details with attendance and performance entry |
| **dashboard/payments** | 6.7 | Club-wide table with filters, delete with confirmation, and the member-side "Pay Membership Fee" redirect |
| **dashboard/notes** | 6.8 | Club-wide notes, priority icons, search by title, add/delete |

**Agents:** parallel `angular-feature-builder` instances, one module each.
**Skills:** `tcm-angular-feature`, `tcm-stripe-payments`. **Plugins:** `frontend-design`, `context7`.
**Exit:** Every screen renders real data, handles loading/empty/error, confirms before destructive actions, and uses phase 8b's tokens and shared patterns — no module introduces a colour, spacing value or status treatment of its own.

### Phase 10 — The member experience

**Goal:** the restricted half of spec section 5, verified rather than assumed.

Member home page, own profile only, own attendance/performance, own payments with online payment, own belts and notes (delete own only), attendance reporting for invited trainings. The coach's controls must be absent — and the underlying API calls must be refused even when forced.

**Agents:** `angular-feature-builder`, then `security-reviewer`.
**Exit:** Logged in as a member, every coach-only route is unreachable in the UI **and** rejected by the API when called directly.

### Phase 11 — Test suite

**Goal:** the role matrix and the core journeys, automated.

1. Backend: service, repository and endpoint tests, with the four-outcome authorization matrix per route.
2. Frontend: service, guard, interceptor and component specs.
3. Playwright: coach journey, member journey, the negative-access journey, and the password-reset journey.
4. A single command that runs everything, documented in `CLAUDE.md`.

**Agent:** `qa-test-agent`. **Skills:** `tcm-testing`. **Plugins:** `playwright`, `qodo`.
**Exit:** All suites green, counts reported, and the negative-access tests genuinely fail when authorization is removed (verify by temporarily breaking it).

### Phase 12 — Hardening and deployment readiness

**Goal:** ready to deploy the day a hosting decision is made.

1. Full `security-reviewer` pass plus `/security-review` over the whole codebase.
2. Confirm zero hardcoded hosts, URLs, keys or connection strings anywhere — spec section 9 is explicit that hosting is undecided, so nothing may assume an environment.
3. Production build settings, structured logging, health check endpoint, `dotnet ef migrations script` for deployment.
4. `README.md`: setup, configuration keys, run and test commands, architecture overview.
5. Accessibility pass on the main screens — charts need text alternatives, forms need labels, colour-coding needs a non-colour cue.

**Agents:** `security-reviewer`, `qa-test-agent`.
**Exit:** Clean security review, clean build, documented setup, and a deployment that needs only configuration values — no code changes.

---

## 3. Sequencing

```
Phase 0 → 1 → 2 → 3 → 4
                      ├─ Phase 5  (integrations)
                      └─ Phase 6  (Members ∥ Trainings ∥ Payments ∥ Notes)
                                 ↓
                        Phase 7 → 8 → 8b  (design system)
                                       ↓
                        Phase 9  (5 modules in parallel)
                                 ↓
                        Phase 10 → 11 → 12
```

Phases 0–4 are strictly sequential: they set versions, layout, schema and house style, and every later phase copies them. Phase 8b is sequential for the same reason — it sets the visual house style that phase 9 copies. The parallel opportunities are phase 6 (four backend domains) and phase 9 (five Angular modules).

## 4. Conventions that hold across every phase

- **Verify, do not guess, an API signature.** `context7` for the pinned version, `microsoft-docs` for the .NET stack. Version drift is the largest avoidable cost in this project.
- **The first slice sets house style.** Every later slice matches it rather than introducing a second way of doing the same thing.
- **Build and test before reporting done**, and state the command and its result.
- **Security is checked, not assumed.** Anything touching auth, roles, payments or member data goes through `security-reviewer` before it counts as finished.
- **Nothing environment-specific in source.** Configuration only.

## 4b. Decisions taken during the build

| Decision | Date | Why |
|---|---|---|
| **Photos live in SQL Server**, not Firebase Storage | 2026-08-22 | User's call. Removes a third-party dependency and a set of credentials entirely. `Photos.Content` is `varbinary(max)`, capped by `Photos:MaxSizeBytes`. Supersedes SPEC section 2's "File storage: Firebase Storage" |
| **Stripe deferred behind `Stripe:Enabled`** | 2026-08-22 | User's call: a working app first, real Stripe after. The flag off selects a local fake that completes the flow; on selects real Checkout. The verification rule in SPEC section 3.2 holds in both paths |
| Timestamps are UTC `DateTime`, not `DateTimeOffset` | 2026-08-22 | EF Core 10 cannot translate `DateTimeOffset.Year`/`.Month` in a `GroupBy`, which section 6.2's chart requires |
| No `PasswordSalt` column | 2026-08-22 | Identity's hasher embeds the salt in `PasswordHash`; a separate column would always be empty |
| `Payments.StripeSessionId` added | 2026-08-22 | The idempotency key section 3.2 needs so a retried webhook cannot double-write a payment |
| A 401 from `/account/login`, `/account/forgot-password` or `/account/reset-password` is **not** treated as an expired session | 2026-08-23 | Found building phase 8. The error interceptor was clearing storage and redirecting to a login page the user was already standing on, over a simple wrong password. Those three endpoints now report inline; `/account/register` is deliberately excluded because it is coach-authenticated, so a 401 there really is a lost session |
| 400 responses are left to the screen that raised them | 2026-08-23 | A rejected form belongs beside the form. A snackbar has faded by the time the user has finished reading the field errors |
| Belts and roles cached for the session in `CommonService`, cleared on logout | 2026-08-23 | Every form with a belt dropdown would otherwise refetch the same nine rows. Roles are coach-only, so the cache must not survive a sign-out |
| Design gets its own phase (8b) rather than being folded into phase 9 | 2026-08-23 | User's call: the app has to look good in use. Doing it before the five feature modules means one visual language they all inherit, instead of five that need reconciling |
| Custom M3 palette: primary `#1B3A6B`, tertiary `#B8860B` | 2026-08-23 | The CLI's default azure reads as "an Angular app". Navy and gold are the club's own; gold rather than a second red keeps the tertiary from colliding with error and the critical signal ramp. Regenerate with `ng generate @angular/material:theme-color` |
| ~~Barlow Condensed for display/headline, Roboto for body~~ → **IBM Plex Sans / Sans Condensed / Mono** | 2026-08-23 | Superseded by the Dojang pass. Barlow + Roboto was the M3 brand/plain split done correctly and still read as a default Angular app. One family in three cuts gives the same split plus a *data voice* — the mono that marks eyebrows, table headers and status chips as readings off a record |
| **Visual direction: "Dojang"** — ink chrome, belt rail, rationed gold | 2026-08-23 | User's call after being shown three directions. The app is for a taekwondo club, so its structure comes from the club's own world: dark walls and a bright mat, and the belt as the thing that encodes rank and state everywhere it appears. The alternatives (a competition "Scoreboard", an editorial "Studio") are recorded here in case the club's identity changes |
| Panel shadows were dead from Phase 8b to this pass | 2026-08-23 | `light-dark()` accepts *colours*, not whole values, so `--tcm-shadow-1: light-dark(0 1px 2px …, …)` was an invalid declaration the browser dropped silently. Every card in the app had been rendering flat. The switch now lives in `--tcm-shadow-colour` |
| Light and dark from `light-dark()` and one `color-scheme` property | 2026-08-23 | Material's system variables already resolve against `color-scheme`, so `ThemeService` setting that one property switches the whole app. No second palette, no theme class to remember, nothing that can drift |
| Chart palette is Okabe-Ito **darkened for the light theme** | 2026-08-23 | The published values are chosen for colour-blind hue separation, not contrast: orange and sky blue land at ~2.2:1 on white. `npm run check:contrast` caught it; the darkened values clear 3:1 in both themes and stay ≥18 ΔE apart |
| Route motion via the View Transitions API | 2026-08-23 | Angular Material 22 dropped `@angular/animations`, so `withViewTransitions()` plus CSS is the only route-animation path that does not add a package back |
| `/dashboard/profile` redirects to `/dashboard/members/<own id>` | 2026-08-23 | "My profile" and "a member's profile" are the same screen from two directions. One component that takes an id beats two that drift apart |
| The dashboard skips its coach-only panels for a member rather than 403ing | 2026-08-23 | `/dashboard` is the landing page for both roles, but the calendar, countdown and quick search sit behind coach-only endpoints. A member sees the club figures alone until Phase 10 builds them a home page of their own |
| Chart series colours are assigned by `<app-chart>`, not by callers | 2026-08-23 | Callers pass labels and numbers. It is what keeps a series the same colour on every screen, and what lets the chart recolour itself when the theme flips |
| Attendance and scores save per row, as they are changed | 2026-08-23 | SPEC 6.6 is a register, not a form. A coach marking twelve people should not have to find a Save button, and a per-row busy flag keeps one slow request from freezing the table |
| `/dashboard` resolves to a different component per role via `canMatch` | 2026-08-23 | Found building phase 10. A redirect would put a second URL in the history and a switching component would download both chunks. `coachHomeMatch` returning false simply lets the router try the next route, so a member never pays for the club dashboard |
| The API serialises every `DateTime` with a `Z` | 2026-08-23 | Found building phase 10's countdown. The columns are UTC `datetime2`, EF returns `Unspecified`, and the default serializer wrote a naive string that JavaScript reads as local time — so a session at 16:00 UTC showed as 16:00 in a UTC+2 browser. A converter in `TCM.Api/Serialization` makes the wire format say what the values already meant |
| A member's session notes live on the training details screen, not the profile | 2026-08-23 | SPEC 6.6 asks for a notes panel per training. `GET /notes/training/{id}/member/{memberId}` existed since phase 6 with no caller; phase 10 gave it one. It is member-side only — for a coach "the member" is ambiguous there, and they already write a session note per row |

## 5. Open items

- ~~**Playwright** is used ad hoc in phase 8b … phase 11 installs it properly for `e2e/`~~ — done in phase 11. `e2e/` is its own package with `@playwright/test`, four journeys, and a `webServer` block that starts the API and the client itself. `client/scripts/screenshots.mjs` still stands alone for the visual sweep.
- **Hosting** stays undecided by design (spec section 9). Phase 12 makes the app deployment-ready without choosing; revisit once the target is known.
- ~~**Charting library**~~ — resolved in phase 7: **Chart.js 4**, used directly through the shared `<app-chart>` component rather than via an Angular wrapper library. Calendar colour-coding uses Material's `MatCalendar` with `dateClass`, so no second date dependency.
- **`gitkraken`** needs a one-time authentication before its git and PR context becomes available.
- **Stripe credentials** are deferred by explicit decision. `Stripe:Enabled` is `false` with a dummy key, so the local fake carries the whole payment flow. Flip the flag and supply real test keys to switch over — no code change.
- ~~**Gmail app password** is still needed before real email sends~~ — supplied 2026-08-24 and stored in user-secrets. `SmtpEmailService` is now the registered sender and delivery is confirmed. The one part still not automated is reading a reset token out of an inbox; a mail catcher would close it (phase 12).
