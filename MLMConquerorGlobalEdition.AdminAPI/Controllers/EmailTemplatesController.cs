using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Email;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/email-templates")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class EmailTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private const string CacheKeyPrefix = "email-template:";

    public EmailTemplatesController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // ── Templates ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.EmailTemplates.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new EmailTemplateListDto(
                x.Id, x.Name, x.EventType, x.Category, x.IsActive, x.Description,
                x.Localizations.Select(l => l.LanguageCode).ToList()))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<EmailTemplateListDto>>.Ok(new PagedResult<EmailTemplateListDto>
        {
            Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplates
            .AsNoTracking()
            .Include(x => x.Localizations)
            .Include(x => x.Variables)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("EMAIL_TEMPLATE_NOT_FOUND", $"Email template '{id}' not found."));

        return Ok(ApiResponse<EmailTemplateDetailDto>.Ok(ToDetailDto(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmailTemplateDto dto, CancellationToken ct = default)
    {
        var admin = User.Identity?.Name ?? "admin";
        var now = DateTime.UtcNow;

        var entity = new EmailTemplate
        {
            Name = dto.Name.Trim(),
            EventType = dto.EventType.Trim().ToUpperInvariant(),
            Category = dto.Category.Trim(),
            Description = dto.Description?.Trim(),
            IsActive = true,
            CreatedBy = admin,
            CreationDate = now
        };

        if (dto.Variables is { Count: > 0 })
        {
            foreach (var v in dto.Variables)
            {
                entity.Variables.Add(new EmailTemplateVariable
                {
                    Name = v.Name.Trim(),
                    Description = v.Description?.Trim(),
                    IsRequired = v.IsRequired,
                    CreatedBy = admin,
                    CreationDate = now
                });
            }
        }

        await _db.EmailTemplates.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"{CacheKeyPrefix}{entity.Id}", ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            ApiResponse<EmailTemplateDetailDto>.Ok(ToDetailDto(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmailTemplateDto dto, CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("EMAIL_TEMPLATE_NOT_FOUND", $"Email template '{id}' not found."));

        entity.Name = dto.Name.Trim();
        entity.EventType = dto.EventType.Trim().ToUpperInvariant();
        entity.Category = dto.Category.Trim();
        entity.Description = dto.Description?.Trim();
        entity.IsActive = dto.IsActive;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"{CacheKeyPrefix}{id}", ct);

        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("EMAIL_TEMPLATE_NOT_FOUND", $"Email template '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"{CacheKeyPrefix}{id}", ct);

        return Ok(ApiResponse<object>.Ok(new { }, "Email template deactivated."));
    }

    // ── Localizations ─────────────────────────────────────────────────────

    [HttpPost("{id:int}/localizations")]
    public async Task<IActionResult> UpsertLocalization(int id, [FromBody] UpsertLocalizationDto dto,
        CancellationToken ct = default)
    {
        var templateExists = await _db.EmailTemplates.AnyAsync(x => x.Id == id, ct);
        if (!templateExists)
            return NotFound(ApiResponse<object>.Fail("EMAIL_TEMPLATE_NOT_FOUND", $"Email template '{id}' not found."));

        var langCode = dto.LanguageCode.Trim().ToLowerInvariant();
        var admin = User.Identity?.Name ?? "admin";
        var now = DateTime.UtcNow;

        var existing = await _db.EmailTemplateLocalizations
            .FirstOrDefaultAsync(x => x.EmailTemplateId == id && x.LanguageCode == langCode, ct);

        if (existing is null)
        {
            var loc = new EmailTemplateLocalization
            {
                EmailTemplateId = id,
                LanguageCode = langCode,
                Subject = dto.Subject.Trim(),
                HtmlBody = dto.HtmlBody,
                TextBody = dto.TextBody?.Trim(),
                CreatedBy = admin,
                CreationDate = now
            };
            await _db.EmailTemplateLocalizations.AddAsync(loc, ct);
        }
        else
        {
            existing.Subject = dto.Subject.Trim();
            existing.HtmlBody = dto.HtmlBody;
            existing.TextBody = dto.TextBody?.Trim();
            existing.LastUpdateBy = admin;
            existing.LastUpdateDate = now;
        }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"{CacheKeyPrefix}{id}", ct);

        return Ok(ApiResponse<object>.Ok(new { templateId = id, languageCode = langCode }));
    }

    [HttpDelete("{id:int}/localizations/{locId:int}")]
    public async Task<IActionResult> DeleteLocalization(int id, int locId, CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplateLocalizations
            .FirstOrDefaultAsync(x => x.Id == locId && x.EmailTemplateId == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("LOCALIZATION_NOT_FOUND", "Localization not found."));

        _db.EmailTemplateLocalizations.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"{CacheKeyPrefix}{id}", ct);

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ── Variables ─────────────────────────────────────────────────────────

    [HttpPost("{id:int}/variables")]
    public async Task<IActionResult> AddVariable(int id, [FromBody] VariableFormDto dto,
        CancellationToken ct = default)
    {
        var templateExists = await _db.EmailTemplates.AnyAsync(x => x.Id == id, ct);
        if (!templateExists)
            return NotFound(ApiResponse<object>.Fail("EMAIL_TEMPLATE_NOT_FOUND", $"Email template '{id}' not found."));

        var varName = dto.Name.Trim();
        var duplicate = await _db.EmailTemplateVariables
            .AnyAsync(x => x.EmailTemplateId == id && x.Name == varName, ct);

        if (duplicate)
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_VARIABLE", $"Variable '{varName}' already exists on this template."));

        var variable = new EmailTemplateVariable
        {
            EmailTemplateId = id,
            Name = varName,
            Description = dto.Description?.Trim(),
            IsRequired = dto.IsRequired,
            CreatedBy = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        await _db.EmailTemplateVariables.AddAsync(variable, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<VariableDto>.Ok(new VariableDto(variable.Id, variable.Name, variable.Description, variable.IsRequired)));
    }

    [HttpPut("{id:int}/variables/{varId:int}")]
    public async Task<IActionResult> UpdateVariable(int id, int varId, [FromBody] VariableFormDto dto,
        CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplateVariables
            .FirstOrDefaultAsync(x => x.Id == varId && x.EmailTemplateId == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("VARIABLE_NOT_FOUND", "Variable not found."));

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.IsRequired = dto.IsRequired;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<VariableDto>.Ok(new VariableDto(entity.Id, entity.Name, entity.Description, entity.IsRequired)));
    }

    [HttpDelete("{id:int}/variables/{varId:int}")]
    public async Task<IActionResult> DeleteVariable(int id, int varId, CancellationToken ct = default)
    {
        var entity = await _db.EmailTemplateVariables
            .FirstOrDefaultAsync(x => x.Id == varId && x.EmailTemplateId == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("VARIABLE_NOT_FOUND", "Variable not found."));

        _db.EmailTemplateVariables.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static EmailTemplateDetailDto ToDetailDto(EmailTemplate x) => new(
        x.Id, x.Name, x.EventType, x.Category, x.IsActive, x.Description,
        x.Localizations.Select(l => new LocalizationDto(l.Id, l.LanguageCode, l.Subject, l.HtmlBody, l.TextBody)).ToList(),
        x.Variables.Select(v => new VariableDto(v.Id, v.Name, v.Description, v.IsRequired)).ToList()
    );

    // ── DTOs ──────────────────────────────────────────────────────────────

    public record EmailTemplateListDto(
        int Id, string Name, string EventType, string Category, bool IsActive,
        string? Description, List<string> Languages);

    public record EmailTemplateDetailDto(
        int Id, string Name, string EventType, string Category, bool IsActive,
        string? Description, List<LocalizationDto> Localizations, List<VariableDto> Variables);

    public record LocalizationDto(int Id, string LanguageCode, string Subject, string HtmlBody, string? TextBody);
    public record VariableDto(int Id, string Name, string? Description, bool IsRequired);

    public record CreateEmailTemplateDto(
        string Name, string EventType, string Category, string? Description,
        List<VariableFormDto>? Variables);

    public record UpdateEmailTemplateDto(
        string Name, string EventType, string Category, string? Description, bool IsActive);

    public record UpsertLocalizationDto(
        string LanguageCode, string Subject, string HtmlBody, string? TextBody);

    public record VariableFormDto(string Name, string? Description, bool IsRequired);
}
