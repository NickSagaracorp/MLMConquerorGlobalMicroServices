namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Una fila del ranking de comisiones (Earnings Lifetime / Earnings por ventana).</summary>
public sealed class EarningsRankRowView
{
    public int     Rank       { get; init; }
    public string  MemberId   { get; init; } = string.Empty;
    public string? Username   { get; init; }
    public string  MemberName { get; init; } = string.Empty;
    public string  Email      { get; init; } = string.Empty;
    public string  Country    { get; init; } = string.Empty;
    public decimal Total      { get; init; }
}
