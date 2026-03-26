using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Services;

/// <summary>
/// System clock wrapper. Inject IDateTimeProvider instead of using
/// DateTime.Now or DateTime.UtcNow directly — enables deterministic unit testing.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.UtcNow;
}
