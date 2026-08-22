---
name: tcm-run-local
description: How to start the TCM stack locally — SQL Server container, the .NET API, the Angular dev server — plus the required configuration keys, CORS and proxy setup, seeded login accounts, and the usual first-run failures and their fixes. Use whenever the app needs to be run, demoed, screenshotted, or debugged end to end.
---

# Running TCM locally

Three processes: SQL Server (Docker), the API, the Angular dev server.

## 1. Database

```bash
docker start tcm-sql || docker run -d --name tcm-sql \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<local-password>" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

Then apply migrations — see `tcm-database`.

## 2. API

```bash
cd server/TCM.Api
dotnet run
```

Swagger comes up on the HTTPS port printed in the console. Use it to confirm the API is healthy before blaming the client.

## 3. Client

```bash
cd client
npm install     # first run only
npm start
```

Serves on `http://localhost:4200`.

## Configuration keys (user-secrets, not committed)

```
ConnectionStrings:Default
Jwt:Key            Jwt:Issuer      Jwt:Audience     Jwt:ExpiryMinutes
Stripe:SecretKey   Stripe:MembershipPriceId
Stripe:SuccessUrl  Stripe:CancelUrl        Stripe:WebhookSecret
Gmail:Host  Gmail:Port  Gmail:SenderEmail  Gmail:SenderName  Gmail:AppPassword
Firebase:Bucket    Firebase:CredentialsPath
Client:BaseUrl                     # used to build email links
Cors:AllowedOrigins                # the Angular origin
Seed:CoachEmail    Seed:CoachPassword
```

Set them with `dotnet user-secrets set "<key>" "<value>"` from `server/TCM.Api`. The app must start with the third-party ones absent — fake implementations take over (see `tcm-notifications`, `tcm-stripe-payments`).

## Seeded accounts

The seeder creates one coach from `Seed:CoachEmail` / `Seed:CoachPassword`, the two roles, the belt lookup and one club. Members are created by logging in as the coach and registering them — there is no self sign-up.

## First-run failures and their fixes

| Symptom | Cause and fix |
|---|---|
| `A network-related or instance-specific error` | Container not running, or wrong port. `docker ps`, check 1433. |
| `Login failed for user 'sa'` | Password mismatch between the container and the connection string. |
| `may cause cycles or multiple cascade paths` | Missing `DeleteBehavior.Restrict` — see `tcm-database`. |
| CORS error in the browser console | `Cors:AllowedOrigins` does not include `http://localhost:4200`. Never fix this with `AllowAnyOrigin` plus credentials. |
| 401 on every API call | Interceptor not registered, or the JWT key/issuer/audience differ between issuance and validation. |
| Angular build fails after a version bump | Check the pinned Angular version's migration guide via `context7` before changing code. |
| Stripe redirect lands on a blank page | `Stripe:SuccessUrl` / `CancelUrl` not configured for this environment. |

## Verifying a change end to end

Start all three, log in as the coach, walk the screen you changed, and check both the browser console and the API console for errors. For a visual check or a screenshot, drive the browser through the `playwright` MCP server rather than asking the user to look.
