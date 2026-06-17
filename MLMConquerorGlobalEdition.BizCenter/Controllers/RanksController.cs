using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.DownloadCertificate;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GenerateCertificateOnDemand;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/ranks")]
[Authorize]
public class RanksController : ControllerBase
{
    private readonly IMediator          _mediator;
    private readonly IHttpClientFactory _httpClientFactory;

    public RanksController(IMediator mediator, IHttpClientFactory httpClientFactory)
    {
        _mediator          = mediator;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>GET /api/v1/bizcenter/ranks/dashboard — current rank and progression stats</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetRankDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRankDashboardQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<RankDashboardDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<RankDashboardDto>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/ranks/history — full rank achievement history, newest first.
    /// HasCertificate=true on an entry means a certificate is ready to download.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetRankHistory(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct     = default)
    {
        var result = await _mediator.Send(new GetRankHistoryQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<RankHistoryDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<RankHistoryDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/ranks/{rankHistoryId}/certificate
    /// Streams the certificate PDF for the specified rank achievement.
    /// The file is served directly from this API — no storage provider URL is exposed to the client.
    /// Only the owner may download their own certificates.
    /// Lazy generation: if no certificate exists yet, one is minted via RankEngine
    /// transparently before the PDF is streamed back. Returns 404 only if the
    /// rank history record itself doesn't exist or isn't owned by the caller.
    /// </summary>
    [HttpGet("{rankHistoryId}/certificate")]
    public async Task<IActionResult> DownloadCertificate(
        string rankHistoryId,
        CancellationToken ct = default)
    {
        var bearer = ExtractBearerToken();
        var result = await _mediator.Send(new DownloadCertificateQuery(rankHistoryId, bearer), ct);

        if (!result.IsSuccess)
        {
            var errorResponse = ApiResponse<string>.Fail(result.ErrorCode!, result.Error!);
            return result.ErrorCode == "RANK_HISTORY_NOT_FOUND"
                ? NotFound(errorResponse)
                : BadRequest(errorResponse);
        }

        // Fetch the PDF from storage and proxy it through this API.
        // The S3 URL is never exposed to the caller.
        var client = _httpClientFactory.CreateClient("certificates");
        var pdfResponse = await client.GetAsync(result.Value!, ct);

        if (!pdfResponse.IsSuccessStatusCode)
            return StatusCode(502, ApiResponse<string>.Fail(
                "CERTIFICATE_FETCH_FAILED",
                "The certificate file could not be retrieved. Please try again later."));

        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync(ct);

        return File(
            pdfBytes,
            "application/pdf",
            $"certificate-{rankHistoryId}.pdf");
    }

    /// <summary>
    /// POST /api/v1/bizcenter/ranks/{rankHistoryId}/certificate
    /// Member-initiated on-demand certificate generation. Triggers the
    /// RankEngine generation flow for the specified rank history record and
    /// returns the resulting certificate URL on success. Ownership is enforced
    /// by both BizCenter (handler) and RankEngine (controller); 404 is returned
    /// when the caller does not own the record or it does not exist.
    /// </summary>
    [HttpPost("{rankHistoryId}/certificate")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateCertificate(
        string rankHistoryId,
        CancellationToken ct = default)
    {
        var bearer = ExtractBearerToken();
        var result = await _mediator.Send(
            new GenerateCertificateOnDemandCommand(rankHistoryId, bearer), ct);

        if (!result.IsSuccess)
        {
            var errorResponse = ApiResponse<string>.Fail(result.ErrorCode!, result.Error!);
            return result.ErrorCode == "RANK_HISTORY_NOT_FOUND"
                ? NotFound(errorResponse)
                : BadRequest(errorResponse);
        }

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    /// <summary>
    /// Extracts the raw bearer token from the request's Authorization header so it
    /// can be relayed to RankEngine. Returns an empty string when no Bearer token
    /// is present — callers treat that as a soft failure.
    /// </summary>
    private string ExtractBearerToken()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return string.Empty;

        var raw = values.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(prefix.Length).Trim()
            : string.Empty;
    }
}
