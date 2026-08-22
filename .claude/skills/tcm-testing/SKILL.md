---
name: tcm-testing
description: The TCM test strategy and commands — xUnit for services, repositories and controllers, Vitest for Angular services, guards and components, Playwright for end-to-end flows, and the role-matrix authorization suite that this app most depends on. Use when writing tests, fixing a failing suite, or deciding what to test for a new feature.
---

# TCM testing

## What matters most here

This app's core risk is not a broken chart — it is a member reading another member's data. Spec section 5 is a test matrix, and turning it into tests is the highest-value work in this repo. Everything else is secondary.

## Backend — xUnit

```bash
dotnet test                                   # all
dotnet test --filter FullyQualifiedName~Members
```

**Service tests.** Fake the repository and the external services. Cover the business rules: payment due-date calculation, note ordering by priority (High first), `IsCurrentBelt` uniqueness when a new belt is added, training status transitions, attendance and performance recording, member deactivation leaving history intact.

**Repository tests.** SQLite in-memory with a real EF Core model, so relationships and cascade behaviour are actually exercised — the plain in-memory provider will not catch a bad `DeleteBehavior`.

**Endpoint tests.** `WebApplicationFactory<Program>` with a test authentication handler that injects a chosen id and role. For every endpoint write, at minimum:

- Coach → allowed.
- Member on their own resource → allowed where spec section 5 says so.
- Member on another member's resource → `403`, and the response body leaks nothing.
- Anonymous → `401`.

Stripe, Firebase and SMTP are always faked. Never touch a real third party in a test.

## Frontend — Vitest

Angular 22 scaffolds with **Vitest and jsdom, not Karma/Jasmine** — the spec predates that change. Specs use `describe` / `it` / `expect` from `vitest`, and `vi.fn()` rather than `jasmine.createSpy()`.

```bash
cd client && npm test
cd client && npm test -- --run     # single pass, for CI
```

The app is also **zoneless** (Angular 22 ships no zone.js), so a component test must await `fixture.whenStable()` after a signal change rather than relying on `detectChanges()` alone to flush async work.

Services with `provideHttpClientTesting`, asserting URL, method, body and the `ApiResponse` unwrapping. Guards and the JWT interceptor tested directly — the interceptor must attach the header, and must clear the session on 401. Components shallow-rendered, asserting the three states (loading, empty, error) and that role-gated controls are absent for a member.

## End-to-end — Playwright

Through the `playwright` plugin's MCP server, against a seeded database. Seed the state each run; never depend on leftovers.

Core journeys:
1. Coach: log in → register a member → create a training with invitees → record attendance and performance → log a cash payment → add a note.
2. Member: log in → see own dashboard only → report attendance for an invited training → view own payments and belts → start the Stripe checkout redirect (assert the redirect happens; do not complete a live payment).
3. Negative: member navigates directly to a coach-only route and to another member's profile URL — both are refused.
4. Forgot password → reset link → new password → log in.

## Conventions

- `Method_Scenario_ExpectedResult` naming. One assertion concept per test.
- Arrange with builders, not copy-pasted object literals.
- A failing test is a finding. Report it with the real output. **Never weaken an assertion to turn a suite green** — if a test is wrong, say why and fix the test deliberately.

## Reporting

Always state the exact command run and the counts passed/failed. Quote any failure verbatim.

## Tooling

`qodo-get-rules` for a pre-commit quality pass, the built-in `/code-review` skill after a slice lands, `security-review` and the `security-reviewer` agent before anything auth- or payment-related is called done.
