using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Tests.UnitTests;

/// <summary>
/// The substitutes every service unit test needs. These tests exist alongside the endpoint suite
/// rather than instead of it: an endpoint test proves the pipeline enforces a rule, a unit test
/// proves the service enforces it even when no attribute is in front of it — which is what
/// stops a rule being lost the day a new caller forgets the attribute.
/// </summary>
internal static class TestDoubles
{
    /// <summary>
    /// A <see cref="UserManager{TUser}"/> that resolves exactly the users handed to it.
    /// It needs nine constructor arguments even though only the store is ever touched.
    /// </summary>
    public static UserManager<ApplicationUser> UserManagerFor(params ApplicationUser[] users)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        manager.FindByIdAsync(Arg.Any<string>())
            .Returns(call => users.FirstOrDefault(u => u.Id == call.Arg<string>()));

        // Default to success: a service under test that calls UpdateAsync would otherwise
        // await a null Task. Tests that care about failure re-stub it.
        manager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);

        manager.FindByEmailAsync(Arg.Any<string>())
            .Returns(call => users.FirstOrDefault(u =>
                string.Equals(u.Email, call.Arg<string>(), StringComparison.OrdinalIgnoreCase)));

        return manager;
    }

    /// <summary>A validator that accepts anything — the service rule is what is under test.</summary>
    public static IValidator<T> PassingValidator<T>()
    {
        var validator = Substitute.For<IValidator<T>>();
        validator.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return validator;
    }

    /// <summary>A validator that always rejects, to prove the service stops before doing work.</summary>
    public static IValidator<T> FailingValidator<T>(string property, string error)
    {
        var validator = Substitute.For<IValidator<T>>();
        validator.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure(property, error)]));
        return validator;
    }

    public static ILogger<T> Logger<T>() => Substitute.For<ILogger<T>>();

    public static IOptions<ClientSettings> ClientSettings(string baseUrl = "http://localhost:4200") =>
        Options.Create(new ClientSettings { BaseUrl = baseUrl });

    /// <summary>
    /// A club member. Ids default to a fresh GUID so a test never accidentally depends on two
    /// users sharing one.
    /// </summary>
    public static ApplicationUser User(
        string? id = null,
        int? clubId = 1,
        bool isCoach = false,
        string email = "person@test.local",
        string firstName = "Test",
        string lastName = "Person",
        bool isActive = true) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            ClubId = clubId,
            IsCoach = isCoach,
            IsActive = isActive,
            DateOfBirth = new DateOnly(2000, 1, 1),
            StartedOn = new DateOnly(2024, 1, 1)
        };
}
