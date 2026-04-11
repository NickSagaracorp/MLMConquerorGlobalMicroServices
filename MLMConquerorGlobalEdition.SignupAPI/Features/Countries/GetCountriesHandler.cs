using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Countries;

public record GetCountriesQuery : IRequest<Result<List<CountryLookupDto>>>;

public record CountryLookupDto(
    string Iso2, string Iso3, string NameEn, string NameNative,
    string DefaultLanguageCode, string FlagEmoji, string? PhoneCode, bool IsActive);

public class GetCountriesHandler : IRequestHandler<GetCountriesQuery, Result<List<CountryLookupDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private const string CacheKey = "countries:all";

    public GetCountriesHandler(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<List<CountryLookupDto>>> Handle(GetCountriesQuery request, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<List<CountryLookupDto>>(CacheKey, ct);
        if (cached is not null)
            return Result<List<CountryLookupDto>>.Success(cached);

        var countries = await _db.Countries
            .AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn)
            .Select(x => new CountryLookupDto(
                x.Iso2, x.Iso3, x.NameEn, x.NameNative,
                x.DefaultLanguageCode, x.FlagEmoji, x.PhoneCode, x.IsActive))
            .ToListAsync(ct);

        var ttl = TimeSpan.FromHours(24);
        await _cache.SetAsync(CacheKey, countries, ttl, ct);

        return Result<List<CountryLookupDto>>.Success(countries);
    }
}
