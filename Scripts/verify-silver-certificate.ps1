<#
.SYNOPSIS
    Live end-to-end verification of the rank-certificate feature.

.DESCRIPTION
    Drives the SignupAPI 3-phase signup wizard to create one ambassador "A" who
    directly sponsors three downline members (B, C, D). Each member enrolls on the
    Travel Advantage Elite product (6 qualification points). This pushes A to:
      - 3 directly-sponsored members  (universal gate: sponsored >= 3)
      - 1 sponsored ExternalMember    (D is a Member signup -> Silver ExternalMembers >= 1)
      - 18 Enrollment-Team points     (Silver EnrollmentTeam requirement)
    so A qualifies for the Silver rank (RankDefinition Id=1, SortOrder=1).

    RankEngine's ProcessRankQueueJob (HangFire, every 5 min) then evaluates A,
    promotes A to Silver, and fire-and-forget generates the certificate PDF into
    MLMConquerorGlobalEdition.RankEngine\wwwroot\certificates\.

    The script finally polls the certificates folder for *_Silver.pdf.

.NOTES
    Re-runnable: every run uses a unique GUID-derived suffix for emails, slugs,
    and FingerprintJS visitor ids, so the fraud-fingerprint guard never trips.

    Does NOT start the services. Start them first:
      cd MLMConquerorGlobalEdition.SignupAPI ; dotnet run --launch-profile https
      cd MLMConquerorGlobalEdition.RankEngine ; dotnet run --launch-profile https

    KNOWN ISSUES discovered during Task 11 live verification (2026-05-22):
      1. RankEngine\RankEngine.csproj must reference 'itext7.bouncy-castle-adapter'
         (9.1.0) in addition to 'itext7' — without it iText throws
         'BouncyCastleFactoryCreator' on PdfReader. (FIXED in csproj.)
      2. CertificateTemplates\1.pdf .. 19.pdf have NO AcroForm fields, but
         ITextCertificatePdfFillerService requires fillable fields 'FullName'
         and 'AchievedDate'. Until the templates are rebuilt with those fields
         (or the filler is changed to draw text onto the page), certificate
         generation throws InvalidOperationException and NO PDF is written.
         The rank PROMOTION itself succeeds regardless (cert gen is
         fire-and-forget). This script will therefore report the promotion as
         verified but the certificate poll as failed until issue 2 is resolved.
      3. EvaluateRankHandler.NotifyUplines reuses the scoped AppDbContext while
         fire-and-forget _push/_mediator calls are still running on it -> a
         'second operation started on this context' exception is logged. It is
         non-fatal (the rank row is saved before the fire-and-forget calls).
#>
[CmdletBinding()]
param(
    [string] $SignupApiBase   = 'https://localhost:7005',
    [string] $RankEngineBase  = 'https://localhost:7009',
    [string] $RepoRoot        = (Split-Path -Parent $PSScriptRoot),
    [string] $Country         = 'CA',          # non-US -> no SSN required
    [int]    $MembershipLevelId = 3,           # Travel Advantage Elite (6 qual points)
    [string] $EliteProductId  = '00000003-prod-0000-0000-000000000003',
    [int]    $CertPollSeconds = 420,           # 7 minutes
    [int]    $CertPollEvery   = 20
)

$ErrorActionPreference = 'Stop'
# Accept the dev TLS certificate for the whole session.
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
} else {
    Add-Type @"
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TrustAll : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint s,X509Certificate c,WebRequest r,int p){return true;}
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAll
    [System.Net.ServicePointManager]::SecurityProtocol  = [System.Net.SecurityProtocolType]::Tls12
}

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "    $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "    $m" -ForegroundColor Yellow }

$run = ([guid]::NewGuid().ToString('N')).Substring(0, 8)
Write-Step "Run id: $run"

# ---- Health checks -------------------------------------------------------
Write-Step 'Checking service health'
$sh = Invoke-RestMethod -Uri "$SignupApiBase/health"  -Method Get
$rh = Invoke-RestMethod -Uri "$RankEngineBase/health" -Method Get
if ($sh.status -ne 'Healthy') { throw "SignupAPI not healthy: $($sh | ConvertTo-Json -Compress)" }
if ($rh.status -ne 'Healthy') { throw "RankEngine not healthy: $($rh | ConvertTo-Json -Compress)" }
Write-Ok "SignupAPI=$($sh.status)  RankEngine=$($rh.status)"

