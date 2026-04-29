using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetCredentialsHistory;

public record GetCredentialsHistoryQuery(int Page, int PageSize) : IRequest<Result<PagedResult<CredentialChangeDto>>>;
