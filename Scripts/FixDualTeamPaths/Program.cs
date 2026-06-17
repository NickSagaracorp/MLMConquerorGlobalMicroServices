// Sprint-15 follow-up data-hygiene tool — Bug 10.
//
// Detects (and optionally repairs) malformed DualTeamTree.HierarchyPath rows
// where two member IDs got concatenated without a separating slash, e.g.:
//
//   /AMB-700829/AMB-375401AMB-610895/   ← Bug 10 (missing internal '/')
//
// The root cause was three placement code paths that did
// `$"{parentPath}{memberId}/"` assuming parentPath always ended in '/'. When
// the parent row's HierarchyPath had historically been written without the
// trailing slash, the child glued IDs together — and every subsequent
// StartsWith('/AMB-X/') subtree query missed those rows.
//
// The code paths are now fixed (TrimEnd('/') + '/'). This tool exists ONLY to
// repair the legacy rows already in the database.
//
// USAGE:
//   dotnet run --project scripts/FixDualTeamPaths
//       → DETECT-ONLY mode. Reports the affected rows; modifies nothing.
//
//   COMMIT_FOR_REAL=1 dotnet run --project scripts/FixDualTeamPaths
//       → APPLY mode. Wraps the UPDATE in a transaction; rewrites only the
//         rows it can repair deterministically. Other rows are reported and
//         skipped.
//
//   ConnectionStrings__DefaultConnection="..." environment override is
//   honored. Defaults to the local dev connection string from
//   MLMConquerorGlobalEdition.AdminAPI/appsettings.json.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

const string DefaultConn =
    "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;" +
    "TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True;";

var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? DefaultConn;

var commit = Environment.GetEnvironmentVariable("COMMIT_FOR_REAL") == "1";

Console.WriteLine("===========================================================");
Console.WriteLine(" FixDualTeamPaths — Sprint-15 Bug 10 data-hygiene tool");
Console.WriteLine("===========================================================");
Console.WriteLine($"Mode               : {(commit ? "APPLY (will UPDATE)" : "DETECT-ONLY")}");
Console.WriteLine($"Server / Database  : {SafeServerLabel(conn)}");
Console.WriteLine();

// Pattern that detects an ID concatenation without a separating slash.
// Examples:
//   AMB-123AMB-456            → bad
//   MBR-001AMB-002            → bad
//   AMB-123/AMB-456           → good
//
// T-SQL doesn't do real regex, so we let SQL Server pre-filter with LIKE
// (cheap, even on millions of rows) and then validate / rewrite in C#.
//
// LIKE pattern matches "<digit><any ID prefix>-"; brackets escape the dash.
const string DetectSql = @"
SELECT Id, MemberId, ParentMemberId, HierarchyPath
FROM   DualTeamTree
WHERE  HierarchyPath LIKE '%[0-9]AMB-%'
   OR  HierarchyPath LIKE '%[0-9]MBR-%';
";

// Validates / normalizes a single path. Returns null when the path is already
// well-formed (no concatenation bug) or when we can't deterministically
// rewrite it.
static string? TryRewrite(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;

    // Split the path into segments and re-emit each one with explicit '/'
    // separators. Each segment must look like exactly one ID — if it has
    // an embedded "AMB-" / "MBR-" / "ROOT" mid-string, split there.
    var segments = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var rewrittenSegments = new List<string>(segments.Length);
    var anyChange = false;

    // Look-ahead split: keep the delimiter at the start of the next chunk.
    // Anchored to the standard ID prefixes used in this codebase.
    var splitter = new Regex(@"(?=AMB-|MBR-|ROOT)", RegexOptions.Compiled);

    foreach (var seg in segments)
    {
        var parts = splitter.Split(seg).Where(p => p.Length > 0).ToArray();

        if (parts.Length == 0)
        {
            // Empty/unparseable — preserve as-is rather than guess.
            rewrittenSegments.Add(seg);
            continue;
        }

        if (parts.Length > 1) anyChange = true;
        foreach (var p in parts) rewrittenSegments.Add(p);
    }

    if (!anyChange) return null;

    var sb = new StringBuilder("/");
    sb.Append(string.Join('/', rewrittenSegments));
    sb.Append('/');
    return sb.ToString();
}

