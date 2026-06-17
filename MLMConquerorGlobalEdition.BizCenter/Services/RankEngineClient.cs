using System.Net.Http.Headers;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>
/// HTTP implementation of <see cref="IRankEngineClient"/>. Posts to the RankEngine
/// member self-service endpoint (<c>POST /api/v1/ranks/certificate/member-generate/{id}</c>)
/// with the caller's bearer token relayed. RankEngine enforces ownership.
/// Named HttpClient: "rankengine" (configured in Program.cs).
/// </summary>
public sealed class RankEngineClient : IRankEngineClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RankEngineClient> _logger;

    public RankEngineClient(IHttpClientFactory httpClientFactory, ILogger<RankEngineClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateMemberCertificateAsync(
        string memberRankHistoryId, string bearerToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberRankHistoryId))
            return Result<string>.Failure("RANK_HISTORY_ID_REQUIRED", "memberRankHistoryId is required.");

        if (string.IsNullOrWhiteSpace(bearerToken))
            return Result<string>.Failure("AUTH_TOKEN_MISSING", "Caller bearer token is required.");

        var client = _httpClientFactory.CreateClient("rankengine");
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/ranks/certificate/member-generate/{Uri.EscapeDataString(memberRankHistoryId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        try
        {
            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // RankEngine returns ApiResponse<object> on failure with errorCode + message;
                // we parse defensively because the body could also be empty on a 5xx.
                var failure = await TryReadFailureAsync(response, ct);
                _logger.LogWarning(
                    "RankEngine certificate generation failed: {Status} {Code} {Error}",
                    response.StatusCode, failure.Code, failure.Error);
                return Result<string>.Failure(failure.Code, failure.Error);
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<CertificateGenerationResponseDto>>(
                cancellationToken: ct);

            if (body?.Data?.CertificateUrl is null)
                return Result<string>.Failure(
                    "CERTIFICATE_RESPONSE_INVALID",
                    "RankEngine returned a success response without a certificate URL.");

            return Result<string>.Success(body.Data.CertificateUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RankEngine certificate generation request failed.");
            return Result<string>.Failure(
                "RANK_ENGINE_UNAVAILABLE",
                "Unable to reach RankEngine to generate the certificate. Please try again later.");
        }
    }

    private static async Task<(string Code, string Error)> TryReadFailureAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct);
            return (
                body?.ErrorCode ?? "CERTIFICATE_GENERATION_FAILED",
                body?.Message   ?? $"RankEngine returned {(int)response.StatusCode}.");
        }
        catch
        {
            return ("CERTIFICATE_GENERATION_FAILED",
                    $"RankEngine returned {(int)response.StatusCode}.");
        }
    }

    /// <summary>
    /// Local DTO mirroring RankEngine's CertificateGenerationResponse. We deliberately
    /// keep it private to this client so BizCenter doesn't grow a project reference
    /// to RankEngine for one DTO.
    /// </summary>
    private sealed class CertificateGenerationResponseDto
    {
        public string? MemberRankHistoryId { get; set; }
        public string? MemberId { get; set; }
        public string? RankName { get; set; }
        public string? CertificateUrl { get; set; }
        public DateTime? GeneratedAt { get; set; }
    }
}
