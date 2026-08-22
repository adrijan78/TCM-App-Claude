using FluentValidation;
using TCM.Application.Dtos.Trainings;
using TCM.Domain.Enums;

namespace TCM.Application.Validation;

/// <summary>
/// The add/edit training form of SPEC section 6.5. Validation runs at the service boundary, so
/// these rules apply to both the create and the update path without being repeated.
/// </summary>
public class EditTrainingDtoValidator : AbstractValidator<EditTrainingDto>
{
    public EditTrainingDtoValidator()
    {
        // 300 characters is the column width configured in TrainingConfiguration.
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);

        RuleFor(x => x.Date).NotEmpty().WithMessage("A training date is required.");

        RuleFor(x => x.TrainingType).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.MemberIds).NotNull().WithMessage("Provide the list of invited members.");

        RuleForEach(x => x.MemberIds).NotEmpty().WithMessage("An invited member id cannot be blank.");
    }
}

public class ReportAttendanceDtoValidator : AbstractValidator<ReportAttendanceDto>
{
    public ReportAttendanceDtoValidator()
    {
        RuleFor(x => x.Status).IsInEnum();

        // Reporting a member back to Invited is not a thing the screen can do, and it would
        // silently discard a reported absence reason.
        RuleFor(x => x.Status).NotEqual(AttendanceStatus.Invited)
            .WithMessage("Report either Present or Absent.");

        // Matches the Attendances.Description column width.
        RuleFor(x => x.AbsenceReason).MaximumLength(500);

        RuleFor(x => x.AbsenceReason).NotEmpty()
            .When(x => x.Status == AttendanceStatus.Absent)
            .WithMessage("Give a reason for the absence.");
    }
}

public class SetPerformanceDtoValidator : AbstractValidator<SetPerformanceDto>
{
    public SetPerformanceDtoValidator()
    {
        RuleFor(x => x.Performance).InclusiveBetween(0, 10)
            .WithMessage("Performance must be a score between 0 and 10.");
    }
}
