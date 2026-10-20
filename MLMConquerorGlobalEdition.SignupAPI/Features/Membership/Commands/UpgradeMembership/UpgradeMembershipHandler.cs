using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.UpgradeMembership;

/// <summary>
/// Subida de nivel del propio miembro (<c>POST api/v1/members/{memberId}/membership/upgrade</c>; es su único
/// llamador, y AdminAPI no tiene subida de nivel).
///
/// NO COBRA NADA: cierra la suscripción actual y abre la nueva. Por eso va detrás de
/// <c>Membership:UpgradeEnabled</c> (<see cref="MembershipUpgrades"/>), apagado por defecto y si falta:
/// apagado responde UPGRADE_DISABLED antes de leer nada y no cambia nada. Lo enciende el Plan G cuando la
/// subida cobre la diferencia.
/// </summary>
public class UpgradeMembershipHandler : IRequestHandler<UpgradeMembershipCommand, Result<bool>>
{
    /// <summary>La subida de nivel está apagada (Membership:UpgradeEnabled): no se hizo nada. 400.</summary>
    public const string UpgradeDisabledCode = "UPGRADE_DISABLED";

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IConfiguration? _configuration;

    public UpgradeMembershipHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        // Sin configuración la subida va apagada (falla cerrada). El contenedor inyecta IConfiguration.
        IConfiguration? configuration = null)
    {
        _db = db;
        _dateTime = dateTime;
        _configuration = configuration;
    }

    public async Task<Result<bool>> Handle(UpgradeMembershipCommand command, CancellationToken ct)
    {
        if (!MembershipUpgrades.IsEnabled(_configuration))
            return Result<bool>.Failure(UpgradeDisabledCode, "Membership upgrades are not available right now.");

        var now = _dateTime.Now;

        var member = await _db.MemberProfiles.FirstOrDefaultAsync(x => x.MemberId == command.MemberId, ct);
        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", $"Member '{command.MemberId}' not found.");

        var currentSub = await _db.MembershipSubscriptions
            .Include(x => x.MembershipLevel)
            .FirstOrDefaultAsync(x => x.MemberId == command.MemberId && x.SubscriptionStatus == MembershipStatus.Active, ct);
        if (currentSub is null)
            return Result<bool>.Failure("NO_ACTIVE_SUBSCRIPTION", "Member has no active membership subscription.");

        var newLevel = await _db.MembershipLevels.FirstOrDefaultAsync(x => x.Id == command.NewMembershipLevelId && x.IsActive, ct);
        if (newLevel is null)
            return Result<bool>.Failure("MEMBERSHIP_LEVEL_NOT_FOUND", "Target membership level not found or inactive.");

        // Domain rule validation — throws MembershipChangeNotAllowedException if invalid
        currentSub.ValidateChange(newLevel.SortOrder, currentSub.MembershipLevel!.SortOrder, SubscriptionChangeReason.Upgrade);

        // Close current subscription
        currentSub.SubscriptionStatus = MembershipStatus.Expired;
        currentSub.EndDate = now;
        currentSub.LastUpdateBy = command.MemberId;

        // Create new subscription
        var newSub = new MembershipSubscription
        {
            MemberId = command.MemberId,
            MembershipLevelId = command.NewMembershipLevelId,
            PreviousMembershipLevelId = currentSub.MembershipLevelId,
            ChangeReason = SubscriptionChangeReason.Upgrade,
            SubscriptionStatus = MembershipStatus.Active,
            StartDate = now,
            IsFree = newLevel.IsFree,
            IsAutoRenew = newLevel.IsAutoRenew,
            CreatedBy = command.MemberId,
            CreationDate = now,
            LastUpdateDate = now
        };

        await _db.MembershipSubscriptions.AddAsync(newSub, ct);
        // Plan Q (tercera revisión del bloque 2, C-a): la vieja deja de cobrarse en el mismo guardado.
        await MembershipBillingStops.StopForReplacedAsync(_db, currentSub.Id, now, command.MemberId, ct);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
