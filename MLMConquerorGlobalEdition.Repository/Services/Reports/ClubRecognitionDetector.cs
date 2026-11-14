using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IClubRecognitionDetector
{
    /// <summary>¿Ya se hizo la carga histórica (existe el marcador en GlobalParameters)?</summary>
    Task<bool> HistoryLoadedAsync(CancellationToken ct = default);

    /// <summary>Carga histórica hasta <paramref name="lastDay"/> inclusive. Devuelve los registros nuevos.</summary>
    Task<int> LoadHistoryAsync(DateTime lastDay, CancellationToken ct = default);

    /// <summary>Detección del día que cerró (<paramref name="day"/>). Devuelve los registros nuevos.</summary>
    Task<int> DetectDayAsync(DateTime day, CancellationToken ct = default);
}

/// <summary>
/// Detección de Club Recognition. Califica quien, con la regla de ganancias de los reportes (<see cref="EarningsQueries"/>),
/// llega al umbral de un nivel activo dentro de su ventana (WindowDays; null = de por vida) y es Ambassador, no
/// Terminated y no cuenta corporativa (estado de hoy). Crea un registro por Ambassador y nivel y nunca borra ni cambia
/// uno existente: es idempotente contra el índice único UX_MemberRecognitions_Member_Level.
/// <para>Nocturno (<see cref="DetectDayAsync"/>): un agregado por ventana distinta; Reached = el día que cerró.</para>
/// <para>Histórico (<see cref="LoadHistoryAsync"/>, en ClubRecognitionDetector.History.cs): una sentencia SQL por ventana
/// distinta; Reached = el día en que se cruzó el umbral; deja el marcador <see cref="HistoryLoadedKey"/>.</para>
/// </summary>
public partial class ClubRecognitionDetector : IClubRecognitionDetector
{
    public const string Author           = "club-recognition";
    public const string HistoryLoadedKey = "ClubRecognition.HistoryLoadedThrough";

    // Recorre CommissionEarnings entero: el tiempo de comando por defecto (30 s) no basta a escala.
    private static readonly TimeSpan LongCommand = TimeSpan.FromMinutes(15);

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public ClubRecognitionDetector(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    /// <summary>Ambassadors que pueden calificar hoy.</summary>
    public static IQueryable<MemberProfile> EligibleMembers(AppDbContext db) =>
        db.MemberProfiles.AsNoTracking().Where(m => m.MemberType == MemberType.Ambassador
                                                 && m.Status != MemberAccountStatus.Terminated
                                                 && !m.IsCorporateAccount);

    /// <summary>
    /// Totales de [<paramref name="from"/>, <paramref name="to"/>) que llegan a <paramref name="minimum"/>, solo de Ambassadors
    /// elegibles. Expuesta para comprobar su traducción a SQL y medirla.
    /// </summary>
    public static IQueryable<MemberEarningsTotal> QualifiedTotals(AppDbContext db, DateTime? from, DateTime to, decimal minimum) =>
        from t in EarningsQueries.TotalsByMember(db, @from, to)
        where t.Total >= minimum
        join m in EligibleMembers(db) on t.MemberId equals m.MemberId
        select t;

    public Task<bool> HistoryLoadedAsync(CancellationToken ct = default) =>
        _db.GlobalParameters.AsNoTracking().AnyAsync(p => p.Key == HistoryLoadedKey, ct);

    public async Task<int> DetectDayAsync(DateTime day, CancellationToken ct = default)
    {
        var closed = day.Date;
        var to     = closed.AddDays(1);                                   // el día que cerró entra entero
        var levels = await _db.RecognitionLevels.AsNoTracking().Where(l => l.IsActive).ToListAsync(ct);
        if (levels.Count == 0) return 0;

        var existing = (await _db.MemberRecognitions.AsNoTracking()
                .Select(r => new { r.MemberId, r.RecognitionLevelId })
                .ToListAsync(ct))
            .Select(r => Key(r.MemberId, r.RecognitionLevelId))
            .ToHashSet();

        if (_db.Database.IsRelational()) _db.Database.SetCommandTimeout(LongCommand);

        var now   = _clock.Now;
        var added = 0;
        foreach (var window in levels.GroupBy(l => l.WindowDays))
        {
            // Ventana de N días que cierra el día D: (D - N, D], como Earnings 180 Days.
            DateTime? from = window.Key is int days ? to.AddDays(-days) : null;
            var qualified  = await QualifiedTotals(_db, from, to, window.Min(l => l.Threshold)).ToListAsync(ct);

            foreach (var total in qualified)
            foreach (var level in window.Where(l => total.Total >= l.Threshold))
            {
                if (!existing.Add(Key(total.MemberId, level.Id))) continue;
                _db.MemberRecognitions.Add(new MemberRecognition
                {
                    MemberId           = total.MemberId,
                    RecognitionLevelId = level.Id,
                    ReachedDate        = closed,
                    QualifyingTotal    = total.Total,
                    Source             = RecognitionSource.Nightly,
                    CreationDate       = now,
                    CreatedBy          = Author
                });
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return added;
    }

    // SQL Server compara MemberId sin mayúsculas (intercalación CI): la clave en memoria hace lo mismo.
    private static string Key(string memberId, int levelId) => $"{memberId.Trim().ToUpperInvariant()}|{levelId}";
}
