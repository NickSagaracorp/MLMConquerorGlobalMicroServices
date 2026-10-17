namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// ¿Pagó el miembro su anualidad en curso? Rama (b) de «Calificado» (Plan J, corrección de Nick
/// N3). PUNTO DE EXTENSIÓN: la anualidad ($99) todavía no está modelada; la cobrará el Plan K, que
/// sustituye <see cref="AnnualFeeNotModeled"/> por una implementación real sin tocar a quien la usa.
/// </summary>
public interface IAnnualFeeStatus
{
    /// <summary>Los miembros de <paramref name="memberIds"/> con la anualidad en curso pagada.</summary>
    Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default);
}

/// <summary>
/// Implementación de hoy: la anualidad no existe todavía, así que nadie la tiene pagada y la rama
/// (b) de «Calificado» nunca se cumple. Quien se inscribió hace más de 12 meses solo califica con
/// la marca <c>MemberProfile.KeepQualified</c>. Se sustituye en el Plan K.
/// </summary>
public sealed class AnnualFeeNotModeled : IAnnualFeeStatus
{
    private static readonly IReadOnlySet<string> Nobody = new HashSet<string>();

    public Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default) =>
        Task.FromResult(Nobody);
}
