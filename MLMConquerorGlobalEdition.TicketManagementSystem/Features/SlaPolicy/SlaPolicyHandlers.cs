using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using SlaEntity = MLMConquerorGlobalEdition.Domain.Entities.Support.SlaPolicy;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.SlaPolicy;

public class CreateSlaPolicyHandler : IRequestHandler<CreateSlaPolicyCommand, Result<SlaPolicyDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateSlaPolicyHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<SlaPolicyDto>> Handle(CreateSlaPolicyCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<SlaPolicyDto>.Failure("FORBIDDEN", "Only admins can manage SLA policies.");

        if (request.Request.WarningThresholdPercent is < 50 or > 95)
            return Result<SlaPolicyDto>.Failure("INVALID_THRESHOLD", "Warning threshold must be between 50 and 95.");

        var now = _dateTime.Now;

        // If this is set as default, unset others
        if (request.Request.IsDefault)
        {
            var others = await _db.SlaPolicies.Where(p => p.IsDefault && !p.IsDeleted).ToListAsync(ct);
            others.ForEach(p => p.IsDefault = false);
        }

        var policy = new SlaEntity
        {
            Name                        = request.Request.Name,
            Description                 = request.Request.Description,
            FirstResponseCriticalMinutes = request.Request.FirstResponseCriticalMinutes,
            FirstResponseHighMinutes    = request.Request.FirstResponseHighMinutes,
            FirstResponseNormalMinutes  = request.Request.FirstResponseNormalMinutes,
            FirstResponseLowMinutes     = request.Request.FirstResponseLowMinutes,
            ResolutionCriticalMinutes   = request.Request.ResolutionCriticalMinutes,
            ResolutionHighMinutes       = request.Request.ResolutionHighMinutes,
            ResolutionNormalMinutes     = request.Request.ResolutionNormalMinutes,
            ResolutionLowMinutes        = request.Request.ResolutionLowMinutes,
            Timezone                    = request.Request.Timezone,
            WorkdaysJson                = request.Request.WorkdaysJson,
            BusinessHoursStart          = request.Request.BusinessHoursStart,
            BusinessHoursEnd            = request.Request.BusinessHoursEnd,
            WarningThresholdPercent     = request.Request.WarningThresholdPercent,
            NotifyAgentAtMinutes        = request.Request.NotifyAgentAtMinutes,
            NotifySupervisorAtMinutes   = request.Request.NotifySupervisorAtMinutes,
            NotifyManagerAtMinutes      = request.Request.NotifyManagerAtMinutes,
            IsDefault                   = request.Request.IsDefault,
            IsActive                    = true,
            CreationDate                = now,
            CreatedBy                   = _currentUser.UserId,
            LastUpdateDate              = now,
            LastUpdateBy                = _currentUser.UserId
        };

        await _db.SlaPolicies.AddAsync(policy, ct);
        await _db.SaveChangesAsync(ct);
        return Result<SlaPolicyDto>.Success(Map(policy));
    }

    private static SlaPolicyDto Map(SlaEntity p) => new()
    {
        Id                          = p.Id,
        Name                        = p.Name,
        Description                 = p.Description,
        FirstResponseCriticalMinutes = p.FirstResponseCriticalMinutes,
        FirstResponseHighMinutes    = p.FirstResponseHighMinutes,
        FirstResponseNormalMinutes  = p.FirstResponseNormalMinutes,
        FirstResponseLowMinutes     = p.FirstResponseLowMinutes,
        ResolutionCriticalMinutes   = p.ResolutionCriticalMinutes,
        ResolutionHighMinutes       = p.ResolutionHighMinutes,
        ResolutionNormalMinutes     = p.ResolutionNormalMinutes,
        ResolutionLowMinutes        = p.ResolutionLowMinutes,
        Timezone                    = p.Timezone,
        WorkdaysJson                = p.WorkdaysJson,
        BusinessHoursStart          = p.BusinessHoursStart,
        BusinessHoursEnd            = p.BusinessHoursEnd,
        WarningThresholdPercent     = p.WarningThresholdPercent,
        IsDefault                   = p.IsDefault,
        IsActive                    = p.IsActive
    };
}

