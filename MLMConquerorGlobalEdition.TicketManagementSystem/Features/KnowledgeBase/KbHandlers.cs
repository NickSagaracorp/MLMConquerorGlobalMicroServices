using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.KnowledgeBase;

public class CreateKbArticleHandler : IRequestHandler<CreateKbArticleCommand, Result<KbArticleDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateKbArticleHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<KbArticleDto>> Handle(CreateKbArticleCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent"))
            return Result<KbArticleDto>.Failure("FORBIDDEN", "Only agents or admins can create KB articles.");

        if (string.IsNullOrWhiteSpace(request.Request.Title) || request.Request.Title.Length < 5)
            return Result<KbArticleDto>.Failure("INVALID_TITLE", "Title must be at least 5 characters.");

        var slugExists = await _db.KbArticles
            .AnyAsync(a => a.Slug == request.Request.Slug && !a.IsDeleted, ct);

        if (slugExists)
            return Result<KbArticleDto>.Failure("DUPLICATE_SLUG", "An article with this slug already exists.");

        var now = _dateTime.Now;

        var article = new KnowledgeBaseArticle
        {
            Title          = request.Request.Title,
            Slug           = request.Request.Slug,
            Body           = request.Request.Body,
            CategoryId     = request.Request.CategoryId,
            TagsJson       = request.Request.TagsJson,
            Visibility     = request.Request.Visibility,
            AuthorAgentId  = _currentUser.UserId,
            SourceTicketId = request.Request.SourceTicketId,
            Version        = 1,
            CreationDate   = now,
            CreatedBy      = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy   = _currentUser.UserId
        };

        if (article.Visibility == KbVisibility.Public)
            article.PublishedAt = now;

        await _db.KbArticles.AddAsync(article, ct);
        await _db.SaveChangesAsync(ct);

        // Create initial version snapshot
        _db.KbArticleVersions.Add(new KbArticleVersion
        {
            ArticleId       = article.Id,
            VersionNumber   = 1,
            BodySnapshot    = article.Body,
            EditedByAgentId = _currentUser.UserId,
            ChangeNote      = request.Request.ChangeNote ?? "Initial version",
            CreationDate    = now,
            CreatedBy       = _currentUser.UserId
        });
        await _db.SaveChangesAsync(ct);

        return Result<KbArticleDto>.Success(Map(article));
    }

    internal static KbArticleDto Map(KnowledgeBaseArticle a, string? categoryName = null) => new()
    {
        Id             = a.Id,
        Title          = a.Title,
        Slug           = a.Slug,
        Body           = a.Body,
        CategoryId     = a.CategoryId,
        CategoryName   = categoryName,
        TagsJson       = a.TagsJson,
        Visibility     = a.Visibility.ToString(),
        AuthorAgentId  = a.AuthorAgentId,
        ViewCount      = a.ViewCount,
        HelpfulCount   = a.HelpfulCount,
        NotHelpfulCount = a.NotHelpfulCount,
        Version        = a.Version,
        PublishedAt    = a.PublishedAt,
        SourceTicketId = a.SourceTicketId,
        CreationDate   = a.CreationDate
    };
}

