using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Commissions;

/// <summary>
/// Single source of truth for commission queries. Used by both BizCenter (member's
/// own view) and Admin (member profile drill-down). Do NOT duplicate any of these
/// queries elsewhere — fix once, fixes both.
/// </summary>
public interface ICommissionsService
{
    Task<CommissionSummaryView> GetSummaryAsync(
        string memberId, CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetCommissionsAsync(
        string memberId, int page, int pageSize,
        string? status, DateTime? from, DateTime? to,
        CancellationToken ct = default);

    Task<List<CommissionHistoryYearView>> GetHistoryAsync(
        string memberId, CancellationToken ct = default);

    Task<List<CommissionBreakdownView>> GetBreakdownAsync(
        string memberId, DateTime paymentDate, DateTime? earnedDate,
        CancellationToken ct = default);

    Task<List<CommissionMonthBreakdownView>> GetMonthBreakdownAsync(
        string memberId, int year, int month,
        CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetDualResidualAsync(
        string memberId, int page, int pageSize,
        CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetFastStartBonusAsync(
        string memberId, int page, int pageSize,
        CancellationToken ct = default);

    Task<CommissionBonusSummaryView> GetFastStartBonusSummaryAsync(
        string memberId, CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetPresidentialBonusAsync(
        string memberId, int page, int pageSize,
        CancellationToken ct = default);

    Task<CommissionBonusSummaryView> GetPresidentialBonusSummaryAsync(
        string memberId, CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetBoostBonusAsync(
        string memberId, int page, int pageSize,
        CancellationToken ct = default);

    Task<BoostBonusMemberSummaryView> GetBoostBonusMemberSummaryAsync(
        string memberId, CancellationToken ct = default);

    Task<BoostBonusWeekStatsView> GetBoostBonusWeekStatsAsync(
        string memberId, CancellationToken ct = default);

    Task<PagedResult<CommissionEarningView>> GetCarBonusAsync(
        string memberId, int page, int pageSize,
        DateTime? from, DateTime? to,
        CancellationToken ct = default);

    Task<CarBonusStatsView> GetCarBonusStatsAsync(
        string memberId, CancellationToken ct = default);

    Task<List<CarBonusAmbassadorView>> GetCarBonusAmbassadorsAsync(
        string memberId, DateTime? from, DateTime? to,
        CancellationToken ct = default);

    Task<CarBonusBranchView> GetCarBonusBranchAsync(
        string branchMemberId, CancellationToken ct = default);
}
