using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/token-types")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class TokenTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public TokenTypesController(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var items = await _db.TokenTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<TokenTypeDto>>.Ok(_mapper.Map<IEnumerable<TokenTypeDto>>(items)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<TokenTypeDto>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        return Ok(ApiResponse<TokenTypeDto>.Ok(_mapper.Map<TokenTypeDto>(entity)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenTypeDto dto, CancellationToken ct = default)
    {
        var entity = _mapper.Map<TokenType>(dto);
        entity.CreatedBy = User.Identity?.Name ?? "admin";
        await _db.TokenTypes.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ApiResponse<TokenTypeDto>.Ok(_mapper.Map<TokenTypeDto>(entity)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTokenTypeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<TokenTypeDto>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        _mapper.Map(dto, entity);
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<TokenTypeDto>.Ok(_mapper.Map<TokenTypeDto>(entity)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.TokenTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("TOKEN_TYPE_NOT_FOUND", $"Token type '{id}' not found."));

        entity.IsActive = false;
        entity.LastUpdateBy = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Token type deactivated."));
    }
}
