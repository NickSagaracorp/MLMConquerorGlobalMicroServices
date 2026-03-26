using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMember;

public record GetMemberQuery(string MemberId) : IRequest<Result<AdminMemberDetailDto>>;
