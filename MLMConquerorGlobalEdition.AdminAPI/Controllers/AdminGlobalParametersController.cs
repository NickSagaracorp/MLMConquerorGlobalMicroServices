using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Alta, consulta y edición de <see cref="GlobalParameter"/>. Hasta ahora cada parámetro tenía su
/// propia pantalla (o ninguna); este es el CRUD genérico. Sin borrado: los parámetros los lee el
/// código por nombre, y quitar uno es cambiar el comportamiento sin que nadie lo vea. Para "apagar"
/// una dirección de depósito se deja el valor vacío.
/// </summary>
/// <remarks>
/// Nadie cachea esta tabla hoy (todos los lectores consultan la base en cada uso), así que guardar
/// basta para que el cambio se vea; si algún día se cachea, aquí es donde invalidar.
///
/// <see cref="GlobalParameter"/> solo guarda el último cambio, así que cada alta y cada edición deja
/// además una fila en <c>AuditTracking</c> con el valor viejo y el nuevo, en la misma transacción
/// que el cambio. El de una dirección de depósito avisa por correo a todos los SuperAdmin activos
/// (riesgo R8 y decisión de cierre 2 del Plan F): un fallo del correo se registra y no deshace nada.
/// </remarks>
[ApiController]
[Route("api/v1/admin/global-parameters")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Admin)]
public class AdminGlobalParametersController : ControllerBase
{
    private const string AuditCreate = "Create";
    private const string AuditUpdate = "Update";

    private readonly AppDbContext                 _db;
    private readonly ICurrentUserService          _user;
    private readonly IDateTimeProvider            _clock;
    private readonly ICryptoDepositAlertNotifier  _depositAlert;
    private readonly ILogger<AdminGlobalParametersController> _logger;

    public AdminGlobalParametersController(
        AppDbContext db, ICurrentUserService user, IDateTimeProvider clock,
        ICryptoDepositAlertNotifier depositAlert,
        ILogger<AdminGlobalParametersController> logger)
    {
        _db           = db;
        _user         = user;
        _clock        = clock;
        _depositAlert = depositAlert;
        _logger       = logger;
    }

    /// <summary>GET /api/v1/admin/global-parameters?prefix=Crypto.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? prefix, CancellationToken ct)
    {
        var query = _db.GlobalParameters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(prefix))
            query = query.Where(p => p.Key.StartsWith(prefix));

        var rows = await query.OrderBy(p => p.Key).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<GlobalParameterDto>>.Ok(rows.Select(ToDto)));
    }

    /// <summary>GET /api/v1/admin/global-parameters/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var p = await _db.GlobalParameters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null
            ? NotFound(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_NOT_FOUND", $"Parameter {id} not found."))
            : Ok(ApiResponse<GlobalParameterDto>.Ok(ToDto(p)));
    }

    /// <summary>POST /api/v1/admin/global-parameters</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGlobalParameterRequest request, CancellationToken ct)
    {
        var key   = request.Key.Trim();
        var value = request.Value?.Trim() ?? string.Empty;

        var error = GlobalParameterRules.ValueError(key, value);
        if (error is not null)
            return BadRequest(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_INVALID", error));

        if (await _db.GlobalParameters.AnyAsync(p => p.Key == key, ct))
            return Conflict(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_EXISTS", $"Parameter '{key}' already exists."));

        var now = _clock.Now;
        var entity = new GlobalParameter
        {
            Key            = key,
            Value          = value,
            Description    = request.Description?.Trim(),
            CreatedBy      = Actor,
            CreationDate   = now,
            LastUpdateDate = now,
            LastUpdateBy   = Actor
        };

        // El Id lo da la base al guardar y el historial lo necesita: dos guardados, una transacción,
        // para que no quede un parámetro creado sin su fila de auditoría.
        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            _db.GlobalParameters.Add(entity);
            await _db.SaveChangesAsync(ct);

            _db.AuditTracking.Add(AuditRow(entity, AuditCreate, oldValue: null, oldDescription: null, now));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await AlertIfDepositChanged(key, oldValue: null, newValue: value);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, ApiResponse<GlobalParameterDto>.Ok(ToDto(entity)));
    }

    /// <summary>PUT /api/v1/admin/global-parameters/{id} — valor y descripción; la clave no cambia.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGlobalParameterRequest request, CancellationToken ct)
    {
        var entity = await _db.GlobalParameters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_NOT_FOUND", $"Parameter {id} not found."));

        var value = request.Value?.Trim() ?? string.Empty;
        var error = GlobalParameterRules.ValueError(entity.Key, value);
        if (error is not null)
            return BadRequest(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_INVALID", error));

        var now            = _clock.Now;
        var oldValue       = entity.Value;
        var oldDescription = entity.Description;

        entity.Value          = value;
        entity.Description    = request.Description?.Trim();
        entity.LastUpdateDate = now;
        entity.LastUpdateBy   = Actor;

        // Un solo guardado: el cambio y su fila de historial entran juntos o no entra ninguno.
        _db.AuditTracking.Add(AuditRow(entity, AuditUpdate, oldValue, oldDescription, now));
        await _db.SaveChangesAsync(ct);

        await AlertIfDepositChanged(entity.Key, oldValue, value);
        return Ok(ApiResponse<GlobalParameterDto>.Ok(ToDto(entity)));
    }

    private string Actor
    {
        get
        {
            var who = !string.IsNullOrWhiteSpace(_user.Email) ? _user.Email : _user.UserId;
            return who.Length <= 100 ? who : who[..100];
        }
    }

    private GeneralAuditTracking AuditRow(
        GlobalParameter entity, string action, string? oldValue, string? oldDescription, DateTime now) => new()
    {
        EntityName = nameof(GlobalParameter),
        EntityId   = entity.Id.ToString(),
        Action     = action,
        OldValues  = action == AuditCreate
            ? null
            : JsonSerializer.Serialize(new { entity.Key, Value = oldValue, Description = oldDescription }),
        NewValues  = JsonSerializer.Serialize(new { entity.Key, entity.Value, entity.Description }),
        // Mismo criterio que el resto de AuditTracking: el id del usuario, que no cambia aunque cambie el correo.
        ChangedBy  = string.IsNullOrWhiteSpace(_user.UserId) ? Actor : _user.UserId,
        ChangedAt  = now
    };

    /// <summary>
    /// Registra y avisa solo si cambió a dónde va el dinero: tocar la descripción de una dirección o
    /// guardarla igual no es un cambio que deba alarmar a nadie.
    /// </summary>
    private async Task AlertIfDepositChanged(string key, string? oldValue, string newValue)
    {
        if (!GlobalParameterRules.IsDepositAddressKey(key)) return;
        if (oldValue is not null && string.Equals(oldValue.Trim(), newValue, StringComparison.Ordinal)) return;

        _logger.LogWarning(
            "Dirección de depósito cripto cambiada: {Key} de '{Old}' a '{New}' por {Actor}.",
            key, oldValue ?? "(nueva)", newValue, Actor);

        await _depositAlert.NotifyAsync(key, oldValue, newValue, Actor);
    }

    private static GlobalParameterDto ToDto(GlobalParameter p) =>
        new(p.Id, p.Key, p.Value, p.Description, p.CreationDate, p.CreatedBy, p.LastUpdateDate, p.LastUpdateBy);
}
