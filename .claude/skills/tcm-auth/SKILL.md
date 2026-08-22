---
name: tcm-auth
description: TCM authentication and authorization — ASP.NET Identity setup, JWT issuance and validation, the Coach/Member role matrix, coach-only member registration, forgot/reset password by email, and the Angular guards and JWT interceptor. Use for any work on login, tokens, roles, password reset, or protecting an endpoint or route.
---

# TCM auth

Spec sections 5, 6.1 and 7 govern this area. Get it wrong and the whole app is wrong, so treat every rule here as binding.

## Server

**Identity.** `ApplicationUser : IdentityUser` + `IdentityRole`, EF stores. Password hashing is Identity's — never hand-rolled. Two roles only: `Coach` and `Member`.

**Token issuance.** `TokenService` / `ITokenService` builds the JWT. Claims: `NameIdentifier` (user id), `Email`, and one `Role` per assigned role. Nothing sensitive beyond that. Signing key, issuer, audience and lifetime all come from configuration.

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateIssuer   = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromMinutes(1)
};
```

**Registration is coach-only.** There is no public sign-up endpoint (spec section 6.1). `POST /api/account/register` is `[Authorize(Roles = "Coach")]`. Registration also creates the Stripe Customer and stores `StripeCustomerId`.

**Forgot / reset password.** `ForgotPassword` always returns the same success response whether or not the email exists — never confirm account existence. Generate the token with `UserManager.GeneratePasswordResetTokenAsync`, URL-encode it, and email a link built from a configured client base URL. `ResetPassword` consumes email + token + new password.

## The two-part authorization rule

Role attributes are necessary but not sufficient. Anything member-scoped must also verify ownership:

```csharp
var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var isCoach  = User.IsInRole("Coach");
if (!isCoach && callerId != requestedMemberId) return Forbid();
```

Put this check in the service layer so it cannot be skipped by a new controller. **A member changing an id in a URL must never reach another member's data.** This is the single most likely security bug in this app.

## Role matrix (spec section 5) — condensed

Coach-only: member list and filters, register member, deactivate member, edit anyone, enter attendance and performance, log a cash payment, CRUD trainings, add/delete belt exams, notes about another member, club-wide payments page, club-wide notes page.

Member: own home page and profile only; view own attendance, performance, payments, belts and notes; pay membership online via Stripe; report own attendance or absence for a training they were invited to; delete their own notes.

## Client

**Interceptor** (`_interceptors/`) attaches `Authorization: Bearer <token>` to every outgoing request, and centralises error handling: 401 clears the session and routes to login, 403 shows a "not permitted" message, 5xx surfaces a generic failure. Never log token contents.

**Guards** (`_guards/`) — an auth guard for "logged in", a role guard for "is coach". Route data declares the required role. Guards are UX, not security: the server decides.

**Storage.** Keep the token in one place behind an auth service. Decode it only to read expiry and role for menu rendering; never trust it for anything the server must verify.

## Verify before calling it done

Log in as a member and attempt a coach-only route and another member's profile id. Both must fail at the API, not only in the UI. `qa-test-agent` turns the role matrix above into an automated test suite — run it.
