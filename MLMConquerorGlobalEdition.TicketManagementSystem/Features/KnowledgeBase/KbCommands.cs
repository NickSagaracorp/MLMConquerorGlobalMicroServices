using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.KnowledgeBase;

public record CreateKbArticleCommand(CreateKbArticleRequest Request) : IRequest<Result<KbArticleDto>>;
public record UpdateKbArticleCommand(string ArticleId, CreateKbArticleRequest Request) : IRequest<Result<bool>>;
public record PublishKbArticleCommand(string ArticleId) : IRequest<Result<bool>>;
public record DeleteKbArticleCommand(string ArticleId) : IRequest<Result<bool>>;
public record SubmitKbFeedbackCommand(string ArticleId, KbFeedbackRequest Request) : IRequest<Result<bool>>;
public record CreateKbArticleFromTicketCommand(string TicketId, CreateKbArticleRequest Request) : IRequest<Result<KbArticleDto>>;
public record GetKbArticleBySlugQuery(string Slug) : IRequest<Result<KbArticleDto>>;
public record SearchKnowledgeBaseQuery(string Q, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<KbArticleSummaryDto>>>;
public record GetKbSuggestionsQuery(string TitlePartial) : IRequest<Result<IEnumerable<KbArticleSummaryDto>>>;
