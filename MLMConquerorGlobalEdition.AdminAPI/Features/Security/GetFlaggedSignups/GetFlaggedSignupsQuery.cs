using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignups;

/// <summary>
/// Paginated query for the AdminWeb "Flagged Signups" screen.
/// All filters are optional and ANDed together; defaults return the most recent events.
/// </summary>
public record GetFlaggedSignupsQuery(
    string?  VisitorId,
    string?  IpAddress,
    string?  SponsorReplicateSite,
    DateTime? From,
    DateTime? To,
    bool     OnlyFlagged,
    bool     IncludeCleared,
    int      Page,
    int      PageSize
) : IRequest<Result<PagedResult<FlaggedSignupDto>>>;
