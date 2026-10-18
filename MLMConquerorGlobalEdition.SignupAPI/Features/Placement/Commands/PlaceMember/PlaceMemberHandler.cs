using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;

/// <summary>
/// El miembro elige el lado y se le coloca en el hueco menos profundo de ese lado bajo el nodo pedido.
/// Plan P:
/// <list type="bullet">
/// <item>Busqueda, candado del hueco, nueva comprobacion e insercion en una transaccion; el arbol se toma en
/// compartido ANTES de buscar (I1) y se reintenta si otro escritor ocupa el hueco elegido.</item>
/// <item>Una raiz provisional (tiene nodo, no tiene padre) se coloca con todo su equipo; el arbol en exclusivo
/// desde el principio. Una raiz del sistema no se coloca; un nodo con padre ya esta colocado.</item>
/// <item>El nodo pedido tiene que estar en la red de patrocinio de su patrocinador (I8), como en el BizCenter.</item>
/// <item>Sin tope de 1.500 de la ruta (I3): el limite real es el de la clave compacta (13.567 niveles).</item>
/// <item>Piernas incrementales (C2) y una sola evaluacion de rango y un solo aviso a la linea (I4): con 8.000
/// ascendientes no se encola ni se avisa a cada uno.</item>
/// </list>
/// </summary>
public class PlaceMemberHandler : IRequestHandler<PlaceMemberCommand, Result<bool>>
{
    /// <summary>Busquedas de hueco cuando otro escritor ocupa el elegido entre la busqueda y el candado.</summary>
    private const int MaxIntentos = 10;

    private readonly AppDbContext             _db;
    private readonly IDateTimeProvider        _dateTime;
    private readonly IPushNotificationService _push;

    public PlaceMemberHandler(
        AppDbContext             db,
        IDateTimeProvider        dateTime,
        IPushNotificationService push)
    {
        _db       = db;
        _dateTime = dateTime;
        _push     = push;
    }

    public async Task<Result<bool>> Handle(PlaceMemberCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        // Validate 30-day placement window
        if ((now - member.EnrollDate).TotalDays > 30)
            throw new PlacementWindowExpiredException();

        var parent = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.MemberId == command.PlaceUnderMemberId, ct);
        if (parent is null)
            return Result<bool>.Failure("PARENT_MEMBER_NOT_FOUND", $"Member '{command.PlaceUnderMemberId}' not found.");

        if (command.PlaceUnderMemberId == command.MemberId)
            return Result<bool>.Failure("AUTO_SUPERIORITY", "A member cannot be placed under itself.");

        // I8: no se cuelga a nadie bajo cualquier nodo. El nodo pedido es el patrocinador o alguien de su red de
        // patrocinio (lo mismo que ofrece GetAvailableNodes en el BizCenter). El personal queda fuera de la regla.
        if (!command.ByStaff && !await IsInSponsorNetworkAsync(member.SponsorMemberId, command.PlaceUnderMemberId, ct))
            return Result<bool>.Failure("PLACEMENT_TARGET_NOT_ALLOWED",
                $"'{command.PlaceUnderMemberId}' is not in the sponsorship network of your sponsor.");

