using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;

public class UpdateMemberStatusHandler : IRequestHandler<UpdateMemberStatusCommand, Result<AdminMemberDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateMemberStatusHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminMemberDto>> Handle(
        UpdateMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberId, cancellationToken);

        if (member is null)
            return Result<AdminMemberDto>.Failure("MEMBER_NOT_FOUND", $"Member '{request.MemberId}' not found.");

        var oldStatus = member.Status;
        var now = _dateTime.Now;

        member.Status = request.Request.Status;
        member.LastUpdateDate = now;
        member.LastUpdateBy = _currentUser.UserId;

        var history = new MemberStatusHistory
        {
            MemberId = member.MemberId,
            OldStatus = oldStatus,
            NewStatus = request.Request.Status,
            Reason = request.Request.Reason,
            ChangedAt = now,
            CreationDate = now,
            CreatedBy = _currentUser.UserId
        };

        await _db.MemberStatusHistories.AddAsync(history, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new AdminMemberDto
        {
            MemberId = member.MemberId,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Phone = member.Phone,
            Country = member.Country,
            Status = member.Status.ToString(),
            MemberType = member.MemberType.ToString(),
            EnrollDate = member.EnrollDate,
            SponsorMemberId = member.SponsorMemberId,
            CreationDate = member.CreationDate
        };

        return Result<AdminMemberDto>.Success(dto);
    }
}
