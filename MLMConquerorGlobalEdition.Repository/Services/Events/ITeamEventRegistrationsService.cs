using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <summary>Eventos del equipo de inscripción (Plan J, bloque 6). Quien llama pasa el miembro del token.</summary>
public interface ITeamEventRegistrationsService
{
    /// <summary>Eventos activos (IsActive y no borrados), del más reciente al más antiguo, 50 como mucho.</summary>
    Task<List<TeamEventView>> GetActiveEventsAsync(CancellationToken ct = default);

    Task<Result<PagedResult<TeamEventRegistrationView>>> GetGridAsync(
        string memberId, string eventId, bool registeredOnly, GridDataRequest request, CancellationToken ct = default);

    Task<Result<TeamEventTotalsView>> GetTotalsAsync(string memberId, string eventId, CancellationToken ct = default);
}
