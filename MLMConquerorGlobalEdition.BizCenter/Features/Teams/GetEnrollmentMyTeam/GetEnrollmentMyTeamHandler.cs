using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentMyTeam;

/// <summary>
/// BizCenter "my team" endpoint. Delegates to the shared <see cref="IEnrollmentTeamService"/>.
/// Do NOT add query logic here.
/// </summary>
public class GetEnrollmentMyTeamHandler
    : IRequestHandler<GetEnrollmentMyTeamQuery, Result<PagedResult<EnrollmentMyTeamMemberDto>>>
{
    private readonly IEnrollmentTeamService _service;
    private readonly ICurrentUserService    _currentUser;

    public GetEnrollmentMyTeamHandler(IEnrollmentTeamService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EnrollmentMyTeamMemberDto>>> Handle(
        GetEnrollmentMyTeamQuery request, CancellationToken ct)
    {
        var view = await _service.GetMyTeamAsync(
            _currentUser.MemberId, request.Page, request.PageSize,
            request.Search, request.From, request.To, ct);

        var mapped = new PagedResult<EnrollmentMyTeamMemberDto>
        {
            TotalCount = view.TotalCount,
            Page       = view.Page,
            PageSize   = view.PageSize,
            Items      = view.Items.Select(MapToDto).ToList()
        };
        return Result<PagedResult<EnrollmentMyTeamMemberDto>>.Success(mapped);
    }

    private static EnrollmentMyTeamMemberDto MapToDto(EnrollmentMyTeamMemberView v) => new()
    {
        MemberId             = v.MemberId,
        FullName             = v.FullName,
        Email                = v.Email,
        Phone                = v.Phone,
        Country              = v.Country,
        Level                = v.Level,
        EnrollDate           = v.EnrollDate,
        SponsorMemberId      = v.SponsorMemberId,
        SponsorFullName      = v.SponsorFullName,
        DualUplineMemberId   = v.DualUplineMemberId,
        DualUplineFullName   = v.DualUplineFullName,
        AccountStatus        = v.AccountStatus,
        MembershipStatus     = v.MembershipStatus,
        IsQualified          = v.IsQualified,
        MembershipLevelName  = v.MembershipLevelName,
        CurrentRankName      = v.CurrentRankName,
        RankDate             = v.RankDate,
        LifetimeRankName     = v.LifetimeRankName,
        NextRankPercent      = v.NextRankPercent,
        QualificationPoints  = v.QualificationPoints,
        EnrollmentTeamPoints = v.EnrollmentTeamPoints,
        LeftTeamPoints       = v.LeftTeamPoints,
        RightTeamPoints      = v.RightTeamPoints,
        SuspensionDate       = v.SuspensionDate,
        CancellationDate     = v.CancellationDate,
        LastPaymentDate      = v.LastPaymentDate
    };
}
