using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.GetProfile;

public class GetProfileHandler : IRequestHandler<GetProfileQuery, Result<ProfileDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetProfileHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<ProfileDto>> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<ProfileDto>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        var dto = new ProfileDto
        {
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Email = _currentUser.Email,
            Phone = member.Phone,
            WhatsApp = member.WhatsApp,
            Country = member.Country,
            State = member.State,
            City = member.City,
            BusinessName = member.BusinessName,
            PhotoUrl = member.ProfilePhotoUrl,
            MemberType = member.MemberType.ToString(),
            Status = member.Status.ToString(),
            EnrollDate = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId
        };

        return Result<ProfileDto>.Success(dto);
    }
}