public class UpdateKbArticleHandler : IRequestHandler<UpdateKbArticleCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateKbArticleHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(UpdateKbArticleCommand request, CancellationToken ct)
    {
        var article = await _db.KbArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId && !a.IsDeleted, ct);
        if (article is null)
            return Result<bool>.Failure("KB_ARTICLE_NOT_FOUND", "Article not found.");

        if (!_currentUser.IsAdmin && article.AuthorAgentId != _currentUser.UserId)
            return Result<bool>.Failure("FORBIDDEN", "Only the author or an admin can edit this article.");

        if (request.Request.Slug != article.Slug)
        {
            var slugExists = await _db.KbArticles.AnyAsync(a => a.Slug == request.Request.Slug && a.Id != article.Id && !a.IsDeleted, ct);
            if (slugExists)
                return Result<bool>.Failure("DUPLICATE_SLUG", "An article with this slug already exists.");
        }

        var now = _dateTime.Now;
        article.Version++;

        // Snapshot current body as a version before overwriting
        _db.KbArticleVersions.Add(new KbArticleVersion
        {
            ArticleId       = article.Id,
            VersionNumber   = article.Version,
            BodySnapshot    = request.Request.Body,
            EditedByAgentId = _currentUser.UserId,
            ChangeNote      = request.Request.ChangeNote,
            CreationDate    = now,
            CreatedBy       = _currentUser.UserId
        });

        article.Title         = request.Request.Title;
        article.Slug          = request.Request.Slug;
        article.Body          = request.Request.Body;
        article.CategoryId    = request.Request.CategoryId;
        article.TagsJson      = request.Request.TagsJson;
        article.LastUpdateDate = now;
        article.LastUpdateBy  = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class PublishKbArticleHandler : IRequestHandler<PublishKbArticleCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public PublishKbArticleHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(PublishKbArticleCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Only admins can publish KB articles.");

        var article = await _db.KbArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId && !a.IsDeleted, ct);
        if (article is null)
            return Result<bool>.Failure("KB_ARTICLE_NOT_FOUND", "Article not found.");

        var now = _dateTime.Now;
        article.Visibility     = KbVisibility.Public;
        article.PublishedAt    = now;
        article.LastUpdateDate = now;
        article.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class DeleteKbArticleHandler : IRequestHandler<DeleteKbArticleCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteKbArticleHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(DeleteKbArticleCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Only admins can delete KB articles.");

        var article = await _db.KbArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId && !a.IsDeleted, ct);
        if (article is null)
            return Result<bool>.Failure("KB_ARTICLE_NOT_FOUND", "Article not found.");

        var now = _dateTime.Now;
        article.IsDeleted      = true;
        article.DeletedAt      = now;
        article.DeletedBy      = _currentUser.UserId;
        article.LastUpdateDate = now;
        article.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class SubmitKbFeedbackHandler : IRequestHandler<SubmitKbFeedbackCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public SubmitKbFeedbackHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
        => (_db, _dateTime, _currentUser) = (db, dateTime, currentUser);

    public async Task<Result<bool>> Handle(SubmitKbFeedbackCommand request, CancellationToken ct)
    {
        var article = await _db.KbArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId && !a.IsDeleted, ct);
        if (article is null)
            return Result<bool>.Failure("KB_ARTICLE_NOT_FOUND", "Article not found.");

        if (request.Request.IsHelpful)
            article.HelpfulCount++;
        else
            article.NotHelpfulCount++;

        article.LastUpdateDate = _dateTime.Now;
        article.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class CreateKbArticleFromTicketHandler : IRequestHandler<CreateKbArticleFromTicketCommand, Result<KbArticleDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateKbArticleFromTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<KbArticleDto>> Handle(CreateKbArticleFromTicketCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId && !t.IsDeleted, ct);
        if (ticket is null)
            return Result<KbArticleDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed)
            return Result<KbArticleDto>.Failure("TICKET_NOT_RESOLVED", "KB articles can only be created from resolved or closed tickets.");

        var req = new CreateKbArticleRequest
        {
            Title          = request.Request.Title,
            Slug           = request.Request.Slug,
            Body           = request.Request.Body,
            CategoryId     = request.Request.CategoryId,
            TagsJson       = request.Request.TagsJson,
            Visibility     = request.Request.Visibility,
            SourceTicketId = ticket.Id,
            ChangeNote     = request.Request.ChangeNote
        };
        var handler = new CreateKbArticleHandler(_db, _currentUser, _dateTime);
        return await handler.Handle(new CreateKbArticleCommand(req), ct);
    }
}

