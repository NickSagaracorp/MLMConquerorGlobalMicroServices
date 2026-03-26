using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateProfile;

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateProfileHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;
        var req = command.Request;

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<ProfileDto>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        member.FirstName = req.FirstName;
        member.LastName = req.LastName;
        member.Phone = req.Phone;
        member.WhatsApp = req.WhatsApp;
        member.Country = req.Country ?? member.Country;
        member.State = req.State;
        member.City = req.City;
        member.Address = req.Address;
        member.ZipCode = req.ZipCode;
        member.BusinessName = req.BusinessName;
        member.ShowBusinessName = req.ShowBusinessName;
        member.LastUpdateDate = _dateTime.UtcNow;
        member.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

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
