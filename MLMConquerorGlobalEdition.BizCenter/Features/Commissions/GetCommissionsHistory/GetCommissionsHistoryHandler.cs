using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsHistory;

public class GetCommissionsHistoryHandler : IRequestHandler<GetCommissionsHistoryQuery, Result<List<CommissionHistoryYearDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsHistoryHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionHistoryYearDto>>> Handle(GetCommissionsHistoryQuery request, CancellationToken ct)
    {
        var years = await _service.GetHistoryAsync(_currentUser.MemberId, ct);

        var mapped = years.Select(y => new CommissionHistoryYearDto
        {
            Year        = y.Year,
            TotalIncome = y.TotalIncome,
            Months      = y.Months.Select(m => new CommissionHistoryMonthDto
            {
                MonthNo     = m.MonthNo,
                MonthName   = m.MonthName,
                TotalIncome = m.TotalIncome
            }).ToList()
        }).ToList();

        return Result<List<CommissionHistoryYearDto>>.Success(mapped);
    }
}