public class GetKbArticleBySlugHandler : IRequestHandler<GetKbArticleBySlugQuery, Result<KbArticleDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public GetKbArticleBySlugHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<KbArticleDto>> Handle(GetKbArticleBySlugQuery request, CancellationToken ct)
    {
        var isAgent = _currentUser.IsAdmin || _currentUser.Roles.Contains("Agent");

        var article = await _db.KbArticles
            .Include(a => a.Category)
            .Where(a => a.Slug == request.Slug && !a.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (article is null)
            return Result<KbArticleDto>.Failure("KB_ARTICLE_NOT_FOUND", "Article not found.");

        if (article.Visibility == KbVisibility.Draft && article.AuthorAgentId != _currentUser.UserId && !_currentUser.IsAdmin)
            return Result<KbArticleDto>.Failure("FORBIDDEN", "Access denied.");

        if (article.Visibility == KbVisibility.Internal && !isAgent)
            return Result<KbArticleDto>.Failure("FORBIDDEN", "Access denied.");

        // Increment view count
        article.ViewCount++;
        article.LastUpdateDate = _dateTime.Now;
        article.LastUpdateBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);

        return Result<KbArticleDto>.Success(CreateKbArticleHandler.Map(article, article.Category?.Name));
    }
}

public class SearchKnowledgeBaseHandler : IRequestHandler<SearchKnowledgeBaseQuery, Result<PagedResult<KbArticleSummaryDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchKnowledgeBaseHandler(AppDbContext db, ICurrentUserService currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<Result<PagedResult<KbArticleSummaryDto>>> Handle(SearchKnowledgeBaseQuery request, CancellationToken ct)
    {
        var isAgent = _currentUser.IsAdmin || _currentUser.Roles.Contains("Agent");

        var q = _db.KbArticles
            .Include(a => a.Category)
            .Where(a => !a.IsDeleted);

        if (!isAgent)
            q = q.Where(a => a.Visibility == KbVisibility.Public);
        else
            q = q.Where(a => a.Visibility != KbVisibility.Draft || a.AuthorAgentId == _currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(request.Q))
            q = q.Where(a => a.Title.Contains(request.Q) || a.Body.Contains(request.Q) || a.TagsJson.Contains(request.Q));

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.HelpfulCount)
            .ThenByDescending(a => a.ViewCount)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var result = new PagedResult<KbArticleSummaryDto>
        {
            Items      = items.Select(a => MapSummary(a, a.Category?.Name)),
            TotalCount = total,
            Page       = request.Page,
            PageSize   = request.PageSize
        };

        return Result<PagedResult<KbArticleSummaryDto>>.Success(result);
    }

    internal static KbArticleSummaryDto MapSummary(KnowledgeBaseArticle a, string? categoryName) => new()
    {
        Id              = a.Id,
        Title           = a.Title,
        Slug            = a.Slug,
        CategoryId      = a.CategoryId,
        CategoryName    = categoryName,
        Visibility      = a.Visibility.ToString(),
        ViewCount       = a.ViewCount,
        HelpfulCount    = a.HelpfulCount,
        NotHelpfulCount = a.NotHelpfulCount,
        Version         = a.Version,
        PublishedAt     = a.PublishedAt,
        CreationDate    = a.CreationDate
    };
}

public class GetKbSuggestionsHandler : IRequestHandler<GetKbSuggestionsQuery, Result<IEnumerable<KbArticleSummaryDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetKbSuggestionsHandler(AppDbContext db, ICurrentUserService currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<Result<IEnumerable<KbArticleSummaryDto>>> Handle(GetKbSuggestionsQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TitlePartial) || request.TitlePartial.Length < 2)
            return Result<IEnumerable<KbArticleSummaryDto>>.Success(Enumerable.Empty<KbArticleSummaryDto>());

        var isAgent = _currentUser.IsAdmin || _currentUser.Roles.Contains("Agent");

        var items = await _db.KbArticles
            .Include(a => a.Category)
            .Where(a => !a.IsDeleted
                     && (isAgent ? a.Visibility != KbVisibility.Draft : a.Visibility == KbVisibility.Public)
                     && (a.Title.Contains(request.TitlePartial) || a.Body.Contains(request.TitlePartial)))
            .OrderByDescending(a => a.HelpfulCount)
            .ThenByDescending(a => a.ViewCount)
            .Take(3)
            .ToListAsync(ct);

        return Result<IEnumerable<KbArticleSummaryDto>>.Success(
            items.Select(a => SearchKnowledgeBaseHandler.MapSummary(a, a.Category?.Name)));
    }
}
