// One-off sample generator for the rank-certificate PDF filler.
// Renders a Silver certificate using the real ITextCertificatePdfFillerService so you
// can eyeball the canvas-overlay output for any recipient name (including long names
// that exercise the adaptive font-size path).
//
// Usage:
//   dotnet run --project scripts/GenerateSampleCertificate -- "Full Name Here" [rankSortOrder]
//
// Default rank is 1 (Silver). The PDF lands in RankEngine/wwwroot/certificates/.

using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.RankEngine.Services;

var fullName = args.Length > 0
    ? args[0]
    : "Dr. Maximiliano Alejandro Wellington-Worthington de Habsburgo III";

var rankSortOrder = args.Length > 1 && int.TryParse(args[1], out var r) ? r : 1;

// scripts/GenerateSampleCertificate/bin/Debug/net10.0/  →  5 levels up = repo root
var repoRoot = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var templatesFolder = Path.Combine(repoRoot,
    "MLMConquerorGlobalEdition.RankEngine", "CertificateTemplates");
var outputFolder = Path.Combine(repoRoot,
    "MLMConquerorGlobalEdition.RankEngine", "wwwroot", "certificates");

Directory.CreateDirectory(outputFolder);

var filler = new ITextCertificatePdfFillerService(
    templatesFolder,
    NullLogger<ITextCertificatePdfFillerService>.Instance);

var data = new CertificateTemplateData(
    FullName:   fullName,
    MemberId:   "AMB-SAMPLE",
    RankName:   $"Rank-{rankSortOrder}",
    AchievedAt: new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc));

Console.WriteLine($"Name length: {fullName.Length} chars  |  rank sort-order: {rankSortOrder}");

var bytes = await filler.FillAsync(rankSortOrder, data, CancellationToken.None);

var outFile = Path.Combine(outputFolder,
    $"sample_longname_{fullName.Length}chars_rank{rankSortOrder}.pdf");
await File.WriteAllBytesAsync(outFile, bytes);

Console.WriteLine($"Wrote: {outFile}  ({bytes.Length:N0} bytes)");
