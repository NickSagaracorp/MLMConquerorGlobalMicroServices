using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;

/// <summary>
/// Admin removes a placement without time-window or opportunity restrictions. The descendants stay linked to
/// the removed member (ParentMemberId) and wait under a floating root of their own; when the member is placed
/// again (AdminPlaceMember) the whole subtree comes back with it. Ghost points are NOT transferred.
/// Plan P: arbol en exclusivo; DualTeamNodes resta el subarbol de la linea ascendente de forma incremental (C2),
/// reescribe la clave y la ruta de los descendientes por el rango de la clave (C1) y borra el nodo.
/// </summary>
public class AdminRemovePlacementHandler : IRequestHandler<AdminRemovePlacementCommand, Result<string>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public AdminRemovePlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<string>> Handle(AdminRemovePlacementCommand command, CancellationToken ct)
    {
        var now = _clock.Now;

        if (!await _db.DualTeamTree.AnyAsync(d => d.MemberId == command.MemberId, ct))
            return Result<string>.Failure("NOT_PLACED", "El miembro no tiene placement activo.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            if (!await _db.DualTeamTree.AnyAsync(d => d.MemberId == command.MemberId, ct))
            {
                await tx.RollbackAsync(ct);
                return Result<string>.Failure("NOT_PLACED", "El miembro no tiene placement activo.");
            }

            await DualTeamNodes.RemoveAsync(_db, command.MemberId, DualTeamChildrenOnRemoval.Floating, now, _currentUser.UserId, ct);

            // Log the removal
            var prevLog = await _db.PlacementLogs.AsNoTracking()
                .Where(p => p.MemberId == command.MemberId)
                .OrderByDescending(p => p.CreationDate)
                .FirstOrDefaultAsync(ct);

            _db.PlacementLogs.Add(new PlacementLog
            {
                MemberId            = command.MemberId,
                PlacedUnderMemberId = string.Empty,
                Side                = Domain.Enums.TreeSide.Left, // sentinel — removal has no target side
                Action              = "Removed",
                Reason              = "Admin removal",
                UnplacementCount    = prevLog?.UnplacementCount ?? 0, // admin does not increment
                FirstPlacementDate  = prevLog?.FirstPlacementDate,
                CreationDate        = now,
                CreatedBy           = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<string>.Success("Placement eliminado exitosamente por el administrador.");
        }
        catch (DualTeamBusyException)
        {
            await tx.RollbackAsync(ct);
            return Result<string>.Failure("PLACEMENT_BUSY",
                "Hay otra colocacion en curso en el arbol. Intentalo de nuevo en unos segundos.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
