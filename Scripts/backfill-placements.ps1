<#
.SYNOPSIS
    Backfills binary-tree placements for every Ambassador in A's enrollment
    subtree that is not yet in the binary tree. Builds DEEP+BALANCED chains
    by walking the existing tree to find the first free slot on each side.

.DESCRIPTION
    Uses Get-FreeSlot from promote-to-platinum.ps1 (alternating L/R chain
    descent). Stops if both A's legs cross MinLegPoints (default 178 for a
    safety cushion above Titanium's 175-per-leg cap).
#>
[CmdletBinding()]
param(
    [string] $SignupApiBase   = 'https://localhost:7005',
    [string] $RootMemberId    = 'AMB-700829',
    [string] $RootEmail       = 't11.de5cd5ef.alphade5cd5ef@example.com',
    [string] $RootPassword    = 'P@ssw0rd!2026',
    [string] $SqlConnString   = 'Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;',
    [int]    $MinLegPoints    = 178
)

$ErrorActionPreference = 'Stop'
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

function W($m,$c='Cyan'){ Write-Host $m -ForegroundColor $c }

# Login as A
W "Logging in as $RootEmail" 'Cyan'
$body = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
$lr = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
$jwt = $lr.data.accessToken
if (-not $jwt) { $jwt = $lr.data.AccessToken }
if (-not $jwt) { throw "no JWT" }
W "Got JWT (len=$($jwt.Length))" 'Green'

function Refresh-Jwt {
    $body = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
    $t = $r.data.accessToken; if (-not $t) { $t = $r.data.AccessToken }
    return $t
}

function Get-FreeSlot {
    param([string] $RootMemberId, [string] $PreferredSide, [string] $ConnString)
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString
    $cn.Open()
    try {
        $preferredSideInt = if ($PreferredSide -eq 'Left') { 0 } else { 1 }
        $current = $RootMemberId
        $maxDepth = 200
        for ($d = 0; $d -lt $maxDepth; $d++) {
            $cmd = $cn.CreateCommand()
            $cmd.CommandText = "SELECT MemberId FROM dbo.DualTeamTree WHERE ParentMemberId=@p AND Side=@s"
            [void]$cmd.Parameters.AddWithValue('@p', $current)
            [void]$cmd.Parameters.AddWithValue('@s', $preferredSideInt)
            $r = $cmd.ExecuteReader()
            $childPreferred = $null
            if ($r.Read()) { $childPreferred = $r['MemberId'] }
            $r.Close()
            if (-not $childPreferred) { return @{ ParentMemberId = $current; Side = $PreferredSide } }
            $current = $childPreferred
        }
        throw "max depth"
    } finally { $cn.Close() }
}

function Invoke-Placement {
    param([string] $NewMemberId, [string] $ParentMemberId, [string] $Side, [string] $Jwt)
    $hdr = @{ Authorization = "Bearer $Jwt" }
    $body = @{ PlaceUnderMemberId = $ParentMemberId; Side = $Side } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/members/$NewMemberId/placement" -Method Post -Headers $hdr -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($r.success) { return @{ Success=$true; Reason='' } }
        return @{ Success=$false; Reason=($r | ConvertTo-Json -Compress) }
    } catch {
        $resp = $null; try { $resp = $_.ErrorDetails.Message } catch {}
        return @{ Success=$false; Reason="$($_.Exception.Message) :: $resp" }
    }
}

function Get-Unplaced {
    param([string] $ConnString)
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = @"
SELECT m.MemberId
FROM dbo.MemberProfiles m
JOIN dbo.GenealogyTree g ON g.MemberId = m.MemberId AND g.IsDeleted = 0
WHERE (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829')
  AND m.MemberType = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId = m.MemberId)
ORDER BY m.CreationDate
"@
        $r = $cmd.ExecuteReader()
        $ids = @()
        while ($r.Read()) { $ids += [string]$r['MemberId'] }
        $r.Close()
        return ,$ids
    } finally { $cn.Close() }
}

function Get-ALegs {
    param([string] $ConnString)
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = "SELECT LeftLegPoints, RightLegPoints FROM dbo.DualTeamTree WHERE MemberId='AMB-700829'"
        $r = $cmd.ExecuteReader()
        if ($r.Read()) { return @{ L=[decimal]$r['LeftLegPoints']; R=[decimal]$r['RightLegPoints'] } }
        return @{ L=0; R=0 }
    } finally { $cn.Close() }
}

$unplaced = Get-Unplaced -ConnString $SqlConnString
W "Unplaced ambassadors in A's subtree: $($unplaced.Count)" 'Cyan'

$leftCount = 0; $rightCount = 0; $failed = 0; $i = 0
$preferred = 'Left'   # start Left; switch based on which leg is currently smaller

foreach ($id in $unplaced) {
    $i++

    # Periodically check A's legs and pick the smaller side to balance
    if ($i % 1 -eq 0) {
        $legs = Get-ALegs -ConnString $SqlConnString
        if ($legs.L -ge $MinLegPoints -and $legs.R -ge $MinLegPoints) {
            W ("Both legs already >= {0} L={1} R={2} -- stopping early." -f $MinLegPoints, $legs.L, $legs.R) 'Green'
            break
        }
        $preferred = if ($legs.L -le $legs.R) { 'Left' } else { 'Right' }
    }

    $slot = $null
    try { $slot = Get-FreeSlot -RootMemberId $RootMemberId -PreferredSide $preferred -ConnString $SqlConnString }
    catch { W "[$i] Get-FreeSlot failed: $($_.Exception.Message)" 'Red'; $failed++; continue }

    $pr = Invoke-Placement -NewMemberId $id -ParentMemberId $slot.ParentMemberId -Side $slot.Side -Jwt $jwt
    if (-not $pr.Success -and ($pr.Reason -match '401|Unauthorized|INVALID')) {
        $jwt = Refresh-Jwt
        $pr = Invoke-Placement -NewMemberId $id -ParentMemberId $slot.ParentMemberId -Side $slot.Side -Jwt $jwt
    }

    if ($pr.Success) {
        if ($slot.Side -eq 'Left') { $leftCount++ } else { $rightCount++ }
        if ($i % 5 -eq 0 -or $i -le 5) {
            $legs2 = Get-ALegs -ConnString $SqlConnString
            W ("[{0}/{1}] Placed {2} under {3} on {4}. A.L={5} A.R={6}" -f $i, $unplaced.Count, $id, $slot.ParentMemberId, $slot.Side, $legs2.L, $legs2.R) 'Green'
        }
    } else {
        $failed++
        $sideStr = $slot.Side
        $parStr  = $slot.ParentMemberId
        $reasonStr = $pr.Reason
        W ("[{0}] FAIL place {1} (side={2} under {3}): {4}" -f $i, $id, $sideStr, $parStr, $reasonStr) 'Yellow'
    }
}

$legsFinal = Get-ALegs -ConnString $SqlConnString
W ""
W "===== Placement backfill summary =====" 'Cyan'
$totalUnpl = $unplaced.Count
$placedTotal = $leftCount + $rightCount
W ("  Total unplaced at start: {0}" -f $totalUnpl) 'White'
W ("  Successful placements:   {0}  L={1} R={2}" -f $placedTotal, $leftCount, $rightCount) 'Green'
W ("  Failed/Skipped:          {0}" -f $failed) 'Yellow'
W ("  A's final legs: L={0}  R={1}" -f $legsFinal.L, $legsFinal.R) 'Green'
W "  Titanium target: 175 per leg" 'White'
