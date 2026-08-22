using FluentValidation;
using TCM.Application.Dtos.Members;

namespace TCM.Application.Validation;

/// <summary>
/// The "Edit Data" form of SPEC section 6.4. Rules match
/// <see cref="MemberRegisterDtoValidator"/> field for field, so a value that was accepted at
/// registration is not rejected on the first edit.
/// </summary>
public class EditMemberDtoValidator : AbstractValidator<EditMemberDto>
{
    public EditMemberDtoValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        RuleFor(x => x.PhoneNumber).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

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

/// <summary>The coach-only "add belt exam" form of SPEC section 6.4.</summary>
public class AddMemberBeltDtoValidator : AbstractValidator<AddMemberBeltDto>
{
    public AddMemberBeltDtoValidator()
    {
        RuleFor(x => x.BeltId).GreaterThan(0);

        // A promotion cannot be dated in the future — the exam either happened or it did not.
        RuleFor(x => x.DateReceived)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The exam date cannot be in the future.")
            .GreaterThan(new DateOnly(1900, 1, 1));

        RuleFor(x => x.Description).MaximumLength(500);
    }
}
