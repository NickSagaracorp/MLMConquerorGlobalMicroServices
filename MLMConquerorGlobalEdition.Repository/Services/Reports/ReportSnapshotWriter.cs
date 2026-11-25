using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IReportSnapshotWriter
{
    /// <summary>Escribe (o reescribe) la foto semanal de los cuatro reportes con fecha <paramref name="domingo"/>.</summary>
    Task<ReportSnapshotResult> WriteAsync(DateTime domingo, CancellationToken ct = default);

    /// <summary>Si ya hay foto completa (los cuatro reportes) de <paramref name="domingo"/>.</summary>
    Task<bool> ExistsAsync(DateTime domingo, CancellationToken ct = default);
}

/// <summary>Filas escritas por reporte.</summary>
public sealed record ReportSnapshotResult(int Earnings180, int EarningsLifetime, int LifetimeRank, int CurrentRank);

/// <summary>
/// Foto semanal de Earnings 180 Days, Earnings Lifetime, Lifetime Rank y Current Rank (como el CSV de los lunes del
/// sistema anterior). Calcula cada lista con el mismo código que la pantalla (EarningsRankingService.CalcularAsync y
/// RankReportService.CalcularAsync), así la foto y el cálculo en vivo dan lo mismo, y la guarda entera: cada (fecha,
/// reporte) se borra y se vuelve a escribir dentro de una transacción, así que repetir la foto la reemplaza. En SQL Server
/// escribe con SqlBulkCopy (cientos de miles de filas). Conserva <see cref="FotosQueSeGuardan"/> semanas.
/// </summary>
/// <remarks>
/// Los rangos se toman como están al hacer la foto (el lunes de madrugada), con el estado del miembro de ese momento;
/// las ganancias, con corte al domingo entero.
/// </remarks>
public class ReportSnapshotWriter : IReportSnapshotWriter
{
    public const int FotosQueSeGuardan = 12;
    public const int VentanaDeLaFoto   = 180;

    private readonly AppDbContext _db;

    public ReportSnapshotWriter(AppDbContext db) => _db = db;

    /// <summary>El domingo de la última semana cerrada a la fecha <paramref name="hoy"/> (si hoy es domingo, el anterior).</summary>
    public static DateTime UltimoDomingoCerrado(DateTime hoy)
    {
        var ayer = hoy.Date.AddDays(-1);
        return ayer.AddDays(-(int)ayer.DayOfWeek);
    }

