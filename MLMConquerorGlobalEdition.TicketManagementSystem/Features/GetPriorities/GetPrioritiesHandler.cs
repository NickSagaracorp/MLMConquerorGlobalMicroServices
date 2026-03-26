using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetPriorities;

public class GetPrioritiesHandler : IRequestHandler<GetPrioritiesQuery, Result<IEnumerable<PriorityDto>>>
{
    public Task<Result<IEnumerable<PriorityDto>>> Handle(GetPrioritiesQuery request, CancellationToken ct)
    {
        var priorities = Enum.GetValues<TicketPriority>()
            .Select(p => new PriorityDto
            {
                Id = (int)p,
                Name = p.ToString()
            })
            .AsEnumerable();

        return Task.FromResult(Result<IEnumerable<PriorityDto>>.Success(priorities));
    }
}
