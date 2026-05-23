// SignupLoadTest — wave-based realistic load test of SignupAPI 3-phase signup.
//
// Per the brief: production rarely sees 10 signups under ONE sponsor in a minute,
// but commonly sees many parallel signups distributed across MANY DIFFERENT
// sponsors at the same wall-clock time. We model that by:
//   * Picking K distinct existing AMB-* sponsors from A's downline.
//   * Firing WAVES of N concurrent signups, distributed round-robin across the
//     K sponsors (so each sponsor gets ~N/K simultaneous signups).
//   * Waving up: 10 / 30 / 60 / 120 / 200 concurrent (default).
//   * Sleeping 10s between waves so the system can breathe (IP-cache, EF, etc.).
//   * Per-task distinct X-Real-IP to bypass IpRateLimiting (5/min on signups).
//
// Each task does:
//   POST /api/v1/signups/ambassador            (phase 1)
//   POST /api/v1/signups/{id}/select-products  (phase 2)
//   POST /api/v1/signups/{id}/complete         (phase 3)
//
// Payload is validation-safe per ValidationPatterns.cs (letters-only FirstName,
// hex32 VisitorId, uppercase DiscountCode).
//
// Per wave we record total / OK / fail / success%, throughput, p50/p95/p99
// latency, top-3 failure tally. We also do a MemberStatistics integrity check
// (read EnrollmentPoints before + after for two of the rotated sponsors) to
// verify Sprint-15 Bug A's atomic-MERGE fix held under concurrent ancestor
// writes.
//
// Usage:
//   dotnet run --project Scripts/SignupLoadTest -- [--waves 10,30,60,120,200] [--sponsors n]

using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SignupLoadTest;

internal sealed class SignupResult
{
    public bool Success;
    public long LatencyMs;
    public int  Phase;
    public int  HttpStatus;
    public string? ErrorCode;
    public string? ErrorBody;
    public string? MemberId;
    public string? SponsorSlug;
    public string? SponsorMemberId;
}

