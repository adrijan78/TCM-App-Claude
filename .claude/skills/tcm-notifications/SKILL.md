---
name: tcm-notifications
description: TCM email and file storage — Gmail SMTP EmailService with its four templated messages (registration confirmation, password reset, training invitation, note notification), and FirebaseStorageService for member and club photos. Use for any work on EmailService, GmailSettings, SendEmailRequest, FirebaseStorageService, the Photos table, or an upload/notification bug.
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

## File storage — Firebase Storage

`IFirebaseStorageService` / `FirebaseStorageService` handles member photos and the club logo, writing `Url` and `PublicId` into the `Photos` table (`Id`, `Url`, `PublicId`, `MemberId`).

### Rules

- Validate before upload: content type is an allowed image type, size under an explicit configured cap, and dimensions sane. Do not trust the client-supplied filename or content type alone — check the actual bytes.
- Generate the stored object name yourself; never build a path from user input.
- Deleting a photo removes both the storage object and the `Photos` row. If the storage delete fails, do not orphan the row silently — log and surface it.
- Service-account credentials come from configuration or an environment variable pointing at a key file. **Never commit a key.** Add `*serviceAccount*.json` to `.gitignore` on day one.
- Ship a local disk-backed or no-op implementation so the app runs without Firebase credentials.
- The `firebase` MCP server is available for inspecting buckets and storage rules.

## Verify

Registration, password reset, training creation and note creation each produce exactly one email in the log or inbox. Photo upload writes a `Photos` row whose `Url` actually loads. A configuration with no credentials still lets the app start and every other feature work.
