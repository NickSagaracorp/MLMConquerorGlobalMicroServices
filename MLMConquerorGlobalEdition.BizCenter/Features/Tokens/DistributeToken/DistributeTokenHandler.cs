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
/// orden ordinal, y los códigos se leen DESPUÉS de tomarlo: dos distribuciones (o una distribución y
/// una transferencia) a la vez no eligen el mismo código, y la segunda ve lo que dejó la primera
/// (saldo insuficiente, no negativo). Toman ese mismo candado, por el dueño del código o del saldo:
/// la emisión de tokens en Billing (compra con saldo, tarjeta, confirmar cripto, conciliar), las
/// concesiones y ajustes de saldo del admin y el canje en el alta (SignupAPI, por el dueño del
/// código). Lo que no lo toma, o lo toma por un dueño que ya cambió, lo paran los tokens de
/// concurrencia de <see cref="TokenTransaction"/> (dueño y estado): el guardado choca, se deshace y
/// se responde <see cref="TokenPurchaseErrors.Busy"/>. <see cref="TokenBalance"/> no tiene RowVersion:
/// es una caché que solo se toca bajo el candado. Ocupado → <see cref="TokenPurchaseErrors.Busy"/>
/// (409) sin escribir nada.
///
/// IDEMPOTENCIA. El controlador exige la cabecera <c>Idempotency-Key</c> y la reclama en la caché
/// antes de llegar aquí: un reenvío de la misma distribución no mueve los códigos dos veces.
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
        var callerId  = _currentUser.MemberId;
        var req       = command.Request;
        var requested = req.RecipientMemberId.Trim();
        var now       = _dateTime.Now;

        if (string.Equals(callerId, requested, StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure(TokenDistributionErrors.InvalidRecipient, "Cannot distribute tokens to yourself.");

        var tokenTypeExists = await _db.TokenTypes.AsNoTracking()
            .AnyAsync(tt => tt.Id == req.TokenTypeId && tt.IsActive, ct);
        if (!tokenTypeExists)
            return Result<bool>.Failure("TOKEN_TYPE_NOT_FOUND", "Token type not found or inactive.");

        // Los dos ids tal como están guardados: nombran los candados (member-funds:{id}) y son los que
        // escribe Billing; uno escrito con otras mayúsculas sería otro recurso.
        var found = await TokenCustody.FindRecipientAsync(_db, requested, ct);
        if (found is null)
            return Result<bool>.Failure(TokenDistributionErrors.RecipientNotFound, "Recipient member not found.");
        var recipient = found.MemberId;

        var team = await TokenCustody.IsInEnrollmentTeamAsync(_db, callerId, recipient, ct);
        if (team.SenderId is null)
            return Result<bool>.Failure("SENDER_NOT_IN_TREE", "Sender's enrollment node not found.");
        if (!team.InTeam)
            return Result<bool>.Failure(TokenDistributionErrors.RecipientNotInDownline, "Recipient must be a member of your enrollment team.");
        var senderId = team.SenderId;

        if (!found.IsActive)
            return Result<bool>.Failure(TokenDistributionErrors.RecipientNotActive, "The recipient's account is not active.");

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
                return Result<bool>.Failure(TokenDistributionErrors.InsufficientTokenBalance,
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

            if (!await TokenCustody.SaveAndCommitAsync(_db, tx, ct))
                return Result<bool>.Failure(TokenPurchaseErrors.Busy,
                    "Another token operation is in progress. Please try again in a moment.");
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
