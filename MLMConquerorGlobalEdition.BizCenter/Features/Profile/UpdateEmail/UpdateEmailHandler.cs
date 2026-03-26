using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateEmail;

public class UpdateEmailHandler : IRequestHandler<UpdateEmailCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateEmailHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(UpdateEmailCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == memberId, ct);

        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", "Member profile not found.");

        // Email is managed via ASP.NET Identity. We only update the audit fields to record the request.
        member.LastUpdateDate = _dateTime.UtcNow;
        member.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
