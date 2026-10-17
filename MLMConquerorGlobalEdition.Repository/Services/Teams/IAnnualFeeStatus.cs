namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// ¿Pagó el miembro su anualidad en curso? Rama (b) de «Calificado» (Plan J, corrección de Nick N3).
/// La implementa <c>Repository.Services.AnnualFee.BillingStateAnnualFeeStatus</c> (Plan K): la anualidad
/// cuenta como pagada si el estado de cuota anual del miembro está al día.
/// </summary>
public interface IAnnualFeeStatus
{
    /// <summary>Los miembros de <paramref name="memberIds"/> con la anualidad en curso pagada.</summary>
    Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default);
}
