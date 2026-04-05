using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CannedResponses;

public record GetCannedResponsesQuery() : IRequest<Result<IEnumerable<CannedResponseDto>>>;
public record CreateCannedResponseCommand(CreateCannedResponseRequest Request) : IRequest<Result<CannedResponseDto>>;
public record UpdateCannedResponseCommand(string Id, CreateCannedResponseRequest Request) : IRequest<Result<bool>>;
public record DeleteCannedResponseCommand(string Id) : IRequest<Result<bool>>;
public record ApplyCannedResponseCommand(ApplyCannedResponseRequest Request) : IRequest<Result<string>>;

public class GetCannedResponsesHandler : IRequestHandler<GetCannedResponsesQuery, Result<IEnumerable<CannedResponseDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCannedResponsesHandler(AppDbContext db, ICurrentUserService currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<Result<IEnumerable<CannedResponseDto>>> Handle(GetCannedResponsesQuery request, CancellationToken ct)
    {
        var responses = await _db.CannedResponses
            .Where(r => !r.IsDeleted && r.IsActive
                && (r.Scope == "global"
                    || (r.Scope == "personal" && r.OwnerAgentId == _currentUser.UserId)
                    || (r.Scope == "team")))
            .OrderBy(r => r.Title)
            .ToListAsync(ct);

        return Result<IEnumerable<CannedResponseDto>>.Success(responses.Select(Map));
    }

    private static CannedResponseDto Map(CannedResponse r) => new()
    {
        Id         = r.Id,
        Title      = r.Title,
        Body       = r.Body,
        Category   = r.Category,
        TagsJson   = r.TagsJson,
        Scope      = r.Scope,
        UsageCount = r.UsageCount
    };
}

public class CreateCannedResponseHandler : IRequestHandler<CreateCannedResponseCommand, Result<CannedResponseDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCannedResponseHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<CannedResponseDto>> Handle(CreateCannedResponseCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent"))
            return Result<CannedResponseDto>.Failure("FORBIDDEN", "Only agents or admins can create canned responses.");

        var now = _dateTime.Now;
        var cr = new CannedResponse
        {
            Title          = request.Request.Title,
            Body           = request.Request.Body,
            Category       = request.Request.Category,
            TagsJson       = request.Request.TagsJson,
            Scope          = request.Request.Scope,
            OwnerAgentId   = request.Request.Scope == "personal" ? _currentUser.UserId : null,
            TeamId         = request.Request.Scope == "team" ? request.Request.TeamId : null,
            IsActive       = true,
            CreationDate   = now,
            CreatedBy      = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy   = _currentUser.UserId
        };

        await _db.CannedResponses.AddAsync(cr, ct);
        await _db.SaveChangesAsync(ct);

        return Result<CannedResponseDto>.Success(new CannedResponseDto
        {
            Id = cr.Id, Title = cr.Title, Body = cr.Body,
            Category = cr.Category, TagsJson = cr.TagsJson,
            Scope = cr.Scope, UsageCount = cr.UsageCount
        });
    }
}

public class UpdateCannedResponseHandler : IRequestHandler<UpdateCannedResponseCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateCannedResponseHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(UpdateCannedResponseCommand request, CancellationToken ct)
    {
        var cr = await _db.CannedResponses.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct);
        if (cr is null)
            return Result<bool>.Failure("NOT_FOUND", "Canned response not found.");

        if (!_currentUser.IsAdmin && cr.OwnerAgentId != _currentUser.UserId)
            return Result<bool>.Failure("FORBIDDEN", "Access denied.");

        var now = _dateTime.Now;
        cr.Title         = request.Request.Title;
        cr.Body          = request.Request.Body;
        cr.Category      = request.Request.Category;
        cr.TagsJson      = request.Request.TagsJson;
        cr.LastUpdateDate = now;
        cr.LastUpdateBy  = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class DeleteCannedResponseHandler : IRequestHandler<DeleteCannedResponseCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteCannedResponseHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(DeleteCannedResponseCommand request, CancellationToken ct)
    {
        var cr = await _db.CannedResponses.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct);
        if (cr is null)
            return Result<bool>.Failure("NOT_FOUND", "Canned response not found.");

        if (!_currentUser.IsAdmin && cr.OwnerAgentId != _currentUser.UserId)
            return Result<bool>.Failure("FORBIDDEN", "Access denied.");

        var now = _dateTime.Now;
        cr.IsDeleted      = true;
        cr.DeletedAt      = now;
        cr.DeletedBy      = _currentUser.UserId;
        cr.LastUpdateDate = now;
        cr.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class ApplyCannedResponseHandler : IRequestHandler<ApplyCannedResponseCommand, Result<string>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public ApplyCannedResponseHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
        => (_db, _dateTime, _currentUser) = (db, dateTime, currentUser);

    public async Task<Result<string>> Handle(ApplyCannedResponseCommand request, CancellationToken ct)
    {
        var cr = await _db.CannedResponses
            .FirstOrDefaultAsync(r => r.Id == request.Request.CannedResponseId && !r.IsDeleted && r.IsActive, ct);

        if (cr is null)
            return Result<string>.Failure("NOT_FOUND", "Canned response not found.");

        // Resolve variables
        var resolved = cr.Body
            .Replace("{{customerName}}", request.Request.CustomerName)
            .Replace("{{ticketNumber}}", request.Request.TicketNumber)
            .Replace("{{agentName}}", request.Request.AgentName)
            .Replace("{{category}}", request.Request.Category)
            .Replace("{{createdDate}}", _dateTime.Now.ToString("yyyy-MM-dd"));

        cr.UsageCount++;
        cr.LastUpdateDate = _dateTime.Now;
        cr.LastUpdateBy   = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);

        return Result<string>.Success(resolved);
    }
}
