using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Foto semanal de los reportes Lifetime Rank y Current Rank: un Ambassador por fila, en el orden del reporte, con el
/// rango, desde cuándo lo tiene y su estado al tomar la foto. La arma cada lunes el job del BizCenter; la pantalla filtra
/// y pagina sobre ella sin recalcular. Cada (fecha, modo) se reescribe entera.
/// </summary>
public class RankStandingSnapshot
{
    /// <summary>El domingo de la foto (columna date).</summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary><see cref="DePorVida"/> (el mayor rango alcanzado) o <see cref="Vigente"/> (el rango actual).</summary>
    public int Mode { get; set; }

    public const int DePorVida = 0;
    public const int Vigente   = 1;

    /// <summary>Orden del reporte (mayor rango, luego el más reciente); también la clave dentro de la foto.</summary>
    public int Position { get; set; }

    public string              MemberId         { get; set; } = string.Empty;
    public string              MemberName       { get; set; } = string.Empty;
    public string              Email            { get; set; } = string.Empty;
    public string              Country          { get; set; } = string.Empty;
    public MemberAccountStatus Status           { get; set; }
    public int                 RankDefinitionId { get; set; }
    public string              RankName         { get; set; } = string.Empty;
    public int                 RankSortOrder    { get; set; }
    public DateTime            Since            { get; set; }
}
