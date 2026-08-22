SPEC.md — Taekwondo Club Management System (TCM)

Extracted from the thesis "Implementation of a Web Application for Managing a Taekwondo Club" (Adrijan Stojanovski, FCSE Skopje, September 2025). This document consolidates the architecture, data model, and functional requirements into a form suitable for giving instructions to Claude Code.

1. Purpose of the Application

A web application for digitizing the day-to-day operations of a taekwondo club. Replaces manual tracking of attendance, membership fees, and member registration. Two types of users:

Coach (Trainer) — full access: registering and managing members, tracking trainings and attendance, tracking membership payments, scoring performance, belt promotions, notes.
Member — restricted access: their own home page and their own profile — attendance/performance, membership fees (with online payment), belt exams and notes for themselves. 2. Technology Stack
Layer Technology
Backend .NET (latest stable version available at implementation time), ASP.NET Core Web API
ORM Entity Framework Core (version matching the chosen .NET version)
Database Microsoft SQL Server
Frontend Angular (latest stable version available at implementation time)
UI libraries Angular Material, Bootstrap 5
Payments Stripe (.NET SDK, Stripe Checkout Session)
File storage Firebase Storage (member/club photos)
Authentication ASP.NET Identity (AspNetUsers/Roles) + JWT token
Email SMTP via Gmail (confirmations, password reset, training invitations, note notifications)

Note on versions: The original thesis used .NET 8 and Angular 19 (as of September 2025). This spec intentionally does not pin those versions — during implementation (Claude Code) the currently latest stable (LTS where applicable) versions of .NET, Angular, Angular Material, and Entity Framework Core should be checked and used. The rest of the architecture (layered backend, modular Angular) stays the same regardless of the specific version.

3. Architecture

Monolithic application, technically split into client and server, designed as a single integrated whole (chosen for simplicity and easier maintenance, suitable for a small-to-medium project).

3.1 Server Architecture — Layered
Controller (Presentation) → Service (Business Layer) → Repository (Data Layer, EF Core) → MSSQL
Controllers (entry point of the RESTful API, receives HTTP requests GET/POST/PUT): AccountController, BaseController, CommonController, MembersController, NotesController, RolesController, StripeController, TrainingsController.
Services (business logic, validation, integration with external services): MemberService, NoteService, TokenService, TrainingService, CommonService, FirebaseStorageService, EmailService (with GmailSettings, SendEmailRequest), StripeService. Each service has its own interface (I...Service).
Repositories (abstraction over MSSQL via EF Core, CRUD operations independent of business logic): generic IRepository/Repository, plus MemberRepository, NoteRepository, TrainingRepository (and IPaymentRepository for payments).
Models/DTOs (not a separate layer, used across all layers): ApiResponse, BeltDto, ClubNumbersInfoDto, EditTrainingDto, ForgotPasswordDto, LoginMemberDto, MemberDto, MemberRegisterDto, MemberRoleDto, MemberTokenDto, MemberTrainingDto, NoteDto, PaymentsDto, PhotoDto, ResetPasswordDto, RoleDto, TrainingDetailsDto, TrainingDto.
3.2 Stripe Integration

The server uses the Stripe .NET SDK to create a Checkout Session (SessionCreateOptions, Mode = "payment", SuccessUrl/CancelUrl, LineItems with priceId), and Angular simply redirects the user to the returned session.Url. This way, sensitive card data never passes through the client or server side. When a new member registers, a Stripe Customer is automatically created (stored as StripeCustomerId on AspNetUsers).

