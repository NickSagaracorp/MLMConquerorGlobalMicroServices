using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.RunAutoPlacement;

/// <summary>
/// Admin-triggered auto-placement run. Idempotent: only processes members not yet in the dual tree.
/// Plan P: delega en el motor unico (<see cref="IPlacementService"/>), que coloca de uno en uno bajo el
/// candado del hueco. La version anterior tenia su propia regla (BFS por anchura), leia con AsNoTracking
/// dentro del bucle y guardaba una sola vez al final, asi que dos patrocinados del mismo patrocinador
/// caian en el mismo lado (diagnostico-huecos-binario.md §2).
/// </summary>
public class RunAutoPlacementHandler : IRequestHandler<RunAutoPlacementCommand, Result<RunAutoPlacementResult>>
{
    private const int PlacementWindowDays = 30;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;
    private readonly IPlacementService   _placement;

    public RunAutoPlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock,
        IPlacementService   placement)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
        _placement   = placement;
    }

    public async Task<Result<RunAutoPlacementResult>> Handle(
        RunAutoPlacementCommand request, CancellationToken ct)
    {
        var windowCutoff = _clock.Now.AddDays(-PlacementWindowDays);

        // Los pendientes con el plazo vencido (PendingPlacements, el mismo criterio que AutoPlacementJob): sin
        // nodo, o raiz provisional con equipo cuyo patrocinador ya esta en el binario. Por fecha de alta: un
        // patrocinador que tambien esta pendiente se coloca antes que sus patrocinados.
        var toPlace = await PendingPlacements.Query(_db, windowCutoff).ToListAsync(ct);

        var audit = new PlacementAudit(_currentUser.UserId, "AutoPlaced", "Admin-triggered auto-placement");
        var placedIds = new List<string>();
        foreach (var member in toPlace)
        {
            // Como antes: un miembro cuyo patrocinador no esta en el arbol se salta (el motor lo crearia
            // como raiz nueva).
            if (!await _db.DualTeamTree.AsNoTracking().AnyAsync(d => d.MemberId == member.SponsorMemberId, ct))
                continue;

            var r = await _placement.PlaceAsync(member.MemberId, member.SponsorMemberId, audit, ct);
            if (r.IsSuccess && !r.Value!.AlreadyPlaced)
                placedIds.Add(member.MemberId);
        }

        return Result<RunAutoPlacementResult>.Success(new RunAutoPlacementResult(placedIds.Count, placedIds));
    }
}
