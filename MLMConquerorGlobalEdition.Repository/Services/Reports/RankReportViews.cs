using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Lifetime Rank (el mayor rango alcanzado) o Current Rank (el vigente).</summary>
public enum RankReportMode
{
    Lifetime,
    Current
}

/// <summary>
/// Filtros de los reportes de rango. Sin estados = los visibles (Active, Inactive, Suspended, Pending); Terminated nunca
/// aparece. SinceFrom y SinceTo son días completos e inclusivos sobre la fecha «Since».
/// </summary>
public sealed record RankReportFilter(
    RankReportMode Mode,
    string? Search = null,
    int? RankDefinitionId = null,
    IReadOnlyCollection<MemberAccountStatus>? Statuses = null,
    DateTime? SinceFrom = null,
    DateTime? SinceTo = null);

/// <summary>Una fila de Lifetime Rank / Current Rank.</summary>
public sealed class RankReportRowView
{
    public string              MemberId         { get; init; } = string.Empty;
    public string              MemberName       { get; init; } = string.Empty;
    public string              Email            { get; init; } = string.Empty;
    public string              Country          { get; init; } = string.Empty;
    public MemberAccountStatus Status           { get; init; }
    public int                 RankDefinitionId { get; init; }
    public string              RankName         { get; init; } = string.Empty;
    public int                 RankSortOrder    { get; init; }
    public DateTime            Since            { get; init; }
}

/// <summary>Lo que trae la consulta SQL (<see cref="RankReportService.SourceQuery"/>) antes de dar forma a la fila.</summary>
public sealed class RankReportSourceRow
{
    public string              MemberId         { get; init; } = string.Empty;
    public int                 RankDefinitionId { get; init; }
    public string              RankName         { get; init; } = string.Empty;
    public int                 SortOrder        { get; init; }
    public DateTime            Since            { get; init; }
    public string              FirstName        { get; init; } = string.Empty;
    public string              LastName         { get; init; } = string.Empty;
    public string              Email            { get; init; } = string.Empty;
    public string              Country          { get; init; } = string.Empty;
    public MemberAccountStatus Status           { get; init; }
}
