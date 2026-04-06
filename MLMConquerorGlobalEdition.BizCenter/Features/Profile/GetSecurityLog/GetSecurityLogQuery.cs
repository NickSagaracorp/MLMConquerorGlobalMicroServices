using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetSecurityLog;

public record GetSecurityLogQuery(int Page, int PageSize)
    : IRequest<Result<PagedResult<SecurityLogDto>>>;
