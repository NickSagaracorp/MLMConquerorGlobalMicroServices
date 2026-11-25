namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Foto semanal del ranking de comisiones (reportes Earnings Lifetime y Earnings 180 Days): una fila por miembro con su
/// posición, sus datos de pantalla y su total hasta el domingo de la foto. La arma cada lunes el job del BizCenter, como
/// el CSV de los lunes del sistema anterior; la pantalla pagina y busca sobre ella sin recalcular. Cada (fecha, ventana)
/// se reescribe entera.
/// </summary>
public class EarningsRankingSnapshot
{
    /// <summary>El domingo de la foto (columna date): el corte del ranking, incluido entero.</summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>Ventana en días (180) o <see cref="DePorVida"/>.</summary>
    public int WindowDays { get; set; }

    public const int DePorVida = 0;

    /// <summary>Posición entre todos (1 = el que más ganó); también la clave dentro de la foto.</summary>
    public int Rank { get; set; }

    public string  MemberId   { get; set; } = string.Empty;
    public string? Username   { get; set; }
    public string  MemberName { get; set; } = string.Empty;
    public string  Email      { get; set; } = string.Empty;
    public string  Country    { get; set; } = string.Empty;
    public decimal Total      { get; set; }
}