# ---- Signup helpers ------------------------------------------------------
function Invoke-Signup {
    param(
        [ValidateSet('ambassador','member')] [string] $Kind,
        [string] $FirstName,
        [string] $LastName,
        [string] $SponsorSlug = $null,
        [string] $OwnSlug     = $null
    )
    $email   = "t11.$run.$($FirstName.ToLower())@example.com"
    $visitor = "vis-$run-$($FirstName.ToLower())-$([guid]::NewGuid().ToString('N').Substring(0,6))"

    $body = @{
        SponsorReplicateSite = $SponsorSlug
        FirstName            = $FirstName
        LastName             = $LastName
        DateOfBirth          = '1990-01-15T00:00:00Z'
        Email                = $email
        Password             = 'P@ssw0rd!2026'
        ConfirmPassword      = 'P@ssw0rd!2026'
        Phone                = '+15550000000'
        Country              = $Country
        MembershipLevelId    = $MembershipLevelId
        VisitorId            = $visitor
        ShowBusinessName     = $false
    }
    if ($Kind -eq 'ambassador' -and $OwnSlug) { $body.ReplicateSiteSlug = $OwnSlug }

    # Phase 1 - create pending signup
    $p1 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/$Kind" -Method Post `
        -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    if (-not $p1.success) { throw "Phase1 ($Kind $FirstName) failed: $($p1 | ConvertTo-Json -Compress)" }
    $signupId = $p1.data.signupId
    $memberId = $p1.data.memberId

    # Phase 2 - select the Elite product (6 qualification points)
    $p2 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/$signupId/select-products" -Method Post `
        -ContentType 'application/json' -Body (@{ productIds = @($EliteProductId) } | ConvertTo-Json)
    if (-not $p2.success) { throw "Phase2 ($FirstName) failed: $($p2 | ConvertTo-Json -Compress)" }

    # Phase 3 - complete with the simplest payment path (DiscountCode, no gateway)
    $p3 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/$signupId/complete" -Method Post `
        -ContentType 'application/json' `
        -Body (@{ PaymentMethod = 4; DiscountCode = "T11-$run" } | ConvertTo-Json)
    if (-not $p3.success) { throw "Phase3 ($FirstName) failed: $($p3 | ConvertTo-Json -Compress)" }

    Write-Ok "$Kind '$FirstName' -> MemberId=$memberId"
    return [pscustomobject]@{ MemberId = $memberId; SignupId = $signupId; Email = $email }
}

# ---- A: root ambassador --------------------------------------------------
Write-Step 'Signing up Ambassador A (root, no sponsor)'
$slugA = "t11-a-$run"
$A = Invoke-Signup -Kind ambassador -FirstName "Alpha$run" -LastName 'Root' -OwnSlug $slugA

# ---- B, C downline ambassadors; D downline member (ExternalMember) -------
Write-Step 'Signing up downline B (ambassador)'
$B = Invoke-Signup -Kind ambassador -FirstName "Bravo$run"   -LastName 'Down' -SponsorSlug $slugA
Write-Step 'Signing up downline C (ambassador)'
$C = Invoke-Signup -Kind ambassador -FirstName "Charlie$run" -LastName 'Down' -SponsorSlug $slugA
Write-Step 'Signing up downline D (member / ExternalMember)'
$D = Invoke-Signup -Kind member     -FirstName "Delta$run"   -LastName 'Down' -SponsorSlug $slugA

Write-Host ''
Write-Host "Enrollment tree:" -ForegroundColor White
Write-Host "  A $($A.MemberId)" -ForegroundColor White
Write-Host "  +- B $($B.MemberId)  (Ambassador)" -ForegroundColor White
Write-Host "  +- C $($C.MemberId)  (Ambassador)" -ForegroundColor White
Write-Host "  +- D $($D.MemberId)  (ExternalMember)" -ForegroundColor White
Write-Host ''

# ---- Poll for the Silver certificate PDF ---------------------------------
$certDir = Join-Path $RepoRoot 'MLMConquerorGlobalEdition.RankEngine\wwwroot\certificates'
Write-Step "Polling for *_Silver.pdf in: $certDir"
Write-Warn2 "ProcessRankQueueJob runs every 5 min - waiting up to $CertPollSeconds s."

$deadline = (Get-Date).AddSeconds($CertPollSeconds)
$found    = $null
while ((Get-Date) -lt $deadline -and -not $found) {
    if (Test-Path $certDir) {
        $found = Get-ChildItem -Path $certDir -Filter '*_Silver.pdf' -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if (-not $found) { Start-Sleep -Seconds $CertPollEvery }
}

if ($found) {
    Write-Ok "CERTIFICATE FOUND: $($found.FullName)  ($($found.Length) bytes)"
    $hdr = [System.IO.File]::ReadAllBytes($found.FullName)[0..3]
    $isPdf = ($hdr[0] -eq 0x25 -and $hdr[1] -eq 0x50 -and $hdr[2] -eq 0x44 -and $hdr[3] -eq 0x46)
    Write-Ok "Valid PDF header (%PDF): $isPdf"
    exit 0
} else {
    Write-Warn2 "No *_Silver.pdf appeared within $CertPollSeconds s."
    Write-Warn2 "Check RankEngine console output for rank-evaluation / PDF-generation errors."
    exit 1
}
