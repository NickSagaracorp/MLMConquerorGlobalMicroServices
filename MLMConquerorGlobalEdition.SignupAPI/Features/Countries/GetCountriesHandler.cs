using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Countries;

public record GetCountriesQuery : IRequest<Result<List<CountryLookupDto>>>;

public record CountryLookupDto(
    string Iso2, string Iso3, string NameEn, string NameNative,
    string DefaultLanguageCode, string FlagEmoji, string? PhoneCode, bool IsActive, bool HasStates);

public class GetCountriesHandler : IRequestHandler<GetCountriesQuery, Result<List<CountryLookupDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private const string CacheKey = "countries:active";

    public GetCountriesHandler(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<List<CountryLookupDto>>> Handle(GetCountriesQuery request, CancellationToken ct)
    {
        // Cache is best-effort — Redis being down must never block the signup page
        List<CountryLookupDto>? cached = null;
        try { cached = await _cache.GetAsync<List<CountryLookupDto>>(CacheKey, ct); }
        catch { /* cache unavailable — fall through to DB */ }

        if (cached is not null)
            return Result<List<CountryLookupDto>>.Success(cached);

        var countries = await _db.Countries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn)
            .Select(x => new CountryLookupDto(
                x.Iso2, x.Iso3, x.NameEn, x.NameNative,
                x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode, x.IsActive, x.HasStates))
            .ToListAsync(ct);

        try { await _cache.SetAsync(CacheKey, countries, TimeSpan.FromHours(24), ct); }
        catch { /* cache write is best-effort */ }

        return Result<List<CountryLookupDto>>.Success(countries);
    }
}
