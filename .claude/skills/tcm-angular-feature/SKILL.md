---
name: tcm-angular-feature
description: The recipe for adding an Angular feature to the TCM client — where files go under _models/_services/_guards/_shared and dashboard/, typed API services, routing and lazy loading, Angular Material plus Bootstrap usage, charts and calendar, and the loading/empty/error rule. Use for any frontend screen or component work.
---

# Adding an Angular feature

Structure is fixed by `SPEC.md` section 3.3.

```
src/app/
  _guards/        auth.guard.ts, coach.guard.ts
  _interceptors/  jwt.interceptor.ts, error.interceptor.ts
  _models/        member.model.ts, training.model.ts, payment.model.ts, note.model.ts, belt.model.ts
  _services/      member.service.ts, training.service.ts, payment.service.ts, note.service.ts, auth.service.ts
  _shared/        components, directives, validators, pipes
  dashboard/      club-details/ members/ notes/ payments/ register-member/ trainings/
  login/ forgot-password/ reset-password/ not-found/
```

## Order of work

1. **Model** in `_models/` — an interface per API payload, mirroring the backend DTO field for field. No `any`, ever.
2. **Service** in `_services/` — the only place `HttpClient` appears. Base URL from `environment`, never a literal. Unwrap the `ApiResponse<T>` envelope in one place so components see plain data or an error.
3. **Route** — lazy-load the feature; declare the required role in route `data` and guard it.
4. **Component** — container fetches, presentational children render. Keep templates free of business logic.
5. **Spec** — service and guard specs are mandatory; component specs for anything with real logic.

## Conventions

- **Check before you write.** Read `angular.json`, `package.json` and one existing feature to see what this repo uses — standalone components, signals, the built-in control flow, `inject()`. Match it. When unsure of an API on the pinned Angular version, ask `context7`; for current web platform practice, invoke `modern-web-guidance`.
- **Material first, Bootstrap for layout.** `MatTable`, `MatPaginator`, `MatSort`, `MatDialog`, `MatDatepicker`, `MatTabGroup`, `MatSnackBar` cover nearly every screen in section 6. Use Bootstrap 5 for grid and spacing only. Do not hand-roll what Material provides.
- **Reactive forms** with typed form groups and validators in `_shared/validators` when reused. Show errors after touch, not on load.
- **Three states, always:** loading, empty, error. Use `<app-state-panel>` from `_shared/components/state-panel.ts` rather than hand-rolling them — it exists so a screen cannot quietly ship with only the happy path. A screen missing one of these is not done.
- **Destructive actions** go through `ConfirmDialog` in `_shared/components/confirm-dialog.ts`. It resolves `true` only on an explicit confirm; dismissing by backdrop or Escape resolves undefined, which callers must treat as "no".
- **Do not add `provideAnimationsAsync()`.** Angular Material 22 dropped `@angular/animations` from its peer dependencies and animates with CSS; asking for it fails the build on a package that is not installed.
- **No leaks.** `async` pipe, or `takeUntilDestroyed`. Never a bare `subscribe` in a component without teardown.
- **Confirmation modals** for every destructive action — deactivate member, delete payment, delete note, delete training (spec sections 6.3, 6.7).
- **Role-driven UI.** Hide what the role cannot do, and render a different side menu for coach and member (section 6.2) — but treat this as UX only. The server enforces access.

## Screen-specific notes

- **Dashboard (6.2):** stat cards, trainings-per-month chart, colour-coded calendar of past and upcoming trainings, countdown to the next training, quick member search. Year/month filters must update the cards reactively.
- **Member profile (6.4):** three `MatTabGroup` tabs — Attendance and Performance (bar, pie and line charts plus a filterable training list), Membership (next-due banner, payment history table), Belt Exams and Notes (belt list on the left, priority-ordered notes on the right, High first).
- **Trainings (6.5):** table view and calendar view of the same data — green finished, yellow active. Clicking a date opens details with per-member attendance percentages.
- **Charts:** use `<app-chart>` in `_shared/components/chart.ts`. It wraps **Chart.js 4** directly — chosen over `ng2-charts` and friends because an Angular wrapper pins a peer range against the Angular major and lags a new release, and this app is on Angular 22. `ariaLabel` is a required input: a bare `<canvas>` is invisible to a screen reader, and these charts show information that appears nowhere else on the page.
- **Calendar:** Angular Material's `MatCalendar` with `dateClass` for the colour coding of SPEC 6.5 (green finished, yellow active). No FullCalendar — it is another Angular-major-coupled dependency, and `dateClass` plus `selectedChange` covers what section 6.5 asks for. Colour alone is not a cue: pair it with a marker or text so the state survives a colour-blind reader.
- **Photos** are fetched, not linked. `<img src="/api/photos/...">` cannot carry a bearer token, and the endpoint is authenticated on purpose. Fetch the bytes through `HttpClient` (`responseType: 'blob'`) and bind an object URL, revoking it on destroy.

## Finish

```bash
npm run build && npm test
```

Invoke the `frontend-design` skill for screens that need real visual judgement. Report the routes and components you added.
