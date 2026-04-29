using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.Repository.Services.Commissions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCarBonusBranch;

public class GetCarBonusBranchHandler : IRequestHandler<GetCarBonusBranchQuery, Result<CarBonusBranchDto>>
{
    private readonly ICommissionsService _service;

    public GetCarBonusBranchHandler(ICommissionsService service) => _service = service;

    public async Task<Result<CarBonusBranchDto>> Handle(GetCarBonusBranchQuery request, CancellationToken ct)
    {
        var v = await _service.GetCarBonusBranchAsync(request.BranchMemberId, ct);

        var dto = new CarBonusBranchDto
        {
            Members = v.Members.Select(m => new CarBonusBranchMemberDto
            {
                OrderNo         = m.OrderNo,
                FullName        = m.FullName,
                MembershipLevel = m.MembershipLevel,
                ExpirationDate  = m.ExpirationDate,
                Points          = m.Points
            }).ToList()
        };

        return Result<CarBonusBranchDto>.Success(dto);
    }
}
