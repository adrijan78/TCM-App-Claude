---
name: tcm-database
description: TCM database workflow — running SQL Server locally in Docker, the EF Core migration commands, entity and Fluent API conventions, the cascade-path traps in this schema, and idempotent seeding of belts, roles, the club and the coach account. Use for any schema change, migration, connection-string or seed-data work.
---

# TCM database workflow

Schema is fixed by `SPEC.md` section 4. Only the `data-model-agent` creates migrations, so history stays linear.

## Local SQL Server

```bash
docker run -d --name tcm-sql \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<strong-local-password>" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

docker start tcm-sql          # subsequent runs
docker logs tcm-sql --tail 20 # confirm "SQL Server is now ready"
```

The connection string goes in user-secrets, never in a committed file:

```bash
cd server/TCM.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=TCM;User Id=sa;Password=<local-password>;TrustServerCertificate=True"
```

`appsettings.json` may contain the *key* with an empty value for discoverability; it must never contain a real credential.

## Migration commands

```bash
dotnet tool install --global dotnet-ef        # once
dotnet ef migrations add <PascalCaseName> -p server/TCM.Infrastructure -s server/TCM.Api
dotnet ef database update                 -p server/TCM.Infrastructure -s server/TCM.Api
dotnet ef migrations remove               -p server/TCM.Infrastructure -s server/TCM.Api   # only if NOT applied
dotnet ef migrations script <from> <to>   -p server/TCM.Infrastructure -s server/TCM.Api   # for deployment
```

Always read the generated `Up`/`Down` before applying. Never edit an applied migration — write a new one.

## Entity conventions

- One `IEntityTypeConfiguration<T>` per entity in `TCM.Infrastructure/Configurations/`, applied with `ApplyConfigurationsFromAssembly`.
- `ApplicationUser : IdentityUser` carries the domain fields (`FirstName`, `LastName`, `PhotoId`, `DateOfBirth`, `IsActive`, `StartedOn`, `IsCoach`, `Height`, `Weight`, `ClubId`, `StripeCustomerId`).
- Enums (`TrainingType`, `TrainingStatus`, `NotePriority`, `AttendanceStatus`) are stored as `int` unless a readable column is worth the join cost — decide once and stay consistent.
- Decimals get explicit precision (`Height`, `Weight`) or EF will warn and SQL Server will silently truncate.
- Index the foreign keys you filter on constantly: `Attendances.TrainingId`, `Attendances.MemberId`, `Payments.MemberId`, `Notes.ToMemberId`, `Trainings.ClubId`.

## The cascade trap in this schema — expect it

`Notes` has two FKs to `AspNetUsers` (`FromMemberId`, `ToMemberId`), `AspNetUserRoles` carries an extra `MemberId`, and `Attendances` points at both a training and a member. SQL Server refuses multiple cascade paths to the same table. Configure `DeleteBehavior.Restrict` on these deliberately:

```csharp
builder.HasOne(n => n.FromMember).WithMany().HasForeignKey(n => n.FromMemberId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne(n => n.ToMember).WithMany().HasForeignKey(n => n.ToMemberId).OnDelete(DeleteBehavior.Restrict);
```

If `migrations add` succeeds but `database update` fails with "may cause cycles or multiple cascade paths", this is why.

## Seeding

Idempotent seeder run at startup in Development, or behind an explicit command:

1. Roles `Coach` and `Member` via `RoleManager`.
2. `Belts` lookup rows (white through black, in grading order).
3. One `Club`.
4. One coach account via `UserManager`, credentials from configuration — never a literal in source.

Check for existence before inserting; the seeder must be safe to run repeatedly.

## Deactivation, not deletion

Spec section 6.3 deactivates members (`IsActive = false`). Never hard-delete a member — attendance, payment and note history depends on the row.

## Tooling

`microsoft-docs` and `context7` for EF Core APIs on the pinned version. `csharp-lsp` to find every reference before renaming an entity property.
