<#
.SYNOPSIS
    Place up to $Count unplaced ambassadors on A's RIGHT subtree using a fast
    in-memory BFS slot finder (one DB read up front, then iterates locally).
#>
[CmdletBinding()]
param(
    [string] $SignupApiBase  = 'https://localhost:7005',
    [string] $RootMemberId   = 'AMB-700829',
    [string] $RootDirectRight= 'AMB-375401',
    [string] $RootEmail      = 't11.de5cd5ef.alphade5cd5ef@example.com',
    [string] $RootPassword   = 'P@ssw0rd!2026',
    [string] $SqlConnString  = 'Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=True;TrustServerCertificate=True;',
    [int]    $Count          = 60
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

# Get the right-subtree snapshot ONCE
$cn = New-Object System.Data.SqlClient.SqlConnection $SqlConnString
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT MemberId, ParentMemberId, Side, HierarchyPath FROM dbo.DualTeamTree WHERE HierarchyPath LIKE '/AMB-700829/$RootDirectRight/%' OR MemberId='$RootDirectRight'"
$r = $cmd.ExecuteReader()
$nodes = New-Object System.Collections.Generic.List[object]
while ($r.Read()) {
    $nodes.Add([pscustomobject]@{ MemberId = $r['MemberId']; ParentMemberId = ($r['ParentMemberId'] -as [string]); Side = [int]$r['Side']; Path = [string]$r['HierarchyPath']; PathLen = ([string]$r['HierarchyPath']).Length })
}
$r.Close()

# Get unplaced AMB list
$cmd2 = $cn.CreateCommand()
$cmd2.CommandText = "SELECT TOP $Count m.MemberId FROM dbo.MemberProfiles m JOIN dbo.GenealogyTree g ON g.MemberId=m.MemberId AND g.IsDeleted=0 WHERE m.MemberType=0 AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId=m.MemberId) AND (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829') ORDER BY m.CreationDate DESC"
$r2 = $cmd2.ExecuteReader()
$unplaced = New-Object System.Collections.Generic.List[string]
while ($r2.Read()) { $unplaced.Add([string]$r2['MemberId']) }
$r2.Close()
$cn.Close()

W "Right-subtree nodes: $($nodes.Count)" 'Cyan'
W "Unplaced to place: $($unplaced.Count) (cap=$Count)" 'Cyan'

# Build occupancy map and a BFS queue
$occupied = @{}
foreach ($n in $nodes) {
    if ($n.ParentMemberId) {
        if (-not $occupied.ContainsKey($n.ParentMemberId)) { $occupied[$n.ParentMemberId] = @{Left=$false;Right=$false} }
        if ($n.Side -eq 0) { $occupied[$n.ParentMemberId].Left = $true } else { $occupied[$n.ParentMemberId].Right = $true }
    }
}
# Sort by path length ascending (BFS shallowest first)
$sortedNodes = $nodes | Sort-Object PathLen

$slots = New-Object System.Collections.Queue
foreach ($n in $sortedNodes) {
    if ($n.PathLen -gt 1500) { continue }
    $slot = $occupied[$n.MemberId]
    if (-not $slot) { $slot = @{Left=$false;Right=$false} }
    if (-not $slot.Left)  { $slots.Enqueue(@{ParentMemberId=$n.MemberId; ParentPath=$n.Path; Side='Left'}) }
    if (-not $slot.Right) { $slots.Enqueue(@{ParentMemberId=$n.MemberId; ParentPath=$n.Path; Side='Right'}) }
}
W "Available slots: $($slots.Count)" 'Cyan'

$hdr = @{ Authorization = "Bearer $jwt" }
$ok=0; $fail=0
$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($newId in $unplaced) {
    if ($slots.Count -eq 0) {
        W "No more slots in right subtree." 'Yellow'; break
    }
    $slot = $slots.Dequeue()
    $body = @{ PlaceUnderMemberId = $slot.ParentMemberId; Side = $slot.Side } | ConvertTo-Json
    try {
        $r3 = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/members/$newId/placement" -Method Post -Headers $hdr -ContentType 'application/json' -Body $body -TimeoutSec 30
        if ($r3.success) {
            $ok++
            $newPath = "$($slot.ParentPath)$newId/"
            # Add to occupancy snapshot so subsequent placements see this slot taken
            if (-not $occupied.ContainsKey($slot.ParentMemberId)) { $occupied[$slot.ParentMemberId] = @{Left=$false;Right=$false} }
            if ($slot.Side -eq 'Left') { $occupied[$slot.ParentMemberId].Left = $true } else { $occupied[$slot.ParentMemberId].Right = $true }
            # Enqueue slots under the new node
            if ($newPath.Length -le 1500) {
                $slots.Enqueue(@{ParentMemberId=$newId; ParentPath=$newPath; Side='Left'})
                $slots.Enqueue(@{ParentMemberId=$newId; ParentPath=$newPath; Side='Right'})
            }
            if ($ok % 10 -eq 0) { W "[$ok placed] last: $newId -> $($slot.ParentMemberId)/$($slot.Side)" 'Green' }
        } else {
            $fail++; W "FAIL $newId : $($r3 | ConvertTo-Json -Compress)" 'Yellow'
        }
    } catch {
        $fail++
        $msg = $_.Exception.Message; $det = try { $_.ErrorDetails.Message } catch { '' }
        W "FAIL $newId : $msg :: $det" 'Yellow'
    }
}
$sw.Stop()
W ""
W ("===== right-fast summary =====  ok={0}  fail={1}  elapsed={2:F1}s" -f $ok, $fail, $sw.Elapsed.TotalSeconds) 'Cyan'