Decision: the SuccessUrl/CancelUrl values must be environment-based configuration, not hardcoded (the original thesis used http://localhost:4200/successful-payment and .../failed-payment directly in code — this must become an environment variable / app setting per deployment environment, e.g. dev/staging/prod).

3.3 Client Architecture — Modular (Angular)
\_guards — access control for protected routes (checks whether the user is authenticated / has the right role).
\_interceptors — HTTP interceptors: attaching the JWT token to headers, centralized error handling, logging/security.
\_models — TS classes/interfaces for data structures (Member, Payment, Training, etc.).
\_services — abstraction for communicating with the backend API (e.g. MemberService, PaymentService).
\_shared — shared components, directives, validators.
dashboard/ — central module with feature sub-modules: club-details, members, notes, payments, register-member, trainings.
login/, forgot-password/, reset-password/ — authentication.
not-found/ — 404 page.
app — root component. 4. Data Model (ER Diagram)

Relational model, MSSQL. Tables and attributes:

AspNetUsers (main table for members/coaches — a standard ASP.NET Identity table extended with domain-specific fields) Id (PK), FirstName, LastName, PhotoId, DateOfBirth, Email, PasswordHash, PasswordSalt, IsActive, StartedOn, IsCoach, Height, Weight, ClubId (FK → Clubs.Id), PhoneNumber, UserName, StripeCustomerId, + standard Identity fields (AccessFailedCount, ConcurrencyStamp, EmailConfirmed, LockoutEnabled, LockoutEnd, NormalizedEmail, NormalizedUserName, PhoneNumberConfirmed, SecurityStamp, TwoFactorEnabled).

AspNetRoles — Id (PK), Name, NormalizedName, ConcurrencyStamp.

AspNetUserRoles (bridge Users↔Roles) — UserId (FK → AspNetUsers.Id), RoleId (FK → AspNetRoles.Id), MemberId (FK → AspNetUsers.Id).

Clubs — Id (PK), Name, Address, ClubLogoId (FK → Photos.Id). Model is 1 coach : 1 club for this project (see decision in section 9 — multi-club support is explicitly out of scope for now, kept only as a possible future improvement, see section 8).

Belts (belt lookup table) — Id (PK), BeltName.

MemberBelts (bridge — each member may have multiple belts over time, but only one is current) — Id (PK), MemberId (FK → AspNetUsers.Id), BeltId (FK → Belts.Id), DateReceived, Description, IsCurrentBelt.

Payments — Id (PK), MemberId (FK → AspNetUsers.Id), IsPaidOnline, PaymentDate, NextPaymentDate.

Attendances — Id (PK), Date, Description, TrainingId (FK → Trainings.Id), MemberId (FK → AspNetUsers.Id), Performance, Status.

Trainings — Id (PK), Date, Description, MemberId (FK → AspNetUsers.Id), ClubId (FK → Clubs.Id), TrainingType (Regular / Sparring), Status (Active / Cancelled / Finished).

Notes — Id (PK), Title, Content, CreatedAt, FromMemberId (FK → AspNetUsers.Id), ToMemberId (FK → AspNetUsers.Id), TrainingId (FK → Trainings.Id), Priority (Low / Medium / High).

Photos — Id (PK), Url, PublicId, MemberId (FK → AspNetUsers.Id).

5. Roles and Access Rights
   Feature Coach Member
   Home dashboard (club stats, calendar) ✅ full ✅ own home page only
   Member list + filters ✅ ❌
   Register new member ✅ (only way in; no self sign-up) ❌
   Deactivate member ✅ ❌
   Edit own data ✅ (for anyone) ✅ (self only)
   Attendance/performance per training ✅ enters for anyone 👁 views own only
   Membership/payments overview ✅ for everyone 👁 own only
   Pay membership fee online (Stripe) — ✅
   Log cash membership payment ✅ —
   CRUD trainings ✅ ❌ (can only report attendance/absence for a training they're invited to)
   Belt exams (add/delete) ✅ 👁 view only
   Notes about another member ✅ ❌
   Notes about self ✅ ✅ (can delete only own notes)
   Club-wide payments page ✅ ❌
   Club-wide notes page ✅ ❌
6. Functional Specification by Screen
   6.1 Login / Registration / Forgot Password

Login is a mandatory entry step for everyone. On valid email/password, the server generates a JWT that determines which routes are available based on role. Registering a new member is available only to a logged-in coach (there is no public self-registration) — a form with: First Name, Last Name, Email, Password, Height, Weight, Date of Birth, Belt, Role. On successful registration, a Stripe Customer is also automatically created. "Forgot Password" is available to everyone: enter email → email with a reset link (containing the email + password reset token) → new password form (with confirmation) → redirect to login.

6.2 Home Page (Dashboard)

After login: cards showing total number of members, trainings held, attendance percentage; a chart of trainings held per month; a calendar with past/upcoming trainings (color-coded); a countdown to the next training; quick search/navigation to a member's profile by first/last name; a side menu (different for coach vs. member). Filtering by year/month of trainings updates the cards in real time.

6.3 Member List (coach only)

A table with all members (regardless of status): first name, last name, join date, email, age, whether they're a coach, status (Active/Inactive), current belt, an action to deactivate (with a confirmation modal). Filters: name/email, belt, age group (Kids, Juniors, Cadets, Seniors, etc.). Button to add a new member.

6.4 Member Profile

Left side: general data (email, join date, age, height, weight, current belt) + "Edit Data" and "Pay Membership Fee" buttons. Center: 3 tabs:

Attendance and Performance — attendance-per-month chart, pie chart of attendance/absence %, a line chart of performance per training, a list of trainings held with performance/description (filter by year).
Membership — a banner with the next payment due date, a table of payment history (date, method Cash/Online, due date), the ability to delete a record.
Belt Exams and Notes — left: list of belt exams (belt, date, whether it's the current belt, add/delete — coach only); right: notes about the member (a titled card with priority icons, search by title, add/delete).
6.5 Trainings (coach only)

Two views: Table view (title, date, type, status, filter by title/status/type, edit/delete per row) and Calendar view (color-coded dates: green = finished, yellow = active; clicking a date shows details + a list of members with attendance %). Add/edit training form: description/title, members (invitees), type (Regular/Sparring), status (Active/Cancelled/Finished), date, optional notes. When a training is created, every invited member receives an email with a link to the details so they can mark attendance/absence.

6.6 Training Details

List of invited/present members (both the coach and the member themselves report attendance/absence + reason for absence); the coach additionally enters a performance score per member; a notes panel for the member for that specific training (add/search/delete).

6.7 Membership Payments (club-wide, coach only)

A table of all payments in the club with filters by year, month, member, payment method; delete a record (with confirmation).

6.8 Notes (club-wide, coach only)

All notes about all members. Priority (Low/Medium/High) determines the display order in the member's profile (High first). When a note is created, the member it's intended for receives an email notification.

7. Security Aspects
   Passwords: PasswordHash + PasswordSalt (ASP.NET Identity).
   JWT token issued on successful login, automatically attached to every request via an Angular interceptor.
   Route guards on the client + role-based authorization on the server.
   Payments: all card processing happens on a Stripe-hosted page — the application never handles sensitive payment data.
8. Future Improvements (already identified by the author)
   Two-factor authentication via a code sent by email/phone.
   Localization (i18n) for accessibility from different language regions.
   Extended analytics (e.g. which member in the black belt group has the best performance over the last 5 trainings).
   Support for one coach managing multiple clubs (currently Club→Members is 1:N with one coach per club — explicitly out of scope for this version, see decision in section 9).
9. Decisions and Open Items

Resolved:

Stripe success/cancel URLs: environment-based configuration (per deployment environment), not hardcoded localhost values.
Multi-club support: not needed. The model stays 1 coach : 1 club for this version. The multi-club idea remains listed only as a possible future improvement (section 8), out of scope for the current build.
Deployment/hosting strategy (MSSQL + .NET API + Angular): not yet decided — this will be determined once the application has been built, since the exact hosting approach isn't known yet. Claude Code should keep configuration environment-agnostic (env variables/app settings, no hardcoded hosts) so that a hosting decision made later doesn't require rework.

Still open:

Is this a brand-new implementation from scratch based on this spec, or a continuation/extension of an existing repository? If existing code already exists, it should be given to Claude Code as context in addition to this spec — this has not been clarified yet. 10. Recommended Claude Code Plugins

The following plugins are recommended to support development of this project. None of them are stack-specific for .NET/EF Core/MSSQL or Stripe — no plugin in the searched marketplace targets those directly, but Claude Code has native support for them without a plugin.

Plugin Why it's relevant here
Engineering Broadly useful across the whole stack: code review, architecture-decision documentation (good for tracking decisions from this spec), testing strategy for both the API and Angular, and a deploy checklist (relevant to the still-open deployment question in section 9).
GitKraken Git/branch/PR/issue access directly from Claude. Useful if subagents work on separate branches or PRs per domain (Members, Trainings, Payments, etc.), matching the vertical-slice subagent approach discussed for this project.
Qodo Shift-left code review — catches issues before commit and resolves PR feedback in-agent. A useful second check after each subagent's task.
Modern Web Guidance Keeps Angular/frontend guidance current with the latest web best practices — relevant given the decision to use the latest stable Angular rather than a pinned version.
Design Design-system management, WCAG accessibility review, Figma-based dev handoff, and UX copy — relevant to the Angular Material/Bootstrap UI described in section 6 (dashboard, member profile tabs, forms, tables).
