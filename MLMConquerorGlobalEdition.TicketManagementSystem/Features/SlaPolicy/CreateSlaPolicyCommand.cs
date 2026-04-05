using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.SlaPolicy;

public record CreateSlaPolicyCommand(CreateSlaPolicyRequest Request) : IRequest<Result<SlaPolicyDto>>;
public record UpdateSlaPolicyCommand(string PolicyId, CreateSlaPolicyRequest Request) : IRequest<Result<bool>>;
public record DeleteSlaPolicyCommand(string PolicyId) : IRequest<Result<bool>>;
public record GetSlaPoliciesQuery() : IRequest<Result<IEnumerable<SlaPolicyDto>>>;
