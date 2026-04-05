using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.SupportAdmin;

public record GetSupportTeamsQuery() : IRequest<Result<IEnumerable<SupportTeamDto>>>;
public record CreateSupportTeamCommand(CreateSupportTeamRequest Request) : IRequest<Result<SupportTeamDto>>;
public record UpdateSupportTeamCommand(int TeamId, CreateSupportTeamRequest Request) : IRequest<Result<bool>>;
public record GetSupportAgentsQuery(int? TeamId = null) : IRequest<Result<IEnumerable<AgentWorkloadDto>>>;
public record CreateSupportAgentCommand(CreateSupportAgentRequest Request) : IRequest<Result<AgentWorkloadDto>>;
public record UpdateAgentAvailabilityCommand(string AgentId, string Availability) : IRequest<Result<bool>>;

public class GetSupportTeamsHandler : IRequestHandler<GetSupportTeamsQuery, Result<IEnumerable<SupportTeamDto>>>
{
    private readonly AppDbContext _db;

    public GetSupportTeamsHandler(AppDbContext db) => _db = db;

    public async Task<Result<IEnumerable<SupportTeamDto>>> Handle(GetSupportTeamsQuery request, CancellationToken ct)
    {
        var teams = await _db.SupportTeams
            .Include(t => t.Agents)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return Result<IEnumerable<SupportTeamDto>>.Success(teams.Select(t => new SupportTeamDto
        {
            Id                = t.Id,
            Name              = t.Name,
            Description       = t.Description,
            SupervisorAgentId = t.SupervisorAgentId,
            RoutingMethod     = t.RoutingMethod,
            IsActive          = t.IsActive,
            AgentCount        = t.Agents.Count
        }));
    }
}

public class CreateSupportTeamHandler : IRequestHandler<CreateSupportTeamCommand, Result<SupportTeamDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateSupportTeamHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<SupportTeamDto>> Handle(CreateSupportTeamCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<SupportTeamDto>.Failure("FORBIDDEN", "Only admins can manage support teams.");

        var now = _dateTime.Now;
        var team = new SupportTeam
        {
            Name              = request.Request.Name,
            Description       = request.Request.Description,
            SupervisorAgentId = request.Request.SupervisorAgentId,
            RoutingMethod     = request.Request.RoutingMethod,
            IsActive          = true,
            CreationDate      = now,
            CreatedBy         = _currentUser.UserId,
            LastUpdateDate    = now,
            LastUpdateBy      = _currentUser.UserId
        };

        await _db.SupportTeams.AddAsync(team, ct);
        await _db.SaveChangesAsync(ct);

        return Result<SupportTeamDto>.Success(new SupportTeamDto
        {
            Id = team.Id, Name = team.Name, Description = team.Description,
            SupervisorAgentId = team.SupervisorAgentId, RoutingMethod = team.RoutingMethod,
            IsActive = team.IsActive, AgentCount = 0
        });
    }
}