    public async Task<bool> ExistsAsync(DateTime domingo, CancellationToken ct = default)
    {
        var dia = domingo.Date;
        var ganancias = await _db.EarningsRankingSnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate == dia).Select(s => s.WindowDays).Distinct().CountAsync(ct);
        var rangos = await _db.RankStandingSnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate == dia).Select(s => s.Mode).Distinct().CountAsync(ct);
        return ganancias == 2 && rangos == 2;
    }

    public async Task<ReportSnapshotResult> WriteAsync(DateTime domingo, CancellationToken ct = default)
    {
        var dia = domingo.Date;

        var e180  = await GananciasAsync(dia, VentanaDeLaFoto, ct);
        var eVida = await GananciasAsync(dia, null, ct);
        var rVida = await RangosAsync(dia, RankReportMode.Lifetime, ct);
        var rHoy  = await RangosAsync(dia, RankReportMode.Current, ct);

        await BorrarFotosViejasAsync(dia, ct);
        return new ReportSnapshotResult(e180, eVida, rVida, rHoy);
    }

    private async Task<int> GananciasAsync(DateTime dia, int? ventana, CancellationToken ct)
    {
        var lista = await EarningsRankingService.CalcularAsync(_db, dia, ventana, ct);
        var clave = ventana ?? EarningsRankingSnapshot.DePorVida;
        var filas = lista.Select(r => new EarningsRankingSnapshot
        {
            SnapshotDate = dia, WindowDays = clave, Rank = r.Rank, MemberId = r.MemberId, Username = r.Username,
            MemberName = r.MemberName, Email = r.Email, Country = r.Country, Total = r.Total
        }).ToList();

        await ReemplazarAsync(
            _db.EarningsRankingSnapshots.Where(s => s.SnapshotDate == dia && s.WindowDays == clave),
            filas, "EarningsRankingSnapshots", ct);
        return filas.Count;
    }

    private async Task<int> RangosAsync(DateTime dia, RankReportMode modo, CancellationToken ct)
    {
        var lista = await RankReportService.CalcularAsync(_db, modo, ct);
        var clave = modo == RankReportMode.Lifetime ? RankStandingSnapshot.DePorVida : RankStandingSnapshot.Vigente;
        var filas = lista.Select((r, i) => new RankStandingSnapshot
        {
            SnapshotDate = dia, Mode = clave, Position = i + 1, MemberId = r.MemberId, MemberName = r.MemberName,
            Email = r.Email, Country = r.Country, Status = r.Status, RankDefinitionId = r.RankDefinitionId,
            RankName = r.RankName, RankSortOrder = r.RankSortOrder, Since = r.Since
        }).ToList();

        await ReemplazarAsync(
            _db.RankStandingSnapshots.Where(s => s.SnapshotDate == dia && s.Mode == clave),
            filas, "RankStandingSnapshots", ct);
        return filas.Count;
    }

    /// <summary>Borra las filas de <paramref name="anteriores"/> y escribe <paramref name="filas"/>, todo o nada.</summary>
    private async Task ReemplazarAsync<T>(IQueryable<T> anteriores, List<T> filas, string tabla, CancellationToken ct)
        where T : class
    {
        if (!_db.Database.IsSqlServer())
        {
            _db.Set<T>().RemoveRange(await anteriores.ToListAsync(ct));
            _db.Set<T>().AddRange(filas);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            return;
        }

        var estrategia = _db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await anteriores.ExecuteDeleteAsync(ct);

            var conexion = (SqlConnection)_db.Database.GetDbConnection();
            using var copia = new SqlBulkCopy(conexion, SqlBulkCopyOptions.CheckConstraints, (SqlTransaction)tx.GetDbTransaction())
            {
                DestinationTableName = tabla,
                BatchSize            = 10_000,
                BulkCopyTimeout      = 0
            };
            var tablaDeDatos = ATabla(filas);
            foreach (DataColumn c in tablaDeDatos.Columns) copia.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            await copia.WriteToServerAsync(tablaDeDatos, ct);

            await tx.CommitAsync(ct);
        });
    }

    private async Task BorrarFotosViejasAsync(DateTime dia, CancellationToken ct)
    {
        var limite = dia.AddDays(-7 * (FotosQueSeGuardan - 1));
        if (!_db.Database.IsRelational())
        {
            _db.EarningsRankingSnapshots.RemoveRange(_db.EarningsRankingSnapshots.Where(s => s.SnapshotDate < limite));
            _db.RankStandingSnapshots.RemoveRange(_db.RankStandingSnapshots.Where(s => s.SnapshotDate < limite));
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            return;
        }
        await _db.EarningsRankingSnapshots.Where(s => s.SnapshotDate < limite).ExecuteDeleteAsync(ct);
        await _db.RankStandingSnapshots.Where(s => s.SnapshotDate < limite).ExecuteDeleteAsync(ct);
    }

    /// <summary>Las columnas de la entidad tal como EF las mapea (nombre de columna y tipo).</summary>
    private DataTable ATabla<T>(List<T> filas) where T : class
    {
        var entidad = _db.Model.FindEntityType(typeof(T))!;
        var props   = entidad.GetProperties().ToList();
        var tabla   = new DataTable();
        foreach (var p in props)
        {
            var tipo = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            if (tipo.IsEnum) tipo = Enum.GetUnderlyingType(tipo);
            tabla.Columns.Add(p.GetColumnName(), tipo);
        }
        foreach (var f in filas)
        {
            var fila = tabla.NewRow();
            for (var i = 0; i < props.Count; i++)
            {
                var v = props[i].PropertyInfo!.GetValue(f);
                fila[i] = v is null ? DBNull.Value : v.GetType().IsEnum ? Convert.ChangeType(v, Enum.GetUnderlyingType(v.GetType())) : v;
            }
            tabla.Rows.Add(fila);
        }
        return tabla;
    }
}
