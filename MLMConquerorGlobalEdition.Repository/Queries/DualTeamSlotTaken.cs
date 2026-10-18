using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>Plan P: reconoce la violacion del indice unico de hueco del binario (la red de DualTeamGate).</summary>
public static class DualTeamSlotTaken
{
    public const string IndexName = "UX_DualTeamTree_ParentMemberId_Side";

    public static bool Is(DbUpdateException ex) =>
        ex.InnerException is SqlException s && (s.Number is 2601 or 2627)
        && s.Message.Contains(IndexName, StringComparison.Ordinal);
}
