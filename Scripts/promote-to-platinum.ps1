<#
.SYNOPSIS
    Promote an existing Gold ambassador to Platinum by adding fresh directly-sponsored
    ambassadors through the live SignupAPI, then placing them in the binary tree
    using a free-slot walker that descends each chain to find the first empty
    Left/Right position.

.DESCRIPTION
    Targets an existing root ambassador A (default AMB-700829, slug t11-a-de5cd5ef)
    who is already Gold with 13 direct downline (~84 ET points, 13 QS members).
    Signs up `-NewAmbassadors` NEW ambassadors directly under A so A's eligible
    enrollment-team points cross Platinum's 175-point requirement.

    Per Platinum RankRequirement: EnrollmentTeam = 175, MaxEnrollmentTeamPointsPerBranch = 0.66
    (cap per direct child = 115.5 points). Each new direct adds 6 points → 17 new directs
    × 6 = 102 → 78 (current eligible) + 102 = 180 ≥ 175 ✓.

    After each successful signup, logs in as A and POSTs a binary-tree placement
    under the FIRST FREE slot on the alternating L/R chain starting from A. A's
    direct L and R slots are already full from the Gold run; the script walks
    down the chain (left-of-left, right-of-right) and falls back to placing on
    the opposite side of a deep chain node when needed.

    Once placements are queued, RankEvaluationQueue rows exist for upline A.
    ProcessRankQueueJob (HangFire) on RankEngine runs every 5 min; when A is
    promoted to Platinum the certificate auto-generates synchronously into
    MLMConquerorGlobalEdition.RankEngine\wwwroot\certificates\.

    Script polls that folder for *_AMB-700829_Platinum.pdf for up to 7 minutes.

.NOTES
    Rate limit: /api/v1/signups/ambassador is capped at 10 req/min/IP. Script
    sleeps 7s between signups (≈ 8/min) to stay under the limit.

    Run order assumes services are already running:
      SignupAPI    https://localhost:7005
      RankEngine   https://localhost:7009
#>
[CmdletBinding()]
param(
    [string] $SignupApiBase   = 'https://localhost:7005',
    [string] $RankEngineBase  = 'https://localhost:7009',
    [string] $RepoRoot        = $(if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) } else { (Get-Location).Path }),
    [string] $RootMemberId    = 'AMB-700829',
    [string] $RootSlug        = 't11-a-de5cd5ef',
    [string] $RootEmail       = 't11.de5cd5ef.alphade5cd5ef@example.com',
    [string] $RootPassword    = 'P@ssw0rd!2026',
    [string] $Country         = 'CA',
    [int]    $MembershipLevelId = 3,
    [string] $EliteProductId  = '00000003-prod-0000-0000-000000000003',
    [int]    $NewAmbassadors  = 17,
    [int]    $SignupSleepSec  = 7,         # respect 10/min rate limit (≈ 8/min actual)
    [int]    $CertPollSeconds = 420,
    [int]    $CertPollEvery   = 30,
    [string] $TargetRankSuffix = 'Platinum',
    [string] $SqlConnString   = 'Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;'
)

$ErrorActionPreference = 'Stop'

# Accept the dev TLS certificate for the whole session (PS 5.1 + PS 7).
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
} else {
    if (-not ('TrustAll' -as [type])) {
        Add-Type @"
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TrustAll : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint s,X509Certificate c,WebRequest r,int p){return true;}
}
"@
    }
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAll
    [System.Net.ServicePointManager]::SecurityProtocol  = [System.Net.SecurityProtocolType]::Tls12
}

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "    $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "    $m" -ForegroundColor Yellow }
function Write-Err2($m) { Write-Host "    $m" -ForegroundColor Red }

$run = ([guid]::NewGuid().ToString('N')).Substring(0, 8)
Write-Step "Run id: $run"
Write-Step "Target root ambassador: $RootMemberId (slug=$RootSlug)"
Write-Step "Will sign up $NewAmbassadors NEW ambassadors directly under $RootMemberId"

# ---- Health checks -------------------------------------------------------
Write-Step 'Checking service health'
try {
    $sh = Invoke-RestMethod -Uri "$SignupApiBase/health"  -Method Get -TimeoutSec 5
    $rh = Invoke-RestMethod -Uri "$RankEngineBase/health" -Method Get -TimeoutSec 5
} catch {
    throw "Service health check failed. Start SignupAPI and RankEngine first. Error: $($_.Exception.Message)"
}
if ($sh.status -ne 'Healthy') { throw "SignupAPI not healthy: $($sh | ConvertTo-Json -Compress)" }
if ($rh.status -ne 'Healthy') { throw "RankEngine not healthy: $($rh | ConvertTo-Json -Compress)" }
Write-Ok "SignupAPI=$($sh.status)  RankEngine=$($rh.status)"

