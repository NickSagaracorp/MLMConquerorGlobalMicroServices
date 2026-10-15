using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;

/// <summary>
/// Distribuye N tokens de un tipo a un miembro del equipo de inscripción. Es N transferencias de
/// instancia de una vez: los MISMOS códigos pasan al destinatario (los más antiguos primero), con
/// la cadena de custodia de <see cref="TransferTokenInstance.TransferTokenInstanceHandler"/>.
/// </summary>
/// <remarks>
/// La versión anterior restaba del agregado del que enviaba y acuñaba códigos nuevos para el que
/// recibía, dejando los del que enviaba en Issued: dos personas podían canjear "el mismo" token. Y
/// aceptaba cualquier destinatario existente, sin mirar el equipo.
///
/// CONCURRENCIA. Elegir los códigos y moverlos va bajo el candado de fondos
/// (<see cref="IMemberFundsGate"/>, <c>member-funds:{memberId}</c>) de los DOS miembros, tomados en
/// orden ordinal: es el mismo candado con el que Billing emite tokens comprados y sube el
/// <see cref="TokenBalance"/>, así que una distribución no se cruza con otra distribución, con una
/// transferencia ni con una compra del que envía o del que recibe. Los códigos se leen DESPUÉS de
/// tomarlo: dos distribuciones a la vez no pueden elegir el mismo código, y la segunda ve lo que dejó
/// la primera (saldo insuficiente, no negativo). <see cref="TokenTransaction"/> no tiene RowVersion y
/// <see cref="TokenBalance"/> no tiene una real; añadirla pediría migración. Ocupado →
/// <see cref="TokenPurchaseErrors.Busy"/> sin escribir nada.
///
/// Para dar un token a alguien FUERA del equipo no hace falta este endpoint: los códigos son al
/// portador y se canjean al inscribirse (ValidateToken no exige relación con el dueño). La pantalla
/// ofrece copiar el código.
/// </remarks>
public class DistributeTokenHandler : IRequestHandler<DistributeTokenCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;
    private readonly IPushNotificationService _push;
    private readonly IMemberFundsGate _gate;

    public DistributeTokenHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache,
        IPushNotificationService push,
        IMemberFundsGate gate)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
        _push        = push;
        _gate        = gate;
    }

    public async Task<Result<bool>> Handle(DistributeTokenCommand command, CancellationToken ct)
    {
        var senderId  = _currentUser.MemberId;
        var req       = command.Request;
        var requested = req.RecipientMemberId.Trim();
        var now       = _dateTime.Now;

        if (string.Equals(senderId, requested, StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure("INVALID_RECIPIENT", "Cannot distribute tokens to yourself.");

        var tokenTypeExists = await _db.TokenTypes.AsNoTracking()
            .AnyAsync(tt => tt.Id == req.TokenTypeId && tt.IsActive, ct);
        if (!tokenTypeExists)
            return Result<bool>.Failure("TOKEN_TYPE_NOT_FOUND", "Token type not found or inactive.");

        // El id tal como está guardado: es el que nombra el candado (member-funds:{id}) y el que escribe
        // Billing; uno escrito con otras mayúsculas sería otro recurso.
        var recipient = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == requested)
            .Select(m => m.MemberId)
            .FirstOrDefaultAsync(ct);
        if (recipient is null)
            return Result<bool>.Failure("RECIPIENT_NOT_FOUND", "Recipient member not found.");

        var inTeam = await TokenCustody.IsInEnrollmentTeamAsync(_db, senderId, recipient, ct);
        if (inTeam is null)
            return Result<bool>.Failure("SENDER_NOT_IN_TREE", "Sender's enrollment node not found.");
        if (inTeam == false)
            return Result<bool>.Failure("RECIPIENT_NOT_IN_DOWNLINE", "Recipient must be a member of your enrollment team.");

        var tx = await TokenCustody.EnterBothAsync(_gate, senderId, recipient, ct);
        if (tx is null)
            return Result<bool>.Failure(TokenPurchaseErrors.Busy,
                "Another token operation is in progress. Please try again in a moment.");

        await using (tx)
        {
            var instances = await _db.TokenTransactions
                .Where(t => t.MemberId == senderId
                         && t.TokenTypeId == req.TokenTypeId
                         && t.ReferenceId != null
                         && (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                         && (t.ExpiresAt == null || t.ExpiresAt > now))
                .OrderBy(t => t.CreationDate).ThenBy(t => t.Id)
                .Take(req.Quantity)
                .ToListAsync(ct);

            if (instances.Count < req.Quantity)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return Result<bool>.Failure("INSUFFICIENT_TOKEN_BALANCE",
                    $"You only have {instances.Count} available token(s) of this type.");
            }

            foreach (var instance in instances)
            {
                instance.PreviousOwnerMemberId = instance.MemberId;
                instance.MemberId              = recipient;
                instance.Status                = TokenInstanceStatus.Distributed;
            }

            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId              = senderId,
                TokenTypeId           = req.TokenTypeId,
                TransactionType       = TokenTransactionType.Distributed,
                Quantity              = req.Quantity,
                DistributedToMemberId = recipient,
                ReferenceId           = null,
                PreviousOwnerMemberId = senderId,
                Status                = TokenInstanceStatus.Distributed,
                CreatedBy             = _currentUser.UserId,
                CreationDate          = now,
                Notes                 = Truncate($"{req.Quantity} token(s) distributed to {recipient}: " +
                                                 string.Join(", ", instances.Select(i => i.ReferenceId)))
            });

            await TokenCustody.MoveBalanceAsync(_db, senderId, recipient, req.TokenTypeId, req.Quantity,
                now, _currentUser.UserId, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(senderId), ct),
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(recipient), ct));

        _ = _push.SendAsync(recipient, NotificationEvents.TokenReceived,
            "Tokens Received", $"You have received {req.Quantity} token(s).", ct);

        return Result<bool>.Success(true);
    }

    // Con hasta 10.000 códigos la lista sería enorme. El detalle por código queda en cada instancia
    // (PreviousOwnerMemberId); el asiento solo lleva un resumen legible.
    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..1997] + "...";
}
