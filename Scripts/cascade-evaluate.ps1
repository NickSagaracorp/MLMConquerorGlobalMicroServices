<#
.SYNOPSIS
    Trigger rank evaluation for each sponsor in a pool file, 10 concurrent.
    Logs which members got promoted.
#>
[CmdletBinding()]
param(
    [string] $RankApiBase    = 'https://localhost:7009',
    [string] $SignupApiBase  = 'https://localhost:7005',
    [string] $AdminEmail     = 'loadtest-admin@example.com',
    [string] $AdminPassword  = 'P@ssw0rd!2026',
    [string] $SponsorsFile   = 'C:\Users\sagar\source\repos\ClaudeRepository\scripts\SignupLoadTest\sponsors-350x1-cascade-20260524-114134.txt',
    [int]    $Concurrency    = 10,
    [string] $OutCsv         = ''
)
$ErrorActionPreference = 'Stop'

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
[System.Net.ServicePointManager]::DefaultConnectionLimit = 200

function W($m,$c='Cyan'){ Write-Host $m -ForegroundColor $c }

# Login
$loginBody = @{ Email = $AdminEmail; Password = $AdminPassword } | ConvertTo-Json
$lr = Invoke-RestMethod -Uri "$SignupApiBase/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody -TimeoutSec 10
$jwt = $lr.data.accessToken; if (-not $jwt) { $jwt = $lr.data.AccessToken }
W "Got admin JWT (len=$($jwt.Length))" 'Green'

# Parse sponsors file: bucket|slug|EP|ETSize|DTL|DTR|DirectSponsored|MemberId|...
$members = New-Object System.Collections.Generic.List[pscustomobject]
Get-Content $SponsorsFile | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith('#')) { return }
    $p = $line.Split('|')
    if ($p.Count -ge 8) {
        $members.Add([pscustomobject]@{ Bucket=$p[0]; Slug=$p[1]; EP=[int]$p[2]; MemberId=$p[7] }) | Out-Null
    }
}
$members = $members | Sort-Object -Unique MemberId
W "Members to evaluate: $($members.Count)" 'Cyan'

if (-not $OutCsv) {
    $ts = [datetime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $OutCsv = "C:\Users\sagar\source\repos\ClaudeRepository\scripts\SignupLoadTest\cascade-results-$ts.csv"
}

# Async evaluation via runspaces
$pool = [runspacefactory]::CreateRunspacePool(1, $Concurrency)
$pool.Open()

$results = New-Object System.Collections.Concurrent.ConcurrentBag[pscustomobject]
$scriptBlock = {
    param($RankApiBase, $jwt, $member, $ip)
    try {
        $hdr = @{ Authorization = "Bearer $jwt"; "X-Real-IP" = $ip }
        $resp = Invoke-RestMethod -Uri "$RankApiBase/api/v1/ranks/evaluate/$($member.MemberId)" -Method Post -Headers $hdr -TimeoutSec 60 -ErrorAction Stop
        $achieved = $false; $rankId = $null; $rankName = $null; $prev = $null
        if ($resp.success -and $resp.data.rankAchieved) {
            $achieved = $true
            $rankId   = $resp.data.achievedRank.id
            $rankName = $resp.data.achievedRank.name
            if ($resp.data.previousRank) { $prev = $resp.data.previousRank.id }
        }
        return [pscustomobject]@{
            MemberId=$member.MemberId; Bucket=$member.Bucket; PrevEP=$member.EP;
            Achieved=$achieved; RankId=$rankId; RankName=$rankName; PrevRankId=$prev; Error=$null
        }
    } catch {
        return [pscustomobject]@{
            MemberId=$member.MemberId; Bucket=$member.Bucket; PrevEP=$member.EP;
            Achieved=$false; RankId=$null; RankName=$null; PrevRankId=$null; Error=$_.Exception.Message
        }
    }
}

$jobs = @()
$i = 0
$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($m in $members) {
    $i++
    $ip = "10.50.$([int]($i / 256)).$($i % 256)"
    $ps = [PowerShell]::Create().AddScript($scriptBlock).AddArgument($RankApiBase).AddArgument($jwt).AddArgument($m).AddArgument($ip)
    $ps.RunspacePool = $pool
    $jobs += [pscustomobject]@{ PS=$ps; AR=$ps.BeginInvoke() }
}
W "Submitted $($jobs.Count) evaluations to pool of $Concurrency" 'Cyan'

$done = 0
foreach ($j in $jobs) {
    $res = $j.PS.EndInvoke($j.AR)
    foreach ($r in $res) { $results.Add($r) }
    $j.PS.Dispose()
    $done++
    if ($done % 25 -eq 0) { W "  evaluated: $done / $($jobs.Count)" 'DarkGray' }
}
$sw.Stop()
$pool.Close(); $pool.Dispose()

W "" 'White'
W "Done. Elapsed: $([math]::Round($sw.Elapsed.TotalSeconds,1))s" 'Green'

$promoted = $results | Where-Object Achieved
W "Total promoted: $($promoted.Count) / $($results.Count)" 'Yellow'
$byRank = $promoted | Group-Object RankName | Sort-Object Count -Descending
foreach ($g in $byRank) { W ("  {0,-12} {1,4}" -f $g.Name, $g.Count) 'Cyan' }

$results | Select-Object MemberId, Bucket, PrevEP, Achieved, RankId, RankName, PrevRankId, Error | Export-Csv -Path $OutCsv -NoTypeInformation
W "Wrote: $OutCsv" 'Green'
