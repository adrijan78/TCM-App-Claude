using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Persistence;

namespace TCM.Tests.UnitTests;

/// <summary>
/// A real <see cref="ApplicationDbContext"/> over a throwaway SQLite database, seeded with two
/// clubs so every "scoped to the caller's club" query has something it could wrongly return.
/// </summary>
/// <remarks>
/// Repository tests run against a real provider rather than a substitute on purpose: what they
/// are actually checking is that each query <em>translates</em> and filters in SQL. An in-memory
/// list would evaluate the same LINQ client-side and pass whatever the provider would reject —
/// which is exactly the class of bug this project has hit before (see the GroupBy note in
/// CLAUDE.md).
/// </remarks>
public sealed class RepositoryFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public ApplicationDbContext Context { get; private set; } = null!;

    public int ClubId { get; private set; }
    public int OtherClubId { get; private set; }
    public string CoachId { get; private set; } = string.Empty;
    public string MemberId { get; private set; } = string.Empty;
    public string PeerId { get; private set; } = string.Empty;
    public string OutsiderId { get; private set; } = string.Empty;
    public int WhiteBeltId { get; private set; }
    public int BlackBeltId { get; private set; }
    public int TrainingId { get; private set; }
    public int OtherClubTrainingId { get; private set; }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        await Context.Database.EnsureCreatedAsync();

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        var club = new Club { Name = "Test Club" };
        var otherClub = new Club { Name = "Rival Club" };
        Context.Clubs.AddRange(club, otherClub);

        var white = new Belt { BeltName = "White", Rank = 1 };
        var black = new Belt { BeltName = "Black", Rank = 10 };
        Context.Belts.AddRange(white, black);
        await Context.SaveChangesAsync();

        ClubId = club.Id;
        OtherClubId = otherClub.Id;
        WhiteBeltId = white.Id;
        BlackBeltId = black.Id;

        var coach = NewUser("coach@test.local", "Cara", "Coach", club.Id, isCoach: true, birthYear: 1985);
        var member = NewUser("member@test.local", "Ana", "Member", club.Id, birthYear: 2000);
        var peer = NewUser("peer@test.local", "Bo", "Peer", club.Id, birthYear: 2014);
        var outsider = NewUser("outsider@test.local", "Zed", "Outsider", otherClub.Id, birthYear: 1999);
        var inactive = NewUser("gone@test.local", "Gil", "Gone", club.Id, birthYear: 1995);
        inactive.IsActive = false;

        Context.Users.AddRange(coach, member, peer, outsider, inactive);
        await Context.SaveChangesAsync();

        CoachId = coach.Id;
        MemberId = member.Id;
        PeerId = peer.Id;
        OutsiderId = outsider.Id;

        Context.MemberBelts.AddRange(
            new MemberBelt
            {
                MemberId = member.Id, BeltId = white.Id,
                DateReceived = new DateOnly(2024, 3, 1), IsCurrentBelt = false
            },
            new MemberBelt
            {
                MemberId = member.Id, BeltId = black.Id,
                DateReceived = new DateOnly(2026, 1, 15), IsCurrentBelt = true
            },
            new MemberBelt
            {
                MemberId = peer.Id, BeltId = white.Id,
                DateReceived = new DateOnly(2025, 5, 5), IsCurrentBelt = true
            });

        var training = new Training
        {
            Description = "Sparring drills",
            Date = new DateTime(2026, 3, 10, 18, 0, 0, DateTimeKind.Utc),
            MemberId = coach.Id,
            ClubId = club.Id,
            TrainingType = TrainingType.Regular,
            Status = TrainingStatus.Finished
        };

        var secondTraining = new Training
        {
            Description = "Belt exam preparation",
            Date = new DateTime(2026, 4, 2, 18, 0, 0, DateTimeKind.Utc),
            MemberId = coach.Id,
            ClubId = club.Id,
            TrainingType = TrainingType.Sparring,
            Status = TrainingStatus.Active
        };

        var rivalTraining = new Training
        {
            Description = "Rival session",
            Date = new DateTime(2026, 3, 11, 18, 0, 0, DateTimeKind.Utc),
            MemberId = outsider.Id,
            ClubId = otherClub.Id,
            TrainingType = TrainingType.Regular,
            Status = TrainingStatus.Active
        };

        Context.Trainings.AddRange(training, secondTraining, rivalTraining);
        await Context.SaveChangesAsync();

        TrainingId = training.Id;
        OtherClubTrainingId = rivalTraining.Id;

        Context.Attendances.AddRange(
            new Attendance
            {
                TrainingId = training.Id, MemberId = member.Id, Date = training.Date,
                Status = AttendanceStatus.Present, Performance = 8
            },
            new Attendance
            {
                TrainingId = training.Id, MemberId = peer.Id, Date = training.Date,
                Status = AttendanceStatus.Absent, Description = "Hospital appointment"
            },
            new Attendance
            {
                TrainingId = secondTraining.Id, MemberId = member.Id, Date = secondTraining.Date,
                Status = AttendanceStatus.Invited
            });

        Context.Payments.AddRange(
            new Payment
            {
                MemberId = member.Id, IsPaidOnline = true,
                PaymentDate = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
                NextPaymentDate = new DateOnly(2026, 2, 4), StripeSessionId = "sess_seeded"
            },
            new Payment
            {
                MemberId = member.Id, IsPaidOnline = false,
                PaymentDate = new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc),
                NextPaymentDate = new DateOnly(2026, 3, 8)
            },
            new Payment
            {
                MemberId = outsider.Id, IsPaidOnline = false,
                PaymentDate = new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
                NextPaymentDate = new DateOnly(2026, 3, 9)
            });

        Context.Notes.AddRange(
            new Note
            {
                Title = "Excellent form", Content = "Much improved footwork.",
                CreatedAt = new DateTime(2026, 3, 10, 19, 0, 0, DateTimeKind.Utc),
                FromMemberId = coach.Id, ToMemberId = member.Id,
                TrainingId = training.Id, Priority = NotePriority.Low
            },
            new Note
            {
                Title = "Missed three sessions", Content = "Follow up with the parents.",
                CreatedAt = new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc),
                FromMemberId = coach.Id, ToMemberId = member.Id,
                Priority = NotePriority.High
            },
            new Note
            {
                Title = "Rival note", Content = "Not this club's business.",
                CreatedAt = new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc),
                FromMemberId = outsider.Id, ToMemberId = outsider.Id,
                Priority = NotePriority.High
            });

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private static ApplicationUser NewUser(
        string email, string firstName, string lastName, int clubId, bool isCoach = false, int birthYear = 2000) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            FirstName = firstName,
            LastName = lastName,
            ClubId = clubId,
            IsCoach = isCoach,
            IsActive = true,
            DateOfBirth = new DateOnly(birthYear, 6, 15),
            StartedOn = new DateOnly(2024, 1, 1)
        };

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
