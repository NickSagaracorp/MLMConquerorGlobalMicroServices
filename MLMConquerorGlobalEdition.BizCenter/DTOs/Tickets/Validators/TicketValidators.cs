using FluentValidation;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Validation;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.SubjectMaxLength)
            .Matches(@"^[^\x00<>]+$")
                .WithMessage("Subject contains invalid characters.");

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.LongTextMaxLength)
            .Matches(@"^[^\x00]+$")
                .WithMessage("Body contains forbidden control characters.");

        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(BizCenterValidationPatterns.LongTextMaxLength)
            .Matches(@"^[^\x00]+$")
                .WithMessage("Content contains forbidden control characters.");
    }
}
