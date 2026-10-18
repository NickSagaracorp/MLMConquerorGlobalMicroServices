using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;
using IDateTimeProvider = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;

public class RemovePlacementHandler : IRequestHandler<RemovePlacementCommand, Result<RemovePlacementResult>>
{
    private const int CorrectionWindowHours     = 72;
    private const int MaxPlacementOpportunities = 2;

    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _clock;

    public RemovePlacementHandler(
        AppDbContext        db,
        ICurrentUserService currentUser,
        IDateTimeProvider   clock)
    {
        _db          = db;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result<RemovePlacementResult>> Handle(
        RemovePlacementCommand command, CancellationToken ct)
    {
        // Hora del servidor (la regla del proyecto), como FirstPlacementDate.
        var now     = _clock.Now;
        var isAdmin = _currentUser.IsAdmin;

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberToRemoveId && !m.IsDeleted, ct);

        if (member is null)
            return Result<RemovePlacementResult>.Failure("MEMBER_NOT_FOUND",
                "El miembro no existe.");

        var node = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == command.MemberToRemoveId, ct);

        if (node is null)
            return Result<RemovePlacementResult>.Failure("NOT_PLACED",
                "El miembro no tiene placement asignado en el Dual Team.");

        var log = await _db.PlacementLogs
            .Where(p => p.MemberId == command.MemberToRemoveId)
            .OrderByDescending(p => p.CreationDate)
            .FirstOrDefaultAsync(ct);

        var opportunitiesUsed = log?.UnplacementCount ?? 0;

        if (!isAdmin)
        {
            if (opportunitiesUsed >= MaxPlacementOpportunities)
                throw new UnplacementLimitExceededException();

            if (log?.FirstPlacementDate is null ||
                now > log.FirstPlacementDate.Value.AddHours(CorrectionWindowHours))
                throw new UnplacementWindowExpiredException();
        }

        var parentMemberId = node.ParentMemberId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Plan P: una retirada reescribe el arbol: arbol en exclusivo (espera a las colocaciones en curso y
            // las detiene). DualTeamNodes resta el subarbol de la linea ascendente de forma incremental (C2: el
            // recalculo por ruta era O(profundidad x N) con el arbol bloqueado), deja a cada hijo como raiz
            // provisional con su equipo y borra el nodo.
            await DualTeamGate.EnterTreeExclusiveAsync(_db, ct);
            if (!await _db.DualTeamTree.AnyAsync(d => d.MemberId == command.MemberToRemoveId, ct))
            {
                await tx.RollbackAsync(ct);
                return Result<RemovePlacementResult>.Failure("NOT_PLACED",
                    "El miembro no tiene placement asignado en el Dual Team.");
            }
            await DualTeamNodes.RemoveAsync(_db, command.MemberToRemoveId, DualTeamChildrenOnRemoval.Roots, now, _currentUser.UserId, ct);

            var removalLog = new PlacementLog
            {
                MemberId            = command.MemberToRemoveId,
                PlacedUnderMemberId = parentMemberId ?? string.Empty,
                Side                = node.Side,
                Action              = "Removed",
                Reason              = isAdmin ? "Admin removal" : "Ambassador correction",
                UnplacementCount    = opportunitiesUsed + 1,
                FirstPlacementDate  = log?.FirstPlacementDate,
                CreationDate        = now,
                CreatedBy           = _currentUser.UserId
            };

            _db.PlacementLogs.Add(removalLog);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var remaining = Math.Max(0, MaxPlacementOpportunities - (opportunitiesUsed + 1));
            return Result<RemovePlacementResult>.Success(new RemovePlacementResult(
                command.MemberToRemoveId,
                $"{member.FirstName} {member.LastName}",
                remaining
            ));
        }
        catch (DualTeamBusyException)
        {
            await tx.RollbackAsync(ct);
            return Result<RemovePlacementResult>.Failure("PLACEMENT_BUSY",
                "Hay otra colocacion en curso en el arbol. Intentalo de nuevo en unos segundos.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
