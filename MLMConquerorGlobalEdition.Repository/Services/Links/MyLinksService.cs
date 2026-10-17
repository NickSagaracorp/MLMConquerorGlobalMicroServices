using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Links;

public sealed class MyLinkView
{
    public string Key   { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Url   { get; set; } = string.Empty;
}

public sealed class MyLinksView
{
    /// <summary>El miembro no tiene slug: los enlaces personales no se pueden construir.</summary>
    public bool SlugMissing { get; set; }

    public List<MyLinkView> Personal  { get; set; } = new();
    public List<MyLinkView> Corporate { get; set; } = new();
}

public interface IMyLinksService
{
    /// <summary>Los enlaces de <paramref name="memberId"/>. Quien llama pasa el miembro del token.</summary>
    Task<MyLinksView> GetAsync(string memberId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MyLinksService : IMyLinksService
{
    private readonly AppDbContext   _db;
    private readonly MyLinksOptions _options;

    public MyLinksService(AppDbContext db, MyLinksOptions options)
    {
        _db      = db;
        _options = options;
    }

    public async Task<MyLinksView> GetAsync(string memberId, CancellationToken ct = default)
    {
        var slug = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => m.ReplicateSiteSlug)
            .FirstOrDefaultAsync(ct);

        var view = new MyLinksView { SlugMissing = string.IsNullOrWhiteSpace(slug) };
        var escaped = view.SlugMissing ? string.Empty : Uri.EscapeDataString(slug!.Trim());

        foreach (var template in _options.Links)
        {
            if (!template.IsPersonal)
            {
                view.Corporate.Add(new MyLinkView { Key = template.Key, Label = template.Label, Url = template.Url });
                continue;
            }

            if (view.SlugMissing) continue;

            view.Personal.Add(new MyLinkView
            {
                Key   = template.Key,
                Label = template.Label,
                Url   = template.Url.Replace(MyLinksOptions.SlugToken, escaped, StringComparison.Ordinal)
            });
        }

        return view;
    }
}
