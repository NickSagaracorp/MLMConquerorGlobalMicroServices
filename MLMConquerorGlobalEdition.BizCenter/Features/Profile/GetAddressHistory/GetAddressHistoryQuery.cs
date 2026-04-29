using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetAddressHistory;

public record GetAddressHistoryQuery(int Page, int PageSize) : IRequest<Result<PagedResult<AddressHistoryDto>>>;