# ---- Log in as A to obtain JWT for placement -----------------------------
Write-Step "Logging in as $RootEmail to obtain JWT"
$loginBody = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
$jwt = $null
try {
    $lr = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post `
            -ContentType 'application/json' -Body $loginBody -TimeoutSec 10
    if (-not $lr.success) { throw "login failed: $($lr | ConvertTo-Json -Compress)" }
    $jwt = $lr.data.accessToken
    if (-not $jwt) { $jwt = $lr.data.AccessToken }
    if (-not $jwt) { throw "no accessToken in response: $($lr.data | ConvertTo-Json -Compress)" }
    Write-Ok "Got JWT (len=$($jwt.Length))"
} catch {
    Write-Warn2 "Login failed: $($_.Exception.Message)"
    Write-Warn2 "Will continue with signups but skip placements."
}

function Refresh-Jwt {
    try {
        $body = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
        $r = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post `
                -ContentType 'application/json' -Body $body -TimeoutSec 10
        if ($r.success) {
            $t = $r.data.accessToken; if (-not $t) { $t = $r.data.AccessToken }
            return $t
        }
    } catch { Write-Warn2 "Refresh-Jwt failed: $($_.Exception.Message)" }
    return $null
}

# ---- DB helpers ---------------------------------------------------------
function Get-FreeSlot {
    <#
    .SYNOPSIS
        Walk A's binary subtree to find the first free L or R slot on the
        requested PreferredSide. Falls back to the opposite side if necessary.
    .DESCRIPTION
        Algorithm: starting from RootMemberId, descend the chain matching
        PreferredSide. At each node, check if it has a child on PreferredSide.
        If not, that's the free slot. Otherwise descend and repeat.
        If chain bottoms out without a free slot (shouldn't happen — every
        leaf has 2 empty children), try opposite side of the deepest visited
        node as a fallback.
    .OUTPUTS
        @{ ParentMemberId = '...'; Side = 'Left'|'Right' }
    #>
    param(
        [string] $RootMemberId,
        [string] $PreferredSide,    # 'Left' or 'Right'
        [string] $ConnString
    )
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString
    $cn.Open()
    try {
        $preferredSideInt = if ($PreferredSide -eq 'Left') { 0 } else { 1 }
        $oppositeSide     = if ($PreferredSide -eq 'Left') { 'Right' } else { 'Left' }
        $oppositeSideInt  = if ($PreferredSide -eq 'Left') { 1 } else { 0 }

        $current = $RootMemberId
        $maxDepth = 200
        for ($d = 0; $d -lt $maxDepth; $d++) {
            # check preferred side child
            $cmd = $cn.CreateCommand()
            $cmd.CommandText = "SELECT MemberId FROM dbo.DualTeamTree WHERE ParentMemberId=@p AND Side=@s"
            [void]$cmd.Parameters.AddWithValue('@p', $current)
            [void]$cmd.Parameters.AddWithValue('@s', $preferredSideInt)
            $r = $cmd.ExecuteReader()
            $childPreferred = $null
            if ($r.Read()) { $childPreferred = $r['MemberId'] }
            $r.Close()

            if (-not $childPreferred) {
                return @{ ParentMemberId = $current; Side = $PreferredSide }
            }

            # preferred side is filled -- check opposite side
            $cmd2 = $cn.CreateCommand()
            $cmd2.CommandText = "SELECT MemberId FROM dbo.DualTeamTree WHERE ParentMemberId=@p AND Side=@s"
            [void]$cmd2.Parameters.AddWithValue('@p', $current)
            [void]$cmd2.Parameters.AddWithValue('@s', $oppositeSideInt)
            $r2 = $cmd2.ExecuteReader()
            $childOpposite = $null
            if ($r2.Read()) { $childOpposite = $r2['MemberId'] }
            $r2.Close()

            if (-not $childOpposite) {
                # Free on opposite side of this node -- but we prefer descending the chain
                # to keep the alternating-chain pattern. We only return the opposite slot
                # if the preferred chain has bottomed out (impossible here -- preferred is filled).
                # Continue descending preferred chain instead.
            }

            # Descend preferred chain
            $current = $childPreferred
        }
        throw "Get-FreeSlot: descended $maxDepth levels without finding a free slot under $RootMemberId on $PreferredSide chain."
    } finally {
        $cn.Close()
    }
}

