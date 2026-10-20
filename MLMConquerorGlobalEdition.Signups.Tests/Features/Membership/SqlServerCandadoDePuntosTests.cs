using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan Q (seguimiento de la tercera revision del bloque 2): si el PRIMER sp_getapplock de MemberPointsLocks no se obtiene
/// (otro lo tiene), no habia ningun candado que soltar y ReleaseAsync volvia sin cerrar la conexion que AcquireAsync abrio:
/// el contexto quedaba con la conexion abierta para siempre (la cuenta de aperturas de EF no volvia a cero).
/// </summary>
public class SqlServerCandadoDePuntosTests
{
    [SqlServerFact]
    public async Task Si_el_primer_candado_no_se_obtiene_la_conexion_se_cierra()
    {
        var miembro = $"TCP-{Guid.NewGuid():N}"[..20];
        await using var otro = new SqlConnection(SqlServerTestDb.ConnectionString);
        await otro.OpenAsync();
        await using (var cmd = new SqlCommand(
                         "EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;", otro))
        {
            cmd.Parameters.AddWithValue("@r", MemberPointsLocks.Resource(miembro));
            await cmd.ExecuteNonQueryAsync();
        }

        await using var db = SqlServerTestDb.Db();
        var act = () => MemberPointsLocks.AcquireAsync(db, new[] { miembro }, lockTimeoutMs: 200);

        await act.Should().ThrowAsync<InvalidOperationException>();
        db.Database.GetDbConnection().State.Should().Be(ConnectionState.Closed,
            "la conexion que abrio AcquireAsync se cierra aunque no llegara a tomar ningun candado");
    }
}
