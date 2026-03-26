using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;

public record UpdateMemberStatusCommand(string MemberId, UpdateMemberStatusRequest Request)
    : IRequest<Result<AdminMemberDto>>;
