using Microsoft.Data.SqlClient;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Reconoce el error de clave duplicada de SQL Server (2601 índice único, 2627 restricción) en cualquier nivel de la excepción.</summary>
public static class DuplicateKeyErrors
{
    public static bool IsDuplicateKey(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 2601 or 2627 }) return true;
        return false;
    }
}