# ---- Signup helper -------------------------------------------------------
function Invoke-Signup {
    param(
        [string] $FirstName,
        [string] $LastName,
        [string] $SponsorSlug,
        [string] $OwnSlug
    )
    $email   = "p.$run.$($FirstName.ToLower())@example.com"
    $visitor = [guid]::NewGuid().ToString()

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
        ReplicateSiteSlug    = $OwnSlug
    }

    $p1 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/ambassador" -Method Post `
            -ContentType 'application/json' -Body ($body | ConvertTo-Json) -TimeoutSec 30
    if (-not $p1.success) { throw "Phase1 ($FirstName) failed: $($p1 | ConvertTo-Json -Compress)" }
    $signupId = $p1.data.signupId
    $memberId = $p1.data.memberId

    $p2 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/$signupId/select-products" -Method Post `
            -ContentType 'application/json' -Body (@{ productIds = @($EliteProductId) } | ConvertTo-Json) -TimeoutSec 30
    if (-not $p2.success) { throw "Phase2 ($FirstName) failed: $($p2 | ConvertTo-Json -Compress)" }

    $p3 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/signups/$signupId/complete" -Method Post `
            -ContentType 'application/json' `
            -Body (@{ PaymentMethod = 4; DiscountCode = "PLAT-$run" } | ConvertTo-Json) -TimeoutSec 30
    if (-not $p3.success) { throw "Phase3 ($FirstName) failed: $($p3 | ConvertTo-Json -Compress)" }

    return [pscustomobject]@{ MemberId = $memberId; SignupId = $signupId; Email = $email }
}

# ---- Placement helper ----------------------------------------------------
function Invoke-Placement {
    param(
        [string] $NewMemberId,
        [string] $ParentMemberId,
        [string] $Side,
        [string] $Jwt
    )
    if (-not $Jwt) {
        return [pscustomobject]@{ Success = $false; Reason = 'No JWT available' }
    }
    $hdr = @{ Authorization = "Bearer $Jwt" }
    $body = @{ PlaceUnderMemberId = $ParentMemberId; Side = $Side } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/members/$NewMemberId/placement" -Method Post `
                -Headers $hdr -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($r.success) { return [pscustomobject]@{ Success = $true; Reason = '' } }
        return [pscustomobject]@{ Success = $false; Reason = ($r | ConvertTo-Json -Compress) }
    } catch {
        $msg = $_.Exception.Message
        $resp = $null
        try { $resp = $_.ErrorDetails.Message } catch {}
        return [pscustomobject]@{ Success = $false; Reason = "$msg :: $resp" }
    }
}

# ---- Drive the signups + placements --------------------------------------
$newAmbs    = New-Object System.Collections.Generic.List[object]
$placeRes   = New-Object System.Collections.Generic.List[object]
$leftCount  = 0
$rightCount = 0

