using FluentValidation;
using TCM.Application.Dtos.Notes;

namespace TCM.Application.Validation;

/// <summary>
/// Runs at the service boundary, like every other validator here. The maximum lengths match the
/// column widths in <c>NoteConfiguration</c>, so an over-long note is refused with a readable
/// message instead of a truncation error from SQL Server.
/// </summary>
public class CreateNoteDtoValidator : AbstractValidator<CreateNoteDto>
{
    public CreateNoteDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ToMemberId).NotEmpty().WithMessage("The note must say who it is about.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Priority must be Low, Medium or High.");
        RuleFor(x => x.TrainingId).GreaterThan(0).When(x => x.TrainingId.HasValue);
    }
}
