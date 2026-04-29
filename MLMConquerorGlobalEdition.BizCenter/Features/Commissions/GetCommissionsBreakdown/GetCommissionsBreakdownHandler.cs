using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsBreakdown;

public class GetCommissionsBreakdownHandler : IRequestHandler<GetCommissionsBreakdownQuery, Result<List<CommissionBreakdownDto>>>
{
    private readonly ICommissionsService _service;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsBreakdownHandler(ICommissionsService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionBreakdownDto>>> Handle(GetCommissionsBreakdownQuery request, CancellationToken ct)
    {
        var items = await _service.GetBreakdownAsync(
            _currentUser.MemberId, request.PaymentDate, request.EarnedDate, ct);

        var mapped = items.Select(v => new CommissionBreakdownDto
        {
            CommissionTypeName = v.CommissionTypeName,
            Detail             = v.Detail,
            Amount             = v.Amount
        }).ToList();

        return Result<List<CommissionBreakdownDto>>.Success(mapped);
    }
}