for ($i = 1; $i -le $NewAmbassadors; $i++) {
    $fn   = "PlatUp${i}${run}"
    $slug = "p-$run-up$i"
    Write-Step "[$i/$NewAmbassadors] Signing up $fn (slug=$slug)"
    try {
        $amb = Invoke-Signup -FirstName $fn -LastName 'Branch' -SponsorSlug $RootSlug -OwnSlug $slug
        Write-Ok "Signed up -> $($amb.MemberId)  (email=$($amb.Email))"
        $newAmbs.Add($amb)

        # alternate L/R preference
        $preferred = if ($i % 2 -eq 1) { 'Left' } else { 'Right' }

        # Find free slot via live DB walk
        $slot = $null
        try {
            $slot = Get-FreeSlot -RootMemberId $RootMemberId -PreferredSide $preferred -ConnString $SqlConnString
        } catch {
            Write-Err2 "Get-FreeSlot failed: $($_.Exception.Message)"
        }
        if (-not $slot) {
            $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$preferred; Parent='?'; Success=$false; Reason='No free slot found' })
        } else {
            $pr = Invoke-Placement -NewMemberId $amb.MemberId -ParentMemberId $slot.ParentMemberId -Side $slot.Side -Jwt $jwt
            if ($pr.Success) {
                if ($slot.Side -eq 'Left') { $leftCount++ } else { $rightCount++ }
                Write-Ok "Placed $($amb.MemberId) under $($slot.ParentMemberId) on $($slot.Side)"
                $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$slot.Side; Parent=$slot.ParentMemberId; Success=$true; Reason='' })
            } else {
                Write-Warn2 "Placement failed: $($pr.Reason)"
                if ($pr.Reason -match '401' -or $pr.Reason -match 'Unauthorized' -or $pr.Reason -match 'INVALID') {
                    Write-Step "Refreshing JWT and retrying placement"
                    $jwt = Refresh-Jwt
                    if ($jwt) {
                        # re-pick slot in case DB state changed
                        $slot = Get-FreeSlot -RootMemberId $RootMemberId -PreferredSide $preferred -ConnString $SqlConnString
                        $pr2 = Invoke-Placement -NewMemberId $amb.MemberId -ParentMemberId $slot.ParentMemberId -Side $slot.Side -Jwt $jwt
                        if ($pr2.Success) {
                            if ($slot.Side -eq 'Left') { $leftCount++ } else { $rightCount++ }
                            Write-Ok "Placed (after refresh) $($amb.MemberId) under $($slot.ParentMemberId) on $($slot.Side)"
                            $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$slot.Side; Parent=$slot.ParentMemberId; Success=$true; Reason='retry-after-refresh' })
                        } else {
                            Write-Err2 "Placement still failed after refresh: $($pr2.Reason)"
                            $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$slot.Side; Parent=$slot.ParentMemberId; Success=$false; Reason=$pr2.Reason })
                        }
                    } else {
                        $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$slot.Side; Parent=$slot.ParentMemberId; Success=$false; Reason=$pr.Reason + ' (refresh-failed)' })
                    }
                } else {
                    $placeRes.Add([pscustomobject]@{ MemberId=$amb.MemberId; Side=$slot.Side; Parent=$slot.ParentMemberId; Success=$false; Reason=$pr.Reason })
                }
            }
        }
    } catch {
        Write-Err2 "Signup #$i failed: $($_.Exception.Message)"
        if ($_.Exception.Message -match '429' -or $_.Exception.Message -match 'Too Many') {
            Write-Warn2 "Rate-limited; sleeping 65s before next attempt"
            Start-Sleep -Seconds 65
        }
    }
    if ($i -lt $NewAmbassadors) {
        Write-Step "Sleeping $SignupSleepSec s (rate-limit)"
        Start-Sleep -Seconds $SignupSleepSec
    }
}

Write-Host ''
Write-Step "Signup summary"
Write-Host ("  New ambassadors created: {0} of {1}" -f $newAmbs.Count, $NewAmbassadors)
foreach ($a in $newAmbs) { Write-Host "    - $($a.MemberId)  (email=$($a.Email))" }
Write-Host ("  Placements: Left={0}  Right={1}  Failed/Skipped={2}" -f $leftCount, $rightCount, ($placeRes | Where-Object { -not $_.Success }).Count)

# ---- Poll for the Platinum certificate PDF -----------------------------------
$certDir = Join-Path $RepoRoot 'MLMConquerorGlobalEdition.RankEngine\wwwroot\certificates'
Write-Step "Polling for *_${RootMemberId}_${TargetRankSuffix}.pdf in: $certDir"
Write-Warn2 "ProcessRankQueueJob runs every 5 min - waiting up to $CertPollSeconds s."

$deadline = (Get-Date).AddSeconds($CertPollSeconds)
$found    = $null
while ((Get-Date) -lt $deadline -and -not $found) {
    if (Test-Path $certDir) {
        $found = Get-ChildItem -Path $certDir -Filter "*_${RootMemberId}_${TargetRankSuffix}.pdf" -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if (-not $found) {
        $remaining = [int](($deadline - (Get-Date)).TotalSeconds)
        Write-Host ("    ...waiting ({0}s remaining)" -f $remaining) -ForegroundColor DarkGray
        Start-Sleep -Seconds $CertPollEvery
    }
}

if ($found) {
    Write-Ok "CERTIFICATE FOUND: $($found.FullName)  ($($found.Length) bytes)"
    $hdr = [System.IO.File]::ReadAllBytes($found.FullName)[0..3]
    $isPdf = ($hdr[0] -eq 0x25 -and $hdr[1] -eq 0x50 -and $hdr[2] -eq 0x44 -and $hdr[3] -eq 0x46)
    Write-Ok "Valid PDF header (%PDF): $isPdf"
    exit 0
} else {
    Write-Err2 "No *_${RootMemberId}_${TargetRankSuffix}.pdf appeared within $CertPollSeconds s."
    Write-Err2 "Check RankEngine console output for rank-evaluation / PDF-generation errors."
    exit 1
}
