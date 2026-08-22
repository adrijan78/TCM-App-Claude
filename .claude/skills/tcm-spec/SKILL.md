---
name: tcm-spec
description: Orientation for the Taekwondo Club Management (TCM) app — where each requirement lives in SPEC.md, the fixed architectural decisions, the repo layout, and which installed plugin to reach for on which problem. Read this first at the start of any TCM task, before planning or writing code.
---

# TCM — project orientation

`SPEC.md` at the repo root is the contract. This skill tells you how to navigate it and how to work in this repo.

## Where things are specified

| You are working on | Read SPEC.md |
|---|---|
| Stack and versions | section 2 |
| Backend layering, class names | section 3.1 |
| Stripe flow | section 3.2 |
| Angular folder structure | section 3.3 |
| Tables, columns, foreign keys | section 4 |
| Who may do what | section 5 |
| A specific screen | section 6.1 – 6.8 |
| Auth, tokens, payment safety | section 7 |
| Out of scope | section 8 |
| Settled decisions | section 9 |

## Decisions that are already made — do not relitigate

- **Versions are not pinned in the spec.** Use the current stable .NET (LTS), Angular, Angular Material and EF Core, verified at implementation time, then pin them in `global.json` / `package.json` and record them in `CLAUDE.md`.
- **Monolith**, split into an ASP.NET Core Web API server and an Angular client.
- **1 coach : 1 club.** Multi-club is a future idea, not this build.
- **No public self-registration.** A coach registers members; that is the only way in.
- **Nothing environment-specific is hardcoded** — no hosts, URLs, connection strings or keys in source. Stripe success/cancel URLs are configuration. Hosting is deliberately undecided, so keep everything environment-agnostic.
- **Card data never touches our code.** Stripe-hosted Checkout only.

## Repo layout

```
server/
  TCM.Api/            controllers, middleware, Program.cs, appsettings
  TCM.Application/    services + interfaces, DTOs, validation
  TCM.Domain/         entities, enums
  TCM.Infrastructure/ DbContext, EF configs, migrations, repositories, external services
  TCM.Tests/          xUnit
client/
  src/app/
    _guards/ _interceptors/ _models/ _services/ _shared/
    dashboard/{club-details,members,notes,payments,register-member,trainings}/
    login/ forgot-password/ reset-password/ not-found/
e2e/                  Playwright
```

## Which plugin for which problem

| Problem | Reach for |
|---|---|
| Exact API surface of the pinned .NET / EF Core / Angular / Stripe version | `context7` MCP — resolve the library, then fetch docs |
| ASP.NET Core, EF Core, Identity, SQL Server guidance | `microsoft-docs` skill / `microsoft-learn` MCP |
| Stripe Checkout, test cards, error meanings | `stripe` plugin skills + `stripe` MCP |
| Firebase Storage buckets and rules | `firebase` MCP |
| Current Angular / web platform best practice | `modern-web-guidance` skill |
| A screen that needs real visual judgement | `frontend-design` skill |
| Browser-driven E2E and screenshots | `playwright` MCP |
| C# / TypeScript symbol resolution, references, rename safety | `csharp-lsp`, `typescript-lsp` |
| Pre-commit quality pass, PR feedback | `qodo-get-rules`, `qodo-pr-resolver`, built-in `/code-review` |
| Commits, branches, PRs, issues | `gitkraken` (needs one-time auth) |

Do not guess an API signature when `context7` or `microsoft-docs` can tell you. Version drift is the single most likely source of wasted work in this project.

## Working rules

- One domain per agent per task. Backend slices (Members, Trainings, Payments, Notes) and Angular feature modules are designed to be built in parallel without file collisions.
- Match the surrounding code's style, naming and comment density. The first finished slice sets house style; every later slice copies it.
- Build and test before reporting done. State the command you ran and its result.

## Related skills

`tcm-backend-slice`, `tcm-angular-feature`, `tcm-database`, `tcm-auth`, `tcm-stripe-payments`, `tcm-firebase-storage`, `tcm-email`, `tcm-testing`, `tcm-run-local`.
