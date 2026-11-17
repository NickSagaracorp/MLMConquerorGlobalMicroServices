using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public partial class ClubRecognitionDetector
{
    /// <summary>
    /// Carga histórica de UNA ventana (@window; NULL = niveles de por vida), por conjuntos y en una sola lectura de
    /// CommissionEarnings:
    /// <list type="number">
    /// <item>Daily: ganancias por miembro y día (Paid + Pending, sin borradas, hasta @lastDay inclusive).</item>
    /// <item>Steps: cada día suma su monto y, con ventana, lo resta @window días después (el día en que sale de la
    /// ventana (D - @window, D]). Sin ventana, la segunda fila es NULL y se descarta.</item>
    /// <item>Totals: la suma acumulada de esos cambios por miembro = total de la ventana (o acumulado de por vida) en cada
    /// día en que cambia. Entre esos días no cambia, así que el primer día en que llega al umbral es uno de ellos.</item>
    /// <item>Crossed: por miembro y nivel activo de esa ventana, el primer día con total >= umbral.</item>
    /// </list>
    /// Inserta solo a Ambassadors no Terminated ni corporativos que aún no tienen registro de ese nivel (NOT EXISTS).
    /// SQL Server no admite RANGE con intervalo de fechas en las funciones de ventana: por eso la ventana móvil se
    /// resuelve con entradas y salidas. Expuesta para medirla (Tarea 6 del plan C) y para revisarla.
    /// </summary>
    public const string HistoricalSql = """
        WITH Daily AS (
            SELECT c.[BeneficiaryMemberId] AS MemberId, CAST(c.[EarnedDate] AS date) AS [Day], SUM(c.[Amount]) AS Amount
            FROM [CommissionEarnings] c
            WHERE c.[IsDeleted] = 0 AND c.[Status] IN (@paid, @pending) AND c.[EarnedDate] < @to
            GROUP BY c.[BeneficiaryMemberId], CAST(c.[EarnedDate] AS date)
        ), Steps AS (
            SELECT d.MemberId, e.[Day], SUM(e.Delta) AS Delta
            FROM Daily d
            CROSS APPLY (VALUES (d.[Day], d.Amount), (DATEADD(day, @window, d.[Day]), -d.Amount)) e([Day], Delta)
            WHERE e.[Day] IS NOT NULL AND e.[Day] <= @lastDay
            GROUP BY d.MemberId, e.[Day]
        ), Totals AS (
            SELECT MemberId, [Day], SUM(Delta) OVER (PARTITION BY MemberId ORDER BY [Day] ROWS UNBOUNDED PRECEDING) AS Total
            FROM Steps
        ), Crossed AS (
            SELECT t.MemberId, l.[Id] AS LevelId, t.[Day], t.Total,
                   ROW_NUMBER() OVER (PARTITION BY t.MemberId, l.[Id] ORDER BY t.[Day]) AS Rn
            FROM Totals t
            JOIN [RecognitionLevels] l
              ON l.[IsActive] = 1
             AND ((@window IS NULL AND l.[WindowDays] IS NULL) OR l.[WindowDays] = @window)
             AND t.Total >= l.[Threshold]
        )
        INSERT INTO [MemberRecognitions]
            ([MemberId], [RecognitionLevelId], [ReachedDate], [QualifyingTotal], [Source], [CreationDate], [CreatedBy])
        SELECT x.MemberId, x.LevelId, x.[Day], x.Total, @source, @now, @author
        FROM Crossed x
        JOIN [MemberProfiles] m ON m.[MemberId] = x.MemberId
        WHERE x.Rn = 1
          AND m.[IsDeleted] = 0 AND m.[MemberType] = @ambassador AND m.[Status] <> @terminated AND m.[IsCorporateAccount] = 0
          AND NOT EXISTS (SELECT 1 FROM [MemberRecognitions] r
                          WHERE r.[MemberId] = x.MemberId AND r.[RecognitionLevelId] = x.LevelId);
        """;

    public async Task<int> LoadHistoryAsync(DateTime lastDay, CancellationToken ct = default)
    {
        var day     = lastDay.Date;
        var windows = await _db.RecognitionLevels.AsNoTracking()
            .Where(l => l.IsActive).Select(l => l.WindowDays).Distinct().ToListAsync(ct);

        var previous = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(LongCommand);
        var added = 0;
        try
        {
            foreach (var window in windows)
                added += await InsertWindowAsync(day, window, ct);
        }
        finally
        {
            _db.Database.SetCommandTimeout(previous);
        }

        await MarkHistoryLoadedAsync(day, ct);
        return added;
    }

    // Otra carga inserto primero entre el NOT EXISTS y el INSERT, o gano un interbloqueo: se repite; ahora el NOT EXISTS
    // ve sus filas.
    private Task<int> InsertWindowAsync(DateTime day, int? window, CancellationToken ct) =>
        WriteConflictRetry.RunAsync(
            () => _db.Database.ExecuteSqlRawAsync(HistoricalSql, HistoryParameters(day, window), ct));

    private object[] HistoryParameters(DateTime day, int? window) => new object[]
    {
        new SqlParameter("@lastDay",    SqlDbType.Date)          { Value = day },
        new SqlParameter("@to",         SqlDbType.DateTime2)     { Value = day.AddDays(1) },
        new SqlParameter("@window",     SqlDbType.Int)           { Value = (object?)window ?? DBNull.Value },
        new SqlParameter("@paid",       SqlDbType.Int)           { Value = (int)CommissionEarningStatus.Paid },
        new SqlParameter("@pending",    SqlDbType.Int)           { Value = (int)CommissionEarningStatus.Pending },
        new SqlParameter("@ambassador", SqlDbType.Int)           { Value = (int)MemberType.Ambassador },
        new SqlParameter("@terminated", SqlDbType.Int)           { Value = (int)MemberAccountStatus.Terminated },
        new SqlParameter("@source",     SqlDbType.Int)           { Value = (int)RecognitionSource.Historical },
        new SqlParameter("@now",        SqlDbType.DateTime2)     { Value = _clock.Now },
        new SqlParameter("@author",     SqlDbType.NVarChar, 100) { Value = Author },
    };

    private Task MarkHistoryLoadedAsync(DateTime day, CancellationToken ct) =>
        WriteConflictRetry.RunAsync(
            () => MarkHistoryLoadedOnceAsync(day, ct), () => _db.ChangeTracker.Clear());

    /// <summary>Deja (o actualiza) el marcador con el último día cargado: el job nocturno deja de hacer la carga inicial.</summary>
    private async Task MarkHistoryLoadedOnceAsync(DateTime day, CancellationToken ct)
    {
        var now    = _clock.Now;
        var marker = await _db.GlobalParameters.FirstOrDefaultAsync(p => p.Key == HistoryLoadedKey, ct);
        if (marker is null)
        {
            marker = new GlobalParameter
            {
                Key          = HistoryLoadedKey,
                Description  = "Club Recognition: last day covered by the historical load. Delete it to make the nightly job run the historical load again.",
                CreationDate = now,
                CreatedBy    = Author
            };
            _db.GlobalParameters.Add(marker);
        }
        else
        {
            marker.LastUpdateDate = now;
            marker.LastUpdateBy   = Author;
        }
        marker.Value = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);
    }
}