internal static class Program
{
    private const string SignupApiBase = "https://localhost:7005";
    private const string EliteProductId = "00000003-prod-0000-0000-000000000003";
    private const int    MembershipLevelEliteId = 3;
    private const int    EliteQualPoints = 6;
    private const string SqlConnString =
        "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;";

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var waves = new[] { 10, 30, 60, 120, 200 };
        var sponsorCount = 20;
        var interWaveDelaySec = 10;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--waves" && i + 1 < args.Length)
                waves = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            else if (args[i] == "--sponsors" && i + 1 < args.Length)
                sponsorCount = int.Parse(args[++i]);
            else if (args[i] == "--pause" && i + 1 < args.Length)
                interWaveDelaySec = int.Parse(args[++i]);
        }

        Console.WriteLine("Loading sponsor pool from DB (AMB-* in A's downline with slug + DT row)…");
        var allSponsors = LoadSponsorsFromDb();
        Console.WriteLine($"Found {allSponsors.Count} candidate sponsors. Rotating across the first {sponsorCount}.");
        if (allSponsors.Count == 0)
        {
            Console.Error.WriteLine("No sponsors. Aborting.");
            return 1;
        }

        var sponsors = allSponsors.Take(sponsorCount).ToList();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(120),
            BaseAddress = new Uri(SignupApiBase)
        };
        ServicePointManager.DefaultConnectionLimit = 500;

        var run = Guid.NewGuid().ToString("N").Substring(0, 8);
        var waveResults = new List<WaveSummary>();
        var allCreatedMembers = new ConcurrentBag<(string MemberId, string SponsorSlug, string SponsorMemberId)>();

        Console.WriteLine($"Run id: {run}");
        Console.WriteLine($"Wave sizes: [{string.Join(", ", waves)}]");
        Console.WriteLine($"Sponsors rotated across: {sponsors.Count}");
        Console.WriteLine($"Inter-wave pause: {interWaveDelaySec}s");
        Console.WriteLine();

        var waveIdx = 0;
        foreach (var n in waves)
        {
            waveIdx++;
            Console.WriteLine($"==== Wave {waveIdx}: {n} concurrent signups across {sponsors.Count} sponsors ====");

            // Pick TWO sponsors we'll spot-check for MemberStatistics integrity.
            var checkSponsors = new[]
            {
                sponsors[(waveIdx * 3) % sponsors.Count],
                sponsors[(waveIdx * 7 + 1) % sponsors.Count]
            }.Distinct().ToArray();

            var beforeStats = new Dictionary<string, MemberStat>();
            foreach (var s in checkSponsors)
            {
                var (memId, st) = ReadStatForSlug(s);
                if (memId is not null) beforeStats[s] = new MemberStat(memId, st.EnrollmentPoints, st.EnrollmentTeamSize);
            }

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task<SignupResult>>(n);
            for (var i = 0; i < n; i++)
            {
                var sponsorIdx = i % sponsors.Count;
                var sponsorSlug = sponsors[sponsorIdx];
                var ip = $"10.{waveIdx % 256}.{(i / 256) % 256}.{(i % 256)}";
                tasks.Add(RunOneSignup(http, run, waveIdx, i, sponsorSlug, ip));
            }

            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // Resolve SponsorMemberId for each successful result by reading the new member.
            var newMemberIds = results.Where(r => r.Success && r.MemberId is not null).Select(r => r.MemberId!).ToList();
            var sponsorByNew = ResolveSponsorMemberIds(newMemberIds);
            foreach (var r in results)
            {
                if (r.Success && r.MemberId is not null && r.SponsorSlug is not null)
                {
                    r.SponsorMemberId = sponsorByNew.GetValueOrDefault(r.MemberId);
                    allCreatedMembers.Add((r.MemberId, r.SponsorSlug, r.SponsorMemberId ?? ""));
                }
            }

            // Per-sponsor integrity check: expected EnrollmentPoints delta = (successful signups directly under that sponsor) * 6.
            var integrityChecks = new List<string>();
            foreach (var s in checkSponsors)
            {
                if (!beforeStats.TryGetValue(s, out var before)) continue;
                var (memId, afterRaw) = ReadStatForSlug(s);
                if (memId is null) continue;
                var observedDeltaEP = afterRaw.EnrollmentPoints - before.EnrollmentPoints;
                var observedDeltaSize = afterRaw.EnrollmentTeamSize - before.EnrollmentTeamSize;
                // Expected: every successful signup whose SponsorMemberId == this member's id
                // contributes +6 EP (direct) AND +6 to every ancestor. Since the sponsor IS
                // the direct, expected = nDirectsThisWave * 6.
                var nDirect = results.Count(r => r.Success && r.MemberId is not null
                    && sponsorByNew.GetValueOrDefault(r.MemberId!) == memId);
                // But also: ancestor sponsors of this sponsor get +6 per any signup under THEIR subtree.
                // Per-sponsor integrity only checks direct effect — if sponsor is itself a downline of
                // OTHER sponsors picked in this wave, ancestor effect adds. To keep the check clean we
                // include both: the EXPECTED LOWER BOUND is nDirect*6 (everything directly under),
                // and the EXPECTED UPPER BOUND adds (nGrand*6) where nGrand counts all signups whose
                // sponsor lives somewhere in this sponsor's subtree.
                var nGrand = CountGrandUnderSponsor(memId, newMemberIds.Where(id => sponsorByNew.GetValueOrDefault(id) != memId).ToList());
                var expectedEP = (nDirect + nGrand) * EliteQualPoints;
                var expectedSize = nDirect + nGrand;
                var verdictEP = observedDeltaEP == expectedEP ? "OK" : "MISMATCH";
                var verdictSize = observedDeltaSize == expectedSize ? "OK" : "MISMATCH";
                integrityChecks.Add(
                    $"  - {s} ({memId}): EnrollmentPoints +{observedDeltaEP} (expected +{expectedEP}, direct={nDirect}+grand={nGrand}) [{verdictEP}], TeamSize +{observedDeltaSize} (expected +{expectedSize}) [{verdictSize}]");
            }

            var summary = BuildWaveSummary(waveIdx, n, results, sw.Elapsed, integrityChecks);
            waveResults.Add(summary);
            PrintWaveSummary(summary);

            if (waveIdx < waves.Length)
            {
                Console.WriteLine($"--- pausing {interWaveDelaySec}s before next wave ---");
                await Task.Delay(interWaveDelaySec * 1000);
            }
        }

        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("Final aggregate table:");
        PrintTable(waveResults);

        // Write reports
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var scriptDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var rigDir = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", ".."));
        var resultsPath = Path.Combine(rigDir, $"results-{ts}.md");
        File.WriteAllText(resultsPath, BuildMarkdownReport(run, waveResults, sponsors.Count, allCreatedMembers));
        Console.WriteLine();
        Console.WriteLine($"Wrote: {resultsPath}");

        var idsPath = Path.Combine(rigDir, $"created-members-{ts}.txt");
        File.WriteAllLines(idsPath, allCreatedMembers.Select(x => $"{x.MemberId}\t{x.SponsorSlug}\t{x.SponsorMemberId}"));
        Console.WriteLine($"Wrote: {idsPath}");
        Console.WriteLine();
        Console.WriteLine($"Total successful signups: {allCreatedMembers.Count}");

        return 0;
    }

    // ---------------- DB helpers ----------------

    private static List<string> LoadSponsorsFromDb()
    {
        var list = new List<string>();
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.ReplicateSiteSlug
            FROM dbo.MemberProfiles m
            INNER JOIN dbo.DualTeamTree d ON d.MemberId = m.MemberId
            WHERE m.ReplicateSiteSlug IS NOT NULL
              AND LEN(m.ReplicateSiteSlug) > 0
              AND m.MemberType = 0
              AND (m.SponsorMemberId = 'AMB-700829' OR d.HierarchyPath LIKE '%/AMB-700829/%')
            ORDER BY NEWID()";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var slug = r.GetString(0);
            if (!string.IsNullOrWhiteSpace(slug)) list.Add(slug);
        }
        return list;
    }

    private record struct StatPair(int EnrollmentPoints, int EnrollmentTeamSize);
    private record MemberStat(string MemberId, int EnrollmentPoints, int EnrollmentTeamSize);

    private static (string? MemberId, StatPair Stats) ReadStatForSlug(string slug)
    {
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.MemberId,
                   CAST(ISNULL(s.EnrollmentPoints,0)   AS decimal(18,4)) AS ep,
                   CAST(ISNULL(s.EnrollmentTeamSize,0) AS decimal(18,4)) AS sz
            FROM dbo.MemberProfiles m
            LEFT JOIN dbo.MemberStatistics s ON s.MemberId = m.MemberId
            WHERE m.ReplicateSiteSlug = @slug";
        cmd.Parameters.AddWithValue("@slug", slug);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (null, default);
        return (r.GetString(0), new StatPair((int)Math.Round(r.GetDecimal(1)), (int)Math.Round(r.GetDecimal(2))));
    }

    private static Dictionary<string, string> ResolveSponsorMemberIds(List<string> memberIds)
    {
        var dict = new Dictionary<string, string>();
        if (memberIds.Count == 0) return dict;
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        // Use IN list — keep param count modest by joining with VALUES.
        var paramNames = new List<string>();
        for (var i = 0; i < memberIds.Count; i++)
        {
            var p = "@p" + i;
            paramNames.Add(p);
            cmd.Parameters.AddWithValue(p, memberIds[i]);
        }
        cmd.CommandText = $"SELECT MemberId, SponsorMemberId FROM dbo.MemberProfiles WHERE MemberId IN ({string.Join(",", paramNames)})";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var mid = r.GetString(0);
            var sid = r.IsDBNull(1) ? "" : r.GetString(1);
            dict[mid] = sid;
        }
        return dict;
    }

    /// <summary>
    /// Counts how many of <paramref name="memberIds"/> live in the genealogy subtree of <paramref name="sponsorMemberId"/>.
    /// </summary>
    private static int CountGrandUnderSponsor(string sponsorMemberId, List<string> memberIds)
    {
        if (memberIds.Count == 0) return 0;
        using var cn = new SqlConnection(SqlConnString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        var paramNames = new List<string>();
        for (var i = 0; i < memberIds.Count; i++)
        {
            var p = "@p" + i;
            paramNames.Add(p);
            cmd.Parameters.AddWithValue(p, memberIds[i]);
        }
        cmd.Parameters.AddWithValue("@sp", "%/" + sponsorMemberId + "/%");
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM dbo.GenealogyTree
            WHERE MemberId IN ({string.Join(",", paramNames)})
              AND HierarchyPath LIKE @sp";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------------- HTTP signup ----------------

    private static async Task<SignupResult> RunOneSignup(
        HttpClient http, string run, int wave, int idx, string sponsorSlug, string ip)
    {
        var sw = Stopwatch.StartNew();
        var result = new SignupResult { Phase = 1, SponsorSlug = sponsorSlug };

        // Letters-only first name (NamePattern ^[\p{L}][\p{L} '\-\.]{0,49}$ rejects digits)
        const string firstName = "Loader";
        const string lastName  = "Conqueror";
        var emailSlug = $"lt{run}{wave}{idx}{Guid.NewGuid().ToString("N").Substring(0, 6)}".ToLowerInvariant();
        var email     = $"lt.{emailSlug}@example.com";
        var siteSlug  = $"lt-{run}-w{wave}-i{idx}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        var visitorId = Guid.NewGuid().ToString("N"); // hex32, no hyphens — passes VisitorIdPattern

        try
        {
            // ---- Phase 1 ----
            var p1Body = new
            {
                SponsorReplicateSite = sponsorSlug,
                FirstName            = firstName,
                LastName             = lastName,
                DateOfBirth          = "1990-01-15T00:00:00Z",
                Email                = email,
                Password             = "P@ssw0rd!2026",
                ConfirmPassword      = "P@ssw0rd!2026",
                Phone                = "+15550000000",
                Country              = "CA",
                MembershipLevelId    = MembershipLevelEliteId,
                VisitorId            = visitorId,
                ShowBusinessName     = false,
                ReplicateSiteSlug    = siteSlug
            };

            using var p1Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/signups/ambassador");
            p1Req.Headers.Add("X-Real-IP", ip);
            p1Req.Content = JsonContent.Create(p1Body);
            using var p1Resp = await http.SendAsync(p1Req);
            result.HttpStatus = (int)p1Resp.StatusCode;
            var p1Json = await p1Resp.Content.ReadAsStringAsync();
            if (!p1Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p1Json); return Done(result, sw); }
            var p1 = ParseEnvelope(p1Json);
            if (!p1.Success) { result.ErrorCode = p1.ErrorCode; result.ErrorBody = Truncate(p1Json); return Done(result, sw); }
            result.MemberId = p1.MemberId;

            // ---- Phase 2 ----
            result.Phase = 2;
            using var p2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/signups/{p1.SignupId}/select-products");
            p2Req.Headers.Add("X-Real-IP", ip);
            p2Req.Content = JsonContent.Create(new { productIds = new[] { EliteProductId } });
            using var p2Resp = await http.SendAsync(p2Req);
            result.HttpStatus = (int)p2Resp.StatusCode;
            var p2Json = await p2Resp.Content.ReadAsStringAsync();
            if (!p2Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p2Json); return Done(result, sw); }
            var p2 = ParseEnvelope(p2Json);
            if (!p2.Success) { result.ErrorCode = p2.ErrorCode; result.ErrorBody = Truncate(p2Json); return Done(result, sw); }

            // ---- Phase 3 ----
            result.Phase = 3;
            using var p3Req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/signups/{p1.SignupId}/complete");
            p3Req.Headers.Add("X-Real-IP", ip);
            // DiscountCode pattern: ^[A-Z0-9\-]{4,32}$ — uppercase only.
            p3Req.Content = JsonContent.Create(new
            {
                PaymentMethod                  = 4,
                DiscountCode                   = "TI-LOADER",
                CheckoutScreenshotContentType  = "image/png"
            });
            using var p3Resp = await http.SendAsync(p3Req);
            result.HttpStatus = (int)p3Resp.StatusCode;
            var p3Json = await p3Resp.Content.ReadAsStringAsync();
            if (!p3Resp.IsSuccessStatusCode) { result.ErrorBody = Truncate(p3Json); return Done(result, sw); }
            var p3 = ParseEnvelope(p3Json);
            if (!p3.Success) { result.ErrorCode = p3.ErrorCode; result.ErrorBody = Truncate(p3Json); return Done(result, sw); }

            result.Success = true;
        }
        catch (TaskCanceledException)
        {
            result.ErrorCode = "TIMEOUT";
            result.ErrorBody = "request timed out";
        }
        catch (Exception ex)
        {
            result.ErrorCode = "EXCEPTION";
            result.ErrorBody = ex.GetType().Name + ": " + ex.Message;
        }

        return Done(result, sw);
    }

    private static SignupResult Done(SignupResult r, Stopwatch sw)
    {
        sw.Stop();
        r.LatencyMs = sw.ElapsedMilliseconds;
        return r;
    }

    private static string Truncate(string s) => s.Length > 280 ? s.Substring(0, 280) + "..." : s;

    private sealed record EnvelopeResult(bool Success, string? ErrorCode, string? MemberId, string? SignupId);

    private static EnvelopeResult ParseEnvelope(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
            string? errorCode = null;
            if (root.TryGetProperty("errorCode", out var ec) && ec.ValueKind == JsonValueKind.String)
                errorCode = ec.GetString();
            string? memberId = null, signupId = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("memberId", out var mi)) memberId = mi.GetString();
                if (data.TryGetProperty("signupId", out var si)) signupId = si.GetString();
            }
            return new EnvelopeResult(success, errorCode, memberId, signupId);
        }
        catch
        {
            return new EnvelopeResult(false, "PARSE_ERROR", null, null);
        }
    }

    // ---------------- Aggregation ----------------

    internal sealed class WaveSummary
    {
        public int    WaveIdx;
        public int    Concurrent;
        public int    Total, Successes, Failures;
        public double SuccessRate;
        public double WallClockSec;
        public double Throughput;
        public long   P50, P95, P99, Max;
        public string FailureTally = "";
        public List<string> IntegrityChecks = new();
    }

    private static WaveSummary BuildWaveSummary(int waveIdx, int conc, SignupResult[] results, TimeSpan wallClock, List<string> integrity)
    {
        var latencies = results.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();
        var ok = results.Count(r => r.Success);
        var ko = results.Length - ok;
        var failTally = results.Where(r => !r.Success)
            .GroupBy(r => $"phase{r.Phase}/http{r.HttpStatus}/{r.ErrorCode ?? "-"}")
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}×{g.Count()}")
            .ToList();

        return new WaveSummary
        {
            WaveIdx     = waveIdx,
            Concurrent  = conc,
            Total       = results.Length,
            Successes   = ok,
            Failures    = ko,
            SuccessRate = results.Length == 0 ? 0 : ok * 100.0 / results.Length,
            WallClockSec= wallClock.TotalSeconds,
            Throughput  = wallClock.TotalSeconds > 0 ? results.Length / wallClock.TotalSeconds : 0,
            P50         = Percentile(latencies, 0.50),
            P95         = Percentile(latencies, 0.95),
            P99         = Percentile(latencies, 0.99),
            Max         = latencies.Length > 0 ? latencies.Max() : 0,
            FailureTally= failTally.Count == 0 ? "—" : string.Join(", ", failTally),
            IntegrityChecks = integrity
        };
    }

    private static long Percentile(long[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Min(sorted.Length - 1, Math.Round(p * (sorted.Length - 1)));
        return sorted[idx];
    }

    private static void PrintWaveSummary(WaveSummary s)
    {
        Console.WriteLine($"   total={s.Total}  ok={s.Successes}  fail={s.Failures}  success-rate={s.SuccessRate:F1}%");
        Console.WriteLine($"   wall-clock={s.WallClockSec:F2}s  throughput={s.Throughput:F2}/sec");
        Console.WriteLine($"   latency-ms  p50={s.P50}  p95={s.P95}  p99={s.P99}  max={s.Max}");
        Console.WriteLine($"   top failures: {s.FailureTally}");
        if (s.IntegrityChecks.Count > 0)
        {
            Console.WriteLine($"   MemberStatistics integrity (Bug A check):");
            foreach (var c in s.IntegrityChecks) Console.WriteLine(c);
        }
    }

    private static void PrintTable(List<WaveSummary> rows)
    {
        Console.WriteLine($"| Wave | Conc | OK | Fail | Success% | Wall(s) | Tput/s | p50  | p95  | p99  | Max  | Top failures |");
        Console.WriteLine($"|------|------|----|------|----------|---------|--------|------|------|------|------|--------------|");
        foreach (var s in rows)
            Console.WriteLine($"| {s.WaveIdx,4} | {s.Concurrent,4} | {s.Successes,2} | {s.Failures,4} | {s.SuccessRate,7:F1}% | {s.WallClockSec,7:F2} | {s.Throughput,6:F2} | {s.P50,4} | {s.P95,4} | {s.P99,4} | {s.Max,4} | {s.FailureTally} |");
    }

    private static string BuildMarkdownReport(
        string run, List<WaveSummary> rows, int sponsorCount,
        ConcurrentBag<(string MemberId, string SponsorSlug, string SponsorMemberId)> created)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# SignupLoadTest — wave-based realistic results (run {run})");
        sb.AppendLine();
        sb.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Endpoint: `{SignupApiBase}/api/v1/signups/{{ambassador,select-products,complete}}` (3 HTTP calls per task)");
        sb.AppendLine($"Rate-limit bypass: distinct `X-Real-IP` per task (AspNetCoreRateLimit default `RealIpHeader`).");
        sb.AppendLine($"Sponsors rotated across: **{sponsorCount}** distinct slugs (AMB-* downline of AMB-700829 with DT row).");
        sb.AppendLine();
        sb.AppendLine("## Wave-by-wave results");
        sb.AppendLine();
        sb.AppendLine($"| Wave | Conc | OK | Fail | Success% | Wall(s) | Tput/s | p50(ms) | p95(ms) | p99(ms) | Max(ms) | Top failures |");
        sb.AppendLine($"|------|------|----|------|----------|---------|--------|---------|---------|---------|---------|--------------|");
        foreach (var s in rows)
            sb.AppendLine($"| {s.WaveIdx} | {s.Concurrent} | {s.Successes} | {s.Failures} | {s.SuccessRate:F1}% | {s.WallClockSec:F2} | {s.Throughput:F2} | {s.P50} | {s.P95} | {s.P99} | {s.Max} | {s.FailureTally} |");
        sb.AppendLine();
        sb.AppendLine("## MemberStatistics integrity (Bug A: atomic-MERGE fix)");
        sb.AppendLine();
        foreach (var s in rows)
        {
            if (s.IntegrityChecks.Count == 0) continue;
            sb.AppendLine($"### Wave {s.WaveIdx}");
            foreach (var c in s.IntegrityChecks) sb.AppendLine(c);
            sb.AppendLine();
        }
        sb.AppendLine($"## Total new ambassadors created: **{created.Count}**");
        sb.AppendLine();
        var byPar = created.GroupBy(x => x.SponsorSlug).OrderByDescending(g => g.Count()).ToList();
        sb.AppendLine($"- Distinct sponsors used: **{byPar.Count}**");
        foreach (var g in byPar.Take(40))
            sb.AppendLine($"  - `{g.Key}`: {g.Count()} signups");
        if (byPar.Count > 40) sb.AppendLine($"  - … {byPar.Count - 40} more sponsors");
        return sb.ToString();
    }
}
