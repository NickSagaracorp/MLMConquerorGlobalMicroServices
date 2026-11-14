using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IMemberCountrySnapshotWriter
{
    /// <summary>Escribe (o reescribe) la foto del día con el estado actual de los miembros. Devuelve las filas del día.</summary>
    Task<int> WriteAsync(DateTime snapshotDate, CancellationToken ct = default);
}

/// <summary>
/// Foto diaria de miembros por país, tipo y estado (Top Countries). Un GROUP BY sobre MemberProfiles (sin borrados) y
/// un solo SaveChanges que actualiza las filas del día que ya existen, añade las nuevas y borra las que sobran: es
/// idempotente y nunca borra e inserta la misma clave. Otros días no se tocan. Lo llaman el job nocturno del BizCenter
/// (el día que cerró) y la foto manual del admin (hoy).
/// </summary>
public class MemberCountrySnapshotWriter : IMemberCountrySnapshotWriter
{
    public const string Author = "country-snapshot";

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public MemberCountrySnapshotWriter(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<int> WriteAsync(DateTime snapshotDate, CancellationToken ct = default)
    {
        try
        {
            return await WriteOnceAsync(snapshotDate, ct);
        }
        catch (DbUpdateException ex) when (DuplicateKeyErrors.IsDuplicateKey(ex))
        {
            // Otra escritura del mismo dia inserto primero (el job nocturno y la foto manual a la vez): se descartan los
            // cambios pendientes y se repite entera; ahora ve las filas y las actualiza.
            _db.ChangeTracker.Clear();
            return await WriteOnceAsync(snapshotDate, ct);
        }
    }

    private async Task<int> WriteOnceAsync(DateTime snapshotDate, CancellationToken ct)
    {
        var day = snapshotDate.Date;

        var counts = await _db.MemberProfiles.AsNoTracking()
            .GroupBy(m => new { m.Country, m.MemberType, m.Status })
            .Select(g => new { g.Key.Country, g.Key.MemberType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var existing = (await _db.MemberCountrySnapshots.Where(s => s.SnapshotDate == day).ToListAsync(ct))
            .ToDictionary(s => Key(s.Country, s.MemberType, s.Status));

        var now = _clock.Now;
        foreach (var c in counts)
        {
            if (existing.Remove(Key(c.Country, c.MemberType, c.Status), out var row))
            {
                row.MemberCount = c.Count;
                continue;
            }
            _db.MemberCountrySnapshots.Add(new MemberCountrySnapshot
            {
                SnapshotDate = day,
                Country      = c.Country,
                MemberType   = c.MemberType,
                Status       = c.Status,
                MemberCount  = c.Count,
                CreationDate = now,
                CreatedBy    = Author
            });
        }
        _db.MemberCountrySnapshots.RemoveRange(existing.Values);   // combinaciones que ese dia ya no tienen miembros

        await _db.SaveChangesAsync(ct);
        return counts.Count;
    }

    // SQL Server compara el pais sin mayusculas ni espacios finales (intercalacion CI): la clave en memoria hace lo mismo
    // para no insertar una fila que el indice unico consideraria repetida.
    private static string Key(string country, MemberType type, MemberAccountStatus status) =>
        $"{country.TrimEnd().ToUpperInvariant()}|{(int)type}|{(int)status}";
}
