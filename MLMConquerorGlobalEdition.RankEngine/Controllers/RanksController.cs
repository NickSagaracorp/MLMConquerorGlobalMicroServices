using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Features.DeleteCertificate;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Features.GetMemberCertificates;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Controllers;

[ApiController]
[Route("api/v1/ranks")]
[Authorize]
public class RanksController : ControllerBase
{
    private readonly IMediator _mediator;

    public RanksController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Returns all active rank definitions with their qualification requirements.
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(typeof(ApiResponse<List<RankDefinitionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDefinitions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRankDefinitionsQuery(), ct);
        return Ok(ApiResponse<List<RankDefinitionResponse>>.Ok(result.Value!));
    }

    /// <summary>
    /// Returns a member's current rank, next rank target, and real-time progress metrics.
    /// Members can view their own progress; admins can view any member.
    /// </summary>
    [HttpGet("progress/{memberId}")]
    [ProducesResponseType(typeof(ApiResponse<RankProgressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProgress(string memberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRankProgressQuery(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RankProgressResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Evaluates a member against all rank definitions and promotes if qualified.
    /// Admin only — also called by the nightly HangFire rank sweep job.
    /// </summary>
    [HttpPost("evaluate/{memberId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<RankEvaluationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Evaluate(string memberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new EvaluateRankCommand(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RankEvaluationResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Generates the certificate PDF for a rank history record. Admin only —
    /// EvaluateRank no longer auto-generates certificates, so this and the member
    /// self-service path are the only two ways a certificate gets created.
    /// </summary>
    [HttpPost("certificate/generate/{memberRankHistoryId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<CertificateGenerationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateCertificate(string memberRankHistoryId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateCertificateCommand(memberRankHistoryId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CertificateGenerationResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Member self-service certificate generation. The caller's JWT must own the
    /// MemberRankHistory record — admin role is NOT required. Used by BizCenter
    /// when the member taps "View certificate" and one has not been minted yet.
    /// Ownership is enforced by matching MemberRankHistory.MemberId against the
    /// JWT's MemberId claim — a mismatch returns 404 (we deliberately avoid
    /// leaking the existence of someone else's rank history).
    /// </summary>
    [HttpPost("certificate/member-generate/{memberRankHistoryId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CertificateGenerationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MemberGenerateCertificate(
        string memberRankHistoryId,
        [FromServices] AppDbContext db,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var memberId = currentUser.MemberId;
        if (string.IsNullOrEmpty(memberId))
            return Unauthorized(ApiResponse<object>.Fail(
                "MEMBER_ID_MISSING",
                "Caller does not have a MemberId claim."));

        // Ownership check first — return 404 (not 403) so we don't leak the existence
        // of someone else's rank history record.
        var owned = await db.MemberRankHistories
            .AsNoTracking()
            .AnyAsync(h => h.Id == memberRankHistoryId
                           && h.MemberId == memberId
                           && !h.IsDeleted, ct);

        if (!owned)
            return NotFound(ApiResponse<object>.Fail(
                "RANK_HISTORY_NOT_FOUND",
                "Rank history record not found."));

        var result = await _mediator.Send(new GenerateCertificateCommand(memberRankHistoryId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CertificateGenerationResponse>.Ok(result.Value!));
    }

    /// <summary>
    /// Force-regenerates a certificate (corrupt file or corrected member name). Rebuilds the
    /// PDF with the member's current name and the original first-achievement date. Admin only.
    /// </summary>
    [HttpPost("certificate/{memberRankHistoryId}/regenerate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<CertificateGenerationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateCertificate(string memberRankHistoryId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GenerateCertificateCommand(memberRankHistoryId, Force: true), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CertificateGenerationResponse>.Ok(result.Value!));
    }

    /// <summary>Deletes a member's certificate (file + stored URL). Admin only.</summary>
    [HttpDelete("certificate/{memberRankHistoryId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCertificate(string memberRankHistoryId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCertificateCommand(memberRankHistoryId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>Lists a member's certificate-eligible rank achievements with status. Admin only.</summary>
    [HttpGet("certificates/{memberId}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<List<MemberCertificateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberCertificates(string memberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberCertificatesQuery(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<List<MemberCertificateDto>>.Ok(result.Value!));
    }
}
