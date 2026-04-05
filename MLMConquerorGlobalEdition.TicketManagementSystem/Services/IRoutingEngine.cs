using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

public interface IRoutingEngine
{
    /// <summary>
    /// Attempts to assign the ticket to an agent based on routing rules.
    /// Returns the resolved AgentId, or null if the ticket should go to the team queue.
    /// Also sets AssignedTeamId on the ticket.
    /// </summary>
    Task<RoutingResult> RouteAsync(Ticket ticket, CancellationToken ct = default);
}

public record RoutingResult(string? AgentId, int? TeamId, bool FallbackToSupervisor, string? SupervisorAgentId);
