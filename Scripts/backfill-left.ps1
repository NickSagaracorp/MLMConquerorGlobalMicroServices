<#
.SYNOPSIS
    Place remaining unplaced ambassadors on A's LEFT subtree using a
    breadth-first first-empty-slot strategy (keeps chains short and balanced
    to avoid the 1700-byte HierarchyPath index limit).
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
}
function W($m,$c='Cyan'){ Write-Host $m -ForegroundColor $c }

$body = @{ Email = $RootEmail; Password = $RootPassword } | ConvertTo-Json
$lr = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
$jwt = $lr.data.accessToken
if (-not $jwt) { $jwt = $lr.data.AccessToken }
W "Got JWT (len=$($jwt.Length))" 'Green'

function Find-BfsFreeSlot {
    <#
       Finds the first free L or R slot in A's left subtree by BFS — keeps the
       tree wide rather than deep.
       Returns @{ ParentMemberId; Side } or $null if everything is full.
    #>
    param([string] $ConnString, [string] $SubtreeRootMember, [int] $MaxPathBytes = 1500)
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString
    $cn.Open()
    try {
        # BFS — load ALL nodes in this subtree, ordered by path length (shorter first = shallower)
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = "SELECT MemberId, HierarchyPath, LEN(HierarchyPath) AS PathLen FROM dbo.DualTeamTree WHERE HierarchyPath LIKE '/AMB-700829/' + @r + '/%' OR MemberId=@r ORDER BY LEN(HierarchyPath) ASC, MemberId"
        [void]$cmd.Parameters.AddWithValue('@r', $SubtreeRootMember)
        $r = $cmd.ExecuteReader()
        $nodes = @()
        while ($r.Read()) { $nodes += [pscustomobject]@{ MemberId=$r['MemberId']; Path=$r['HierarchyPath']; Len=[int]$r['PathLen'] } }
        $r.Close()

        # For each, check which sides are free
        foreach ($n in $nodes) {
            if ($n.Len -gt $MaxPathBytes) { continue }  # skip too deep, would blow index
            $cmd2 = $cn.CreateCommand()
            $cmd2.CommandText = "SELECT Side FROM dbo.DualTeamTree WHERE ParentMemberId=@p"
            [void]$cmd2.Parameters.AddWithValue('@p', $n.MemberId)
            $r2 = $cmd2.ExecuteReader()
            $left=$false; $right=$false
            while ($r2.Read()) {
                if ([int]$r2['Side'] -eq 0) { $left=$true } else { $right=$true }
            }
            $r2.Close()
            if (-not $left)  { return @{ ParentMemberId=$n.MemberId; Side='Left' } }
            if (-not $right) { return @{ ParentMemberId=$n.MemberId; Side='Right' } }
        }
        return $null
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
    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnString; $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = @"
SELECT m.MemberId FROM dbo.MemberProfiles m
JOIN dbo.GenealogyTree g ON g.MemberId = m.MemberId AND g.IsDeleted = 0
WHERE (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829')
  AND m.MemberType = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId = m.MemberId)
ORDER BY m.CreationDate
"@
        $r = $cmd.ExecuteReader(); $ids=@()
        while ($r.Read()) { $ids += [string]$r['MemberId'] }
        $r.Close(); return ,$ids
    } finally { $cn.Close() }
}

$unplaced = Get-Unplaced -ConnString $SqlConnString
W "Unplaced ambassadors: $($unplaced.Count)" 'Cyan'

# The left direct of A is AMB-550531. We'll BFS within that subtree.
$leftDirect  = 'AMB-550531'
$i = 0; $ok = 0; $fail = 0
foreach ($id in $unplaced) {
    $i++
    $slot = Find-BfsFreeSlot -ConnString $SqlConnString -SubtreeRootMember $leftDirect
    if (-not $slot) {
        W ("[{0}] No free slot found in LEFT subtree of A under {1}." -f $i, $leftDirect) 'Yellow'
        $fail++
        continue
    }
    $pr = Invoke-Placement -NewMemberId $id -ParentMemberId $slot.ParentMemberId -Side $slot.Side -Jwt $jwt
    if ($pr.Success) {
        $ok++
        W ("[{0}/{1}] Placed {2} under {3} on {4}" -f $i, $unplaced.Count, $id, $slot.ParentMemberId, $slot.Side) 'Green'
    } else {
        $fail++
        W ("[{0}] FAIL {1} (side={2} under {3}): {4}" -f $i, $id, $slot.Side, $slot.ParentMemberId, $pr.Reason) 'Yellow'
    }
}

W ""
W ("===== Backfill-left summary =====  ok={0}  fail={1}" -f $ok, $fail) 'Cyan'
