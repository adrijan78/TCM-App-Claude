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
- **Three states, always:** loading (skeleton or spinner), empty ("no trainings yet"), error (message plus retry). A screen missing one of these is not done.
- **No leaks.** `async` pipe, or `takeUntilDestroyed`. Never a bare `subscribe` in a component without teardown.
- **Confirmation modals** for every destructive action — deactivate member, delete payment, delete note, delete training (spec sections 6.3, 6.7).
- **Role-driven UI.** Hide what the role cannot do, and render a different side menu for coach and member (section 6.2) — but treat this as UX only. The server enforces access.

## Screen-specific notes

- **Dashboard (6.2):** stat cards, trainings-per-month chart, colour-coded calendar of past and upcoming trainings, countdown to the next training, quick member search. Year/month filters must update the cards reactively.
- **Member profile (6.4):** three `MatTabGroup` tabs — Attendance and Performance (bar, pie and line charts plus a filterable training list), Membership (next-due banner, payment history table), Belt Exams and Notes (belt list on the left, priority-ordered notes on the right, High first).
- **Trainings (6.5):** table view and calendar view of the same data — green finished, yellow active. Clicking a date opens details with per-member attendance percentages.
- **Charts:** pick one charting library and use it everywhere. Give every chart an accessible text alternative — a screen reader must not hit a bare canvas.

## Finish

```bash
npm run build && npm test
```

Invoke the `frontend-design` skill for screens that need real visual judgement. Report the routes and components you added.
