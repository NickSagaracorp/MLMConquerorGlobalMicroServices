using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;

public class UnblockFingerprintRequestValidator : AbstractValidator<UnblockFingerprintRequest>
{
    public UnblockFingerprintRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.VisitorId) || !string.IsNullOrWhiteSpace(x.IpAddress))
                .WithMessage("Either VisitorId or IpAddress must be provided.");

        RuleFor(x => x.VisitorId)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.VisitorId));

        RuleFor(x => x.IpAddress)
            .MaximumLength(45)
            .When(x => !string.IsNullOrEmpty(x.IpAddress));

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for the audit trail.")
            .MaximumLength(500);
    }
}
