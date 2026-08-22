---
name: tcm-notifications
description: TCM email and photo storage — Gmail SMTP EmailService with its four templated messages (registration confirmation, password reset, training invitation, note notification), and PhotoService storing member and club photos as bytes in the database. Use for any work on EmailService, GmailSettings, SendEmailRequest, PhotoService, the Photos table, or an upload/notification bug.
---

# TCM email and storage

## Email — Gmail SMTP

`IEmailService` / `EmailService`, configured by a strongly-typed `GmailSettings` (host, port, sender address, sender display name, app password) bound from configuration. The app password lives in user-secrets or an environment variable — never in a committed file, never in source.

### The four messages (spec section 2 and section 6)

| Trigger | Content |
|---|---|
| Coach registers a member (6.1) | Welcome and confirmation, with how to sign in |
| Forgot password (6.1) | Reset link containing the email and the URL-encoded reset token |
| Training created (6.5) | Invitation with a deep link to the training details screen so the member can report attendance or absence |
| Note created for a member (6.8) | Notification that a note was added |

### Rules

- **Email failure must never fail the business operation.** Wrap every send; log the failure and continue. A coach must not lose a created training because SMTP timed out.
- Send off the request path where it is not user-visible — fire-and-forget with logging, or a background queue. Never block an HTTP response on SMTP.
- Links are built from a configured client base URL, never a hardcoded host (spec section 9).
- One HTML template per message with a plain-text fallback, in a `Templates/` folder with simple token substitution. Do not concatenate HTML inline in the service.
- Never log a reset token, a password, or full recipient lists.
- Ship a `NoOpEmailService` (logs instead of sending) so the app runs locally and in tests without SMTP credentials, selected by configuration.

### Gmail specifics

Gmail SMTP requires an app password on an account with 2FA — a normal account password will not authenticate. Port 587 with STARTTLS. Expect rate limits; that is another reason failures must be non-fatal.

## Photo storage — in the database

**Decided 2026-08-22: photos live in SQL Server, not Firebase Storage.** This supersedes SPEC section 2's "File storage: Firebase Storage" and removes that dependency and its credentials entirely.

`IPhotoService` / `PhotoService` stores member photos and the club logo as bytes in the `Photos` table: `Id`, `PublicId` (a GUID), `FileName`, `ContentType`, `Content` (`varbinary(max)`), `SizeBytes`, `CreatedAt`, `MemberId`.

### Rules

- **Sniff the bytes, do not trust the client.** Check the actual magic numbers for the allowed image types (JPEG, PNG, WebP, GIF) rather than believing the supplied `Content-Type` or file extension.
- Enforce `Photos:MaxSizeBytes` before reading the whole stream into memory, not after.
- Never use the client-supplied filename as a path. It is stored for display only.
- `PublicId` is a GUID, not the primary key, so photo URLs cannot be walked by incrementing a number.
- **Serving requires authentication.** These are photographs of club members, some of them minors. The endpoint authorizes like any other member-scoped resource: a coach may fetch any photo in their club, a member only their own. Because an `<img src>` cannot carry an `Authorization` header, the Angular side fetches the bytes through the authenticated HTTP client and renders an object URL.
- Set an explicit `Cache-Control: private` and an `ETag` so repeat views are cheap without the image landing in a shared cache.
- Deleting a photo removes the row and clears any `PhotoId` / `ClubLogoId` still pointing at it.
- **Never `Include` the `Content` column in a list query.** Project only the metadata; loading a hundred members must not drag a hundred images into memory.

### The trade-off, stated plainly

`varbinary(max)` keeps everything transactional and backed up with the database, at the cost of database size and of images flowing through the app on every request. That is fine at club scale — hundreds of members, one photo each. If this ever grows past a few gigabytes, the fix is object storage plus a URL column, and the `Photos` table is already shaped for that move.

## Verify

Registration, password reset, training creation and note creation each produce exactly one email in the log or inbox. A photo round-trips: upload, fetch, and the bytes come back byte-identical with the right content type. An unauthenticated fetch is refused, as is a member fetching someone else's photo. A configuration with no third-party credentials still lets the app start and every feature work.