        var existing = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == command.MemberId, ct);
        if (existing is not null && existing.ParentMemberId is not null)
            return Result<bool>.Failure("ALREADY_PLACED", $"Member '{command.MemberId}' is already placed in the dual tree.");
        if (existing is not null && member.SponsorMemberId is null)
            return Result<bool>.Failure("ROOT_CANNOT_BE_PLACED", $"Member '{command.MemberId}' is a system root.");
        var esRaizProvisional = existing is not null;

        var side = Enum.Parse<TreeSide>(command.Side);

        string? parentMemberId = null;
        for (var intento = 1; intento <= MaxIntentos && parentMemberId is null; intento++)
        {
            var nuevos = new List<object>();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (esRaizProvisional) await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
                else await DualTeamGate.EnterTreeSharedAsync(_db, ct);

                // El estado del miembro, con el candado.
                var ahora = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == command.MemberId, ct);
                if ((ahora is not null) != esRaizProvisional || ahora?.ParentMemberId is not null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<bool>.Failure("ALREADY_PLACED", $"Member '{command.MemberId}' is already placed in the dual tree.");
                }

                // El nodo pedido sin nodo propio: entra como raiz provisional (como hace el motor con un patrocinador).
                var raiz = await _db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == command.PlaceUnderMemberId, ct);
                if (raiz is null)
                {
                    raiz = await DualTeamNodes.RootAsync(_db, command.PlaceUnderMemberId, now, command.MemberId, ct);
                    nuevos.Add(_db.DualTeamTree.Add(raiz).Entity);
                    await _db.SaveChangesAsync(ct);
                }

                var slot = await FindFirstEmptySlotAsync(raiz, side, ct);
                if (slot is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<bool>.Failure("NO_AVAILABLE_SLOT",
                        $"No available {command.Side} slot found under '{command.PlaceUnderMemberId}'.");
                }

                if (!esRaizProvisional) await DualTeamGate.EnterSlotAsync(_db, slot.MemberId, side, ct);
                if (await _db.DualTeamTree.AnyAsync(d => d.ParentMemberId == slot.MemberId && d.Side == side, ct))
                {
                    await tx.RollbackAsync(ct);
                    Detach(nuevos);
                    continue;   // otro escritor lo ocupo: se busca el siguiente hueco
                }

                if (esRaizProvisional)
                {
                    if (ahora!.BinaryPath is not null && slot.BinaryPath is not null && DualBinaryPath.Contains(ahora.BinaryPath, slot.BinaryPath))
                    {
                        await tx.RollbackAsync(ct);
                        return Result<bool>.Failure("CIRCULAR_REFERENCE", $"'{command.PlaceUnderMemberId}' is inside the team of '{command.MemberId}'.");
                    }
                    await DualTeamNodes.MoveSubtreeAsync(_db, command.MemberId,
                        new DualTeamDestination.UnderParent(slot, side), adjustLegs: true, now, command.MemberId, ct);
                }
                else
                {
                    var node = await DualTeamNodes.ChildAsync(_db, slot, command.MemberId, side, now, command.MemberId, ct);
                    nuevos.Add(_db.DualTeamTree.Add(node).Entity);
                    await _db.SaveChangesAsync(ct);
                    await LegPointMaintenance.ApplyIncrementalAsync(_db, new[] { command.MemberId }, ct);
                }

                nuevos.Add(_db.PlacementLogs.Add(new PlacementLog
                {
                    MemberId            = command.MemberId,
                    PlacedUnderMemberId = slot.MemberId,
                    Side                = side,
                    Action              = "Placed",
                    Reason              = esRaizProvisional ? "Provisional root moved with its team" : null,
                    FirstPlacementDate  = now,
                    CreationDate        = now,
                    CreatedBy           = command.MemberId
                }).Entity);

                // I4: una sola evaluacion de rango, la del padre directo (antes, una fila por ascendiente).
                nuevos.Add(_db.RankEvaluationQueue.Add(new RankEvaluationQueue
                {
                    TriggerMemberId  = command.MemberId,
                    EvaluateMemberId = slot.MemberId,
                    TriggerEvent     = RankEvaluationTrigger.Placement,
                    TriggerDate      = now,
                    CreatedBy        = command.MemberId,
                    CreationDate     = now
                }).Entity);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                parentMemberId = slot.MemberId;
            }
            catch (DbUpdateException ex) when (DualTeamSlotTaken.Is(ex))
            {
                await tx.RollbackAsync(ct);   // la red del indice unico de hueco: se busca otro
                Detach(nuevos);
            }
            catch (DualTeamBusyException ex)
            {
                await tx.RollbackAsync(ct);
                return Result<bool>.Failure("PLACEMENT_BUSY", ex.Message);
            }
        }

        if (parentMemberId is null)
            return Result<bool>.Failure("PLACEMENT_CONFLICT",
                $"The {command.Side} slots under '{command.PlaceUnderMemberId}' kept changing; retry later.");

        // Notify the placed member and its direct upline (I4: no a cada uno de los ascendientes).
        _ = _push.SendAsync(
            command.MemberId,
            NotificationEvents.PlacementCompleted,
            "Placement Completed",
            $"You have been placed in the {command.Side} position of your team.",
            ct);
        _ = _push.SendAsync(
            parentMemberId,
            NotificationEvents.DownlinePlaced,
            "New Team Placement",
            $"A new member has been placed in your downline ({command.Side} leg).",
            ct);

        return Result<bool>.Success(true);
    }

    private void Detach(List<object> nuevos)
    {
        foreach (var e in nuevos) _db.Entry(e).State = EntityState.Detached;
        nuevos.Clear();
    }

    /// <summary>¿Es <paramref name="target"/> el patrocinador o alguien de su red de patrocinio?</summary>
    private async Task<bool> IsInSponsorNetworkAsync(string? sponsorId, string target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sponsorId)) return false;
        if (target == sponsorId) return true;
        var nodo = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(sponsorId, ct);
        if (nodo is null) return false;
        return await _db.GenealogyTree.AsNoTracking().CurrentUnder(nodo).AnyAsync(g => g.MemberId == target, ct);
    }

    /// <summary>
    /// El hueco menos profundo del lado pedido bajo <paramref name="root"/> (regla del producto: el miembro elige el
    /// lado). Por adyacencia (ParentMemberId), nivel a nivel, y se detiene en el primer nivel con hueco; dentro de un
    /// nivel, por (CreationDate, Id). Sin filtro por ruta y sin tope de longitud (I3).
    /// </summary>
    private async Task<DualTeamEntity?> FindFirstEmptySlotAsync(DualTeamEntity root, TreeSide requiredSide, CancellationToken ct)
    {
        var nivel = new List<DualTeamEntity> { root };
        while (nivel.Count > 0)
        {
            var ids = nivel.Select(n => n.MemberId).ToList();
            var hijos = await _db.DualTeamTree.AsNoTracking()
                .Where(d => d.ParentMemberId != null && ids.Contains(d.ParentMemberId))
                .ToListAsync(ct);

            var ocupados = hijos.Where(h => h.Side == requiredSide).Select(h => h.ParentMemberId!).ToHashSet();
            var libre = nivel.FirstOrDefault(n => !ocupados.Contains(n.MemberId));
            if (libre is not null) return libre;

            nivel = hijos.OrderBy(h => h.CreationDate).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        }
        return null;
    }
}