public class UpdateSlaPolicyHandler : IRequestHandler<UpdateSlaPolicyCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateSlaPolicyHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(UpdateSlaPolicyCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Only admins can manage SLA policies.");

        var policy = await _db.SlaPolicies
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && !p.IsDeleted, ct);

        if (policy is null)
            return Result<bool>.Failure("SLA_POLICY_NOT_FOUND", "SLA policy not found.");

        if (request.Request.WarningThresholdPercent is < 50 or > 95)
            return Result<bool>.Failure("INVALID_THRESHOLD", "Warning threshold must be between 50 and 95.");

        var now = _dateTime.Now;

        if (request.Request.IsDefault && !policy.IsDefault)
        {
            var others = await _db.SlaPolicies.Where(p => p.IsDefault && p.Id != policy.Id && !p.IsDeleted).ToListAsync(ct);
            others.ForEach(p => p.IsDefault = false);
        }

        policy.Name                        = request.Request.Name;
        policy.Description                 = request.Request.Description;
        policy.FirstResponseCriticalMinutes = request.Request.FirstResponseCriticalMinutes;
        policy.FirstResponseHighMinutes    = request.Request.FirstResponseHighMinutes;
        policy.FirstResponseNormalMinutes  = request.Request.FirstResponseNormalMinutes;
        policy.FirstResponseLowMinutes     = request.Request.FirstResponseLowMinutes;
        policy.ResolutionCriticalMinutes   = request.Request.ResolutionCriticalMinutes;
        policy.ResolutionHighMinutes       = request.Request.ResolutionHighMinutes;
        policy.ResolutionNormalMinutes     = request.Request.ResolutionNormalMinutes;
        policy.ResolutionLowMinutes        = request.Request.ResolutionLowMinutes;
        policy.Timezone                    = request.Request.Timezone;
        policy.WorkdaysJson                = request.Request.WorkdaysJson;
        policy.BusinessHoursStart          = request.Request.BusinessHoursStart;
        policy.BusinessHoursEnd            = request.Request.BusinessHoursEnd;
        policy.WarningThresholdPercent     = request.Request.WarningThresholdPercent;
        policy.NotifyAgentAtMinutes        = request.Request.NotifyAgentAtMinutes;
        policy.NotifySupervisorAtMinutes   = request.Request.NotifySupervisorAtMinutes;
        policy.NotifyManagerAtMinutes      = request.Request.NotifyManagerAtMinutes;
        policy.IsDefault                   = request.Request.IsDefault;
        policy.LastUpdateDate              = now;
        policy.LastUpdateBy                = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class DeleteSlaPolicyHandler : IRequestHandler<DeleteSlaPolicyCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteSlaPolicyHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(DeleteSlaPolicyCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Only admins can delete SLA policies.");

        var policy = await _db.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.PolicyId && !p.IsDeleted, ct);
        if (policy is null)
            return Result<bool>.Failure("SLA_POLICY_NOT_FOUND", "SLA policy not found.");

        var now = _dateTime.Now;
        policy.IsDeleted   = true;
        policy.DeletedAt   = now;
        policy.DeletedBy   = _currentUser.UserId;
        policy.IsActive    = false;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class GetSlaPoliciesHandler : IRequestHandler<GetSlaPoliciesQuery, Result<IEnumerable<SlaPolicyDto>>>
{
    private readonly AppDbContext _db;

    public GetSlaPoliciesHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<SlaPolicyDto>>> Handle(GetSlaPoliciesQuery request, CancellationToken ct)
    {
        var policies = await _db.SlaPolicies
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        var result = policies.Select(p => new SlaPolicyDto
        {
            Id                          = p.Id,
            Name                        = p.Name,
            Description                 = p.Description,
            FirstResponseCriticalMinutes = p.FirstResponseCriticalMinutes,
            FirstResponseHighMinutes    = p.FirstResponseHighMinutes,
            FirstResponseNormalMinutes  = p.FirstResponseNormalMinutes,
            FirstResponseLowMinutes     = p.FirstResponseLowMinutes,
            ResolutionCriticalMinutes   = p.ResolutionCriticalMinutes,
            ResolutionHighMinutes       = p.ResolutionHighMinutes,
            ResolutionNormalMinutes     = p.ResolutionNormalMinutes,
            ResolutionLowMinutes        = p.ResolutionLowMinutes,
            Timezone                    = p.Timezone,
            WorkdaysJson                = p.WorkdaysJson,
            BusinessHoursStart          = p.BusinessHoursStart,
            BusinessHoursEnd            = p.BusinessHoursEnd,
            WarningThresholdPercent     = p.WarningThresholdPercent,
            IsDefault                   = p.IsDefault,
            IsActive                    = p.IsActive
        });

        return Result<IEnumerable<SlaPolicyDto>>.Success(result);
    }
}
