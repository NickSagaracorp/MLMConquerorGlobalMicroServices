using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;

public class GenerateCertificateHandler : IRequestHandler<GenerateCertificateCommand, Result<CertificateGenerationResponse>>
{
    private readonly AppDbContext _db;
    private readonly ICertificatePdfFillerService _pdfFiller;
    private readonly ICertificateStorage _storage;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public GenerateCertificateHandler(
        AppDbContext db,
        ICertificatePdfFillerService pdfFiller,
        ICertificateStorage storage,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _pdfFiller   = pdfFiller;
        _storage     = storage;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CertificateGenerationResponse>> Handle(
        GenerateCertificateCommand command, CancellationToken ct)
    {
        var history = await _db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .FirstOrDefaultAsync(h => h.Id == command.MemberRankHistoryId && !h.IsDeleted, ct);

        if (history is null)
            return Result<CertificateGenerationResponse>.Failure(
                "RANK_HISTORY_NOT_FOUND",
                $"Rank history record '{command.MemberRankHistoryId}' not found.");

        if (history.RankDefinition is null)
            return Result<CertificateGenerationResponse>.Failure(
                "RANK_DEFINITION_MISSING",
                "Rank definition not found for this history record.");

        if (!CertificateRules.IsCertificateEligible(history.RankDefinition.SortOrder))
            return Result<CertificateGenerationResponse>.Failure(
                "RANK_NOT_CERTIFICATE_ELIGIBLE",
                $"Rank '{history.RankDefinition.Name}' is not eligible for an achievement certificate.");

        // The earliest non-deleted record for this (member, rank) pair is THE certificate
        // record: it carries the URL, and its AchievedAt is the first-achievement date
        // stamped on the PDF. ThenBy(Id) is a deterministic tie-breaker on equal dates.
        var certRecord = await _db.MemberRankHistories
            .Where(h => h.MemberId == history.MemberId &&
                        h.RankDefinitionId == history.RankDefinitionId &&
                        !h.IsDeleted)
            .OrderBy(h => h.AchievedAt)
            .ThenBy(h => h.Id)
            .FirstAsync(ct);

        // Already generated and not a forced regeneration → return the cached URL.
        if (certRecord.GeneratedCertificateUrl is not null && !command.Force)
            return Result<CertificateGenerationResponse>.Success(new CertificateGenerationResponse
            {
                MemberRankHistoryId = certRecord.Id,
                MemberId            = history.MemberId,
                RankName            = history.RankDefinition.Name,
                CertificateUrl      = certRecord.GeneratedCertificateUrl,
                GeneratedAt         = certRecord.LastUpdateDate
            });

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == history.MemberId, ct);

        if (member is null)
            return Result<CertificateGenerationResponse>.Failure(
                "MEMBER_NOT_FOUND",
                $"Member '{history.MemberId}' not found.");

        var fullName = $"{member.FirstName} {member.LastName}".Trim();

        var templateData = new CertificateTemplateData(
            FullName:   fullName,
            MemberId:   member.MemberId,
            RankName:   history.RankDefinition.Name,
            AchievedAt: certRecord.AchievedAt);

        var pdfBytes = await _pdfFiller.FillAsync(
            history.RankDefinition.SortOrder, templateData, ct);

        var fileName = CertificateFileNaming.Build(
            member.Id, member.MemberId, history.RankDefinition.Name);

        var certificateUrl = await _storage.SaveAsync(fileName, pdfBytes, ct);

        var now = _dateTime.Now;
        certRecord.GeneratedCertificateUrl = certificateUrl;
        certRecord.LastUpdateDate          = now;
        certRecord.LastUpdateBy            = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<CertificateGenerationResponse>.Success(new CertificateGenerationResponse
        {
            MemberRankHistoryId = certRecord.Id,
            MemberId            = member.MemberId,
            RankName            = history.RankDefinition.Name,
            CertificateUrl      = certificateUrl,
            GeneratedAt         = now
        });
    }
}