await using var c = new SqlConnection(conn);
await c.OpenAsync();

// 1. Detect.
var affected = new List<(string Id, string MemberId, string Old, string? New, string Note)>();

await using (var cmd = new SqlCommand(DetectSql, c))
await using (var rdr = await cmd.ExecuteReaderAsync())
{
    while (await rdr.ReadAsync())
    {
        var id        = rdr.GetString(0);
        var memberId  = rdr.GetString(1);
        var oldPath   = rdr.GetString(3);
        var rewritten = TryRewrite(oldPath);

        if (rewritten is null)
        {
            // SQL LIKE was a false positive (e.g. legitimate ID containing
            // digits before "AMB-" in a way the regex didn't deem split-able).
            // Skip silently.
            continue;
        }

        if (rewritten == oldPath)
            continue;

        var note = rewritten.Length > 2000
            ? "WOULD-EXCEED-COLUMN-LIMIT(2000) — skipped"
            : "ok";

        affected.Add((id, memberId, oldPath, rewritten, note));
    }
}

Console.WriteLine($"Rows scanned & flagged by LIKE : (see SQL Server)");
Console.WriteLine($"Rows the rewriter can fix      : {affected.Count(a => a.Note == "ok")}");
Console.WriteLine($"Rows too long to fit column    : {affected.Count(a => a.Note.StartsWith("WOULD-EXCEED"))}");
Console.WriteLine();

if (affected.Count == 0)
{
    Console.WriteLine("Nothing to do — no malformed HierarchyPath rows detected.");
    return 0;
}

// 2. Preview up to 10 examples.
Console.WriteLine("Examples (up to 10):");
foreach (var row in affected.Take(10))
{
    Console.WriteLine($"  Id={row.Id} MemberId={row.MemberId}");
    Console.WriteLine($"    OLD: {row.Old}");
    Console.WriteLine($"    NEW: {row.New}");
    if (row.Note != "ok") Console.WriteLine($"    NOTE: {row.Note}");
}
Console.WriteLine();

if (!commit)
{
    Console.WriteLine($"{affected.Count(a => a.Note == "ok")} rows would be fixed. " +
                       "Re-run with COMMIT_FOR_REAL=1 to apply (wrapped in a transaction).");
    return 0;
}

// 3. Apply (transactional).
Console.WriteLine("APPLY mode — opening transaction...");
await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
try
{
    var updateSql = "UPDATE DualTeamTree SET HierarchyPath = @new, LastUpdateBy = 'fix-dualteam-paths-tool', LastUpdateDate = SYSUTCDATETIME() WHERE Id = @id;";
    var updated   = 0;

    foreach (var row in affected.Where(a => a.Note == "ok"))
    {
        await using var cmd = new SqlCommand(updateSql, c, tx);
        cmd.Parameters.AddWithValue("@new", row.New!);
        cmd.Parameters.AddWithValue("@id",  row.Id);
        updated += await cmd.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"Updated {updated} rows inside the transaction.");
    Console.Write("Type COMMIT to commit, anything else to ROLLBACK: ");
    var answer = Console.ReadLine();
    if (string.Equals(answer?.Trim(), "COMMIT", StringComparison.Ordinal))
    {
        await tx.CommitAsync();
        Console.WriteLine("COMMITTED.");
    }
    else
    {
        await tx.RollbackAsync();
        Console.WriteLine("ROLLED BACK — no changes saved.");
    }
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine($"Failed mid-transaction, rolled back. {ex.Message}");
    return 2;
}

return 0;

static string SafeServerLabel(string connStr)
{
    // Pull just Server=... and Database=... so passwords (if any) never log.
    var b = new SqlConnectionStringBuilder(connStr);
    return $"{b.DataSource} / {b.InitialCatalog}";
}
