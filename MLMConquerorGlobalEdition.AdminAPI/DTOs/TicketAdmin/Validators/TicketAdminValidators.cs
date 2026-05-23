using FluentValidation;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Validation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin.Validators;

public class AdminAddCommentRequestValidator : AbstractValidator<AdminAddCommentRequest>
{
    public AdminAddCommentRequestValidator()
    {
        // Ticket comments are markdown-ish — allow most printable text but
        // cap length and block null bytes; richer sanitisation happens in
        // the handler.
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(@"^[^\x00]+$").WithMessage("Content contains forbidden control characters.");
    }
}

public class AdminAssignTicketRequestValidator : AbstractValidator<AdminAssignTicketRequest>
{
    public AdminAssignTicketRequestValidator()
    {
        RuleFor(x => x.AssignedToUserId)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(AdminValidationPatterns.UserIdPattern)
                .WithMessage("AssignedToUserId contains invalid characters.");
    }
}

public class AdminCreateTicketRequestValidator : AbstractValidator<AdminCreateTicketRequest>
{
    public AdminCreateTicketRequestValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(AdminValidationPatterns.MemberIdPattern);

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.SubjectMaxLength)
            .Matches(@"^[^\x00<>]+$").WithMessage("Subject contains invalid characters.");

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(@"^[^\x00]+$").WithMessage("Body contains forbidden control characters.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class AdminResolveTicketRequestValidator : AbstractValidator<AdminResolveTicketRequest>
{
    public AdminResolveTicketRequestValidator()
    {
        RuleFor(x => x.ResolutionNotes)
            .MaximumLength(AdminValidationPatterns.LongTextMaxLength)
            .Matches(@"^[^\x00]+$").WithMessage("ResolutionNotes contains forbidden control characters.")
            .When(x => !string.IsNullOrEmpty(x.ResolutionNotes));
    }
}

public class AdminUpdateTicketRequestValidator : AbstractValidator<AdminUpdateTicketRequest>
{
    public AdminUpdateTicketRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
    }
}
