using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance.Validators;

public class UpdateRecurringPerformanceSettingsRequestValidator
    : AbstractValidator<UpdateRecurringPerformanceSettingsRequest>
{
    public UpdateRecurringPerformanceSettingsRequestValidator()
    {
        RuleFor(x => x.LatencySamplingDays)
            .InclusiveBetween(1, 90);

        RuleFor(x => x.CascadeStrategy)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[A-Za-z][A-Za-z0-9_\-]{0,63}$")
                .WithMessage("CascadeStrategy contains invalid characters.");

        RuleFor(x => x.AggregatorTriggerMode)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[A-Za-z][A-Za-z0-9_\-]{0,63}$")
                .WithMessage("AggregatorTriggerMode contains invalid characters.");

        RuleFor(x => x.Window).NotNull();
        When(x => x.Window is not null, () =>
        {
            RuleFor(x => x.Window.TargetCompletionWindowHours)
                .InclusiveBetween(1, 24);

            RuleFor(x => x.Window.BatchStartTimeUtc)
                .NotEmpty()
                .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
                    .WithMessage("BatchStartTimeUtc must be HH:mm (24-hour UTC).");
        });

        RuleFor(x => x.PerGateway).NotNull();
        RuleForEach(x => x.PerGateway).ChildRules(row =>
        {
            row.RuleFor(g => g.Processor)
                .NotEmpty()
                .MaximumLength(64)
                .Matches(@"^[A-Za-z][A-Za-z0-9_]{0,63}$");
            row.RuleFor(g => g.MinWorkers).InclusiveBetween(0, 1000);
            row.RuleFor(g => g.MaxConcurrency).InclusiveBetween(1, 10_000);
            row.RuleFor(g => g.WindowOffsetMinutes).InclusiveBetween(0, 1440);
        });
    }
}
