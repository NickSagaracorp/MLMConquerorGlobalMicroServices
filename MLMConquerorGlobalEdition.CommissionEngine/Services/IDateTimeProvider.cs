namespace MLMConquerorGlobalEdition.CommissionEngine.Services;

/// <summary>
/// Local provider exists for backwards compatibility with handlers that import this
/// namespace directly. Inherits the SharedKernel contract so a single DI registration
/// satisfies both the local and SharedKernel-typed consumers (e.g. DailyResidualConsolidationJob).
/// Matches the pattern in BizCenter, TicketManagementSystem and SharedAPICenter.
/// </summary>
public interface IDateTimeProvider : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
{
}
