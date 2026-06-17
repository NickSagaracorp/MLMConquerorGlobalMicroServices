<#
.SYNOPSIS
    Place unplaced ambassadors under A by interleaving LEFT and RIGHT subtrees
    using an in-memory BFS slot finder. Supports a target split (LeftCount/RightCount).
#>
[CmdletBinding()]
param(
    [string] $SignupApiBase  = 'https://localhost:7005',
    [string] $RootMemberId   = 'AMB-700829',
    [string] $RootDirectLeft = 'AMB-550531',
    [string] $RootDirectRight= 'AMB-375401',
    [string] $RootEmail      = 't11.de5cd5ef.alphade5cd5ef@example.com',
    [string] $RootPassword   = 'P@ssw0rd!2026',
    [string] $SqlConnString  = 'Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;',
    [int]    $LeftCount      = 180,
    [int]    $RightCount     = 170
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
$body = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
$lr = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
$jwt = $lr.data.accessToken; if (-not $jwt) { $jwt = $lr.data.AccessToken }
W "Got JWT" 'Green'

# Load both subtrees once
function Load-Subtree($conn, $directChild) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT MemberId, ParentMemberId, Side, HierarchyPath FROM dbo.DualTeamTree WHERE HierarchyPath LIKE '/AMB-700829/$directChild/%' OR MemberId='$directChild'"
    $r = $cmd.ExecuteReader()
    $nodes = New-Object System.Collections.Generic.List[object]
    while ($r.Read()) {
        $nodes.Add([pscustomobject]@{ MemberId = $r['MemberId']; ParentMemberId = ($r['ParentMemberId'] -as [string]); Side = [int]$r['Side']; Path = [string]$r['HierarchyPath']; PathLen = ([string]$r['HierarchyPath']).Length })
    }
    $r.Close()
    return ,$nodes
}

function Build-Slots($nodes) {
    $occupied = @{}
    foreach ($n in $nodes) {
        if ($n.ParentMemberId) {
            if (-not $occupied.ContainsKey($n.ParentMemberId)) { $occupied[$n.ParentMemberId] = @{Left=$false;Right=$false} }
            if ($n.Side -eq 0) { $occupied[$n.ParentMemberId].Left = $true } else { $occupied[$n.ParentMemberId].Right = $true }
        }
    }
    $sortedNodes = $nodes | Sort-Object PathLen
    $slots = New-Object System.Collections.Queue
    foreach ($n in $sortedNodes) {
        if ($n.PathLen -gt 1500) { continue }
        $slot = $occupied[$n.MemberId]
        if (-not $slot) { $slot = @{Left=$false;Right=$false} }
        if (-not $slot.Left)  { $slots.Enqueue(@{ParentMemberId=$n.MemberId; ParentPath=$n.Path; Side='Left'}) }
        if (-not $slot.Right) { $slots.Enqueue(@{ParentMemberId=$n.MemberId; ParentPath=$n.Path; Side='Right'}) }
    }
    return @{ Occ=$occupied; Slots=$slots }
}

$cn = New-Object System.Data.SqlClient.SqlConnection $SqlConnString
$cn.Open()
$leftNodes = Load-Subtree $cn $RootDirectLeft
$rightNodes = Load-Subtree $cn $RootDirectRight

$cmd2 = $cn.CreateCommand()
$cmd2.CommandText = "SELECT m.MemberId FROM dbo.MemberProfiles m JOIN dbo.GenealogyTree g ON g.MemberId=m.MemberId AND g.IsDeleted=0 WHERE m.MemberType=0 AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId=m.MemberId) AND (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829') ORDER BY m.CreationDate DESC"
$r2 = $cmd2.ExecuteReader()
$unplaced = New-Object System.Collections.Generic.List[string]
while ($r2.Read()) { $unplaced.Add([string]$r2['MemberId']) }
$r2.Close()
$cn.Close()

W "Left  subtree nodes: $($leftNodes.Count)" 'Cyan'
W "Right subtree nodes: $($rightNodes.Count)" 'Cyan'
W "Unplaced to place: $($unplaced.Count) (target L=$LeftCount + R=$RightCount = $($LeftCount+$RightCount))" 'Cyan'

$L = Build-Slots $leftNodes
$R = Build-Slots $rightNodes
W "Left  slots available: $($L.Slots.Count)" 'Cyan'
W "Right slots available: $($R.Slots.Count)" 'Cyan'

$hdr = @{ Authorization = "Bearer $jwt" }
$okL=0; $okR=0; $fail=0
$sw = [Diagnostics.Stopwatch]::StartNew()
$leftDone = 0; $rightDone = 0
$idx = 0
foreach ($newId in $unplaced) {
    # Decide side: alternate until both per-side budgets reached
    $useLeft = $false
    if ($leftDone -lt $LeftCount -and $rightDone -lt $RightCount) {
        # Both have budget; interleave roughly proportional to target
        if (($idx % 2) -eq 0 -and $L.Slots.Count -gt 0) { $useLeft = $true }
        elseif ($R.Slots.Count -gt 0) { $useLeft = $false }
        elseif ($L.Slots.Count -gt 0) { $useLeft = $true }
        else { W "No slots remaining on either side" 'Red'; break }
    }
    elseif ($leftDone -lt $LeftCount -and $L.Slots.Count -gt 0) { $useLeft = $true }
    elseif ($rightDone -lt $RightCount -and $R.Slots.Count -gt 0) { $useLeft = $false }
    else { W "Quotas met or no slots." 'Yellow'; break }

    $side = if ($useLeft) { $L } else { $R }
    $sideName = if ($useLeft) { 'L' } else { 'R' }
    if ($side.Slots.Count -eq 0) { W "No more slots on $sideName" 'Yellow'; continue }
    $slot = $side.Slots.Dequeue()
    $body = @{ PlaceUnderMemberId = $slot.ParentMemberId; Side = $slot.Side } | ConvertTo-Json
    try {
        $r3 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/members/$newId/placement" -Method Post -Headers $hdr -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($r3.success) {
            if ($useLeft) { $okL++; $leftDone++ } else { $okR++; $rightDone++ }
            $newPath = "$($slot.ParentPath)$newId/"
            if (-not $side.Occ.ContainsKey($slot.ParentMemberId)) { $side.Occ[$slot.ParentMemberId] = @{Left=$false;Right=$false} }
            if ($slot.Side -eq 'Left') { $side.Occ[$slot.ParentMemberId].Left = $true } else { $side.Occ[$slot.ParentMemberId].Right = $true }
            if ($newPath.Length -le 1500) {
                $side.Slots.Enqueue(@{ParentMemberId=$newId; ParentPath=$newPath; Side='Left'})
                $side.Slots.Enqueue(@{ParentMemberId=$newId; ParentPath=$newPath; Side='Right'})
            }
            if ((($okL+$okR) % 25) -eq 0) { W "[L=$okL R=$okR] last: $newId -> $sideName/$($slot.ParentMemberId)" 'Green' }
        } else {
            $fail++; W "FAIL $newId ($sideName): $($r3 | ConvertTo-Json -Compress)" 'Yellow'
        }
    } catch {
        $fail++
        $msg = $_.Exception.Message; $det = try { $_.ErrorDetails.Message } catch { '' }
        W "FAIL $newId ($sideName): $msg :: $det" 'Yellow'
    }
    $idx++
}
$sw.Stop()
W ""
W ("===== both-fast summary =====  okL={0}  okR={1}  fail={2}  elapsed={3:F1}s" -f $okL, $okR, $fail, $sw.Elapsed.TotalSeconds) 'Cyan'
