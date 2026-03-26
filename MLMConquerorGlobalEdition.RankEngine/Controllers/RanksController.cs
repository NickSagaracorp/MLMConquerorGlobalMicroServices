using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
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
    [Authorize(Roles = "Admin")]
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
    /// Generates (or regenerates) the certificate PDF URL for a specific rank history record.
    /// The actual PDF rendering is handled asynchronously by the certificate background service.
    /// </summary>
    [HttpPost("certificate/generate/{memberRankHistoryId}")]
    [ProducesResponseType(typeof(ApiResponse<CertificateGenerationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateCertificate(string memberRankHistoryId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateCertificateCommand(memberRankHistoryId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CertificateGenerationResponse>.Ok(result.Value!));
    }
}