public class UpdateSupportTeamHandler : IRequestHandler<UpdateSupportTeamCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateSupportTeamHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(UpdateSupportTeamCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Only admins can manage support teams.");

        var team = await _db.SupportTeams.FindAsync([request.TeamId], ct);
        if (team is null)
            return Result<bool>.Failure("NOT_FOUND", "Support team not found.");

        var now = _dateTime.Now;
        team.Name              = request.Request.Name;
        team.Description       = request.Request.Description;
        team.SupervisorAgentId = request.Request.SupervisorAgentId;
        team.RoutingMethod     = request.Request.RoutingMethod;
        team.LastUpdateDate    = now;
        team.LastUpdateBy      = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public class GetSupportAgentsHandler : IRequestHandler<GetSupportAgentsQuery, Result<IEnumerable<AgentWorkloadDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSupportAgentsHandler(AppDbContext db, ICurrentUserService currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<Result<IEnumerable<AgentWorkloadDto>>> Handle(GetSupportAgentsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Supervisor"))
            return Result<IEnumerable<AgentWorkloadDto>>.Failure("FORBIDDEN", "Access denied.");

        var query = _db.SupportAgents.Where(a => !a.IsDeleted);
        if (request.TeamId.HasValue)
            query = query.Where(a => a.TeamId == request.TeamId);

        var agents = await query.OrderBy(a => a.DisplayName).ToListAsync(ct);

        return Result<IEnumerable<AgentWorkloadDto>>.Success(agents.Select(a => new AgentWorkloadDto
        {
            AgentId              = a.Id,
            DisplayName          = a.DisplayName,
            Email                = a.Email,
            Tier                 = a.Tier,
            Availability         = a.Availability,
            CurrentTicketCount   = a.CurrentTicketCount,
            MaxConcurrentTickets = a.MaxConcurrentTickets,
            WorkloadPercent      = a.MaxConcurrentTickets > 0 ? Math.Round((double)a.CurrentTicketCount / a.MaxConcurrentTickets * 100, 1) : 0
        }));
    }
}

public class CreateSupportAgentHandler : IRequestHandler<CreateSupportAgentCommand, Result<AgentWorkloadDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateSupportAgentHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<AgentWorkloadDto>> Handle(CreateSupportAgentCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<AgentWorkloadDto>.Failure("FORBIDDEN", "Only admins can create agents.");

        var exists = await _db.SupportAgents.AnyAsync(a => a.UserId == request.Request.UserId && !a.IsDeleted, ct);
        if (exists)
            return Result<AgentWorkloadDto>.Failure("AGENT_EXISTS", "An agent with this UserId already exists.");

        var now = _dateTime.Now;
        var agent = new SupportAgent
        {
            UserId               = request.Request.UserId,
            MemberId             = request.Request.MemberId,
            DisplayName          = request.Request.DisplayName,
            Email                = request.Request.Email,
            TeamId               = request.Request.TeamId,
            Tier                 = request.Request.Tier,
            SkillsJson           = request.Request.SkillsJson,
            LanguagesJson        = request.Request.LanguagesJson,
            MaxConcurrentTickets = request.Request.MaxConcurrentTickets,
            IsActive             = true,
            CreationDate         = now,
            CreatedBy            = _currentUser.UserId,
            LastUpdateDate       = now,
            LastUpdateBy         = _currentUser.UserId
        };

        await _db.SupportAgents.AddAsync(agent, ct);
        await _db.SaveChangesAsync(ct);

        return Result<AgentWorkloadDto>.Success(new AgentWorkloadDto
        {
            AgentId = agent.Id, DisplayName = agent.DisplayName,
            Email = agent.Email, Tier = agent.Tier,
            Availability = agent.Availability, CurrentTicketCount = 0,
            MaxConcurrentTickets = agent.MaxConcurrentTickets, WorkloadPercent = 0
        });
    }
}

public class UpdateAgentAvailabilityHandler : IRequestHandler<UpdateAgentAvailabilityCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateAgentAvailabilityHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<bool>> Handle(UpdateAgentAvailabilityCommand request, CancellationToken ct)
    {
        var validStatuses = new[] { "available", "busy", "offline" };
        if (!validStatuses.Contains(request.Availability))
            return Result<bool>.Failure("INVALID_STATUS", "Availability must be available, busy, or offline.");

        var agent = await _db.SupportAgents.FirstOrDefaultAsync(a => a.Id == request.AgentId && !a.IsDeleted, ct);
        if (agent is null)
            return Result<bool>.Failure("NOT_FOUND", "Agent not found.");

        if (!_currentUser.IsAdmin && agent.UserId != _currentUser.UserId)
            return Result<bool>.Failure("FORBIDDEN", "Agents can only update their own availability.");

        var now = _dateTime.Now;
        agent.Availability    = request.Availability;
        agent.LastUpdateDate  = now;
        agent.LastUpdateBy    = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
