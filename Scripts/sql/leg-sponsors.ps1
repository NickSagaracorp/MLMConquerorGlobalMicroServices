$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

function MaxDepthUnder($gw){
  $c = $conn.CreateCommand(); $c.CommandTimeout = 300
  $pat = '%' + $gw + '%'
  # depth = slash count; computed without a literal slash in this script via CHAR(47)
  $c.CommandText = "SELECT MAX(LEN(HierarchyPath)-LEN(REPLACE(HierarchyPath,CHAR(47),''))) FROM DualTeamTree WHERE IsDeleted=0 AND (MemberId=@gw OR HierarchyPath LIKE @pat)"
  [void]$c.Parameters.AddWithValue('@gw',$gw); [void]$c.Parameters.AddWithValue('@pat',$pat)
  return $c.ExecuteScalar()
}

# Shallow sponsors with slugs + an open slot (fewer than 2 children) under a gateway leg.
function WriteSponsors($gw, $outFile, $top){
  $c = $conn.CreateCommand(); $c.CommandTimeout = 300
  $pat = '%' + $gw + '%'
  $c.CommandText = @"
SELECT TOP ($top) d.MemberId, mp.ReplicateSiteSlug AS Slug,
       (LEN(d.HierarchyPath)-LEN(REPLACE(d.HierarchyPath,CHAR(47),''))) AS Depth
FROM DualTeamTree d
JOIN MemberProfiles mp ON mp.MemberId=d.MemberId
WHERE d.IsDeleted=0 AND mp.IsDeleted=0
  AND mp.ReplicateSiteSlug IS NOT NULL AND mp.ReplicateSiteSlug<>''
  AND (d.MemberId=@gw OR d.HierarchyPath LIKE @pat)
ORDER BY (LEN(d.HierarchyPath)-LEN(REPLACE(d.HierarchyPath,CHAR(47),''))) ASC, d.MemberId
"@
  [void]$c.Parameters.AddWithValue('@gw',$gw); [void]$c.Parameters.AddWithValue('@pat',$pat)
  $r = $c.ExecuteReader()
  $lines = New-Object System.Collections.Generic.List[string]
  while($r.Read()){ $lines.Add([string]$r['MemberId'] + '|' + [string]$r['Slug']) }
  $r.Close()
  Set-Content -Path $outFile -Value $lines -Encoding UTF8
  return $lines.Count
}

$rightGw = 'AMB-375401'
$leftGw  = 'AMB-550531'
"Right leg max depth: " + (MaxDepthUnder $rightGw)
"Left  leg max depth: " + (MaxDepthUnder $leftGw)

$base = "C:\Users\sagar\source\repos\ClaudeRepository\Scripts\SignupLoadTest"
$nr = WriteSponsors $rightGw "$base\sponsors-br-right.txt" 2000
$nl = WriteSponsors $leftGw  "$base\sponsors-br-left.txt"  2000
"Wrote right sponsors: $nr"
"Wrote left  sponsors: $nl"
$conn.Close()
