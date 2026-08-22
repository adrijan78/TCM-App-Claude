using FluentValidation;
using TCM.Application.Dtos.Account;

namespace TCM.Application.Validation;

/// <summary>
/// Validation runs at the service boundary, not through model binding, so a rule cannot be
/// skipped by adding a new controller that forgets to check <c>ModelState</c>.
/// </summary>
public class MemberRegisterDtoValidator : AbstractValidator<MemberRegisterDto>
{
    public MemberRegisterDtoValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        // Length only. The strength rules live in Identity's password options, and duplicating
        // them here would mean two places to change and two chances to disagree.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);

        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.BeltId).GreaterThan(0);

        RuleFor(x => x.DateOfBirth)
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.")
            .GreaterThan(new DateOnly(1900, 1, 1));

        RuleFor(x => x.Height).InclusiveBetween(50m, 250m).When(x => x.Height.HasValue)
            .WithMessage("Height must be between 50 and 250 cm.");
        RuleFor(x => x.Weight).InclusiveBetween(10m, 300m).When(x => x.Weight.HasValue)
            .WithMessage("Weight must be between 10 and 300 kg.");
    }
}

public class LoginMemberDtoValidator : AbstractValidator<LoginMemberDto>
{
    public LoginMemberDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("The two passwords do not match.");
    }
}

public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
