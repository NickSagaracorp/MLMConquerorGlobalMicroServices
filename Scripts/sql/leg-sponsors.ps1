$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

function MaxDepthUnder($gw){
  $c = $conn.CreateCommand(); $c.CommandTimeout = 300
  # Plan Q: el binario no tiene ruta de texto; profundidad = Depth y pertenencia por la clave (dbo.DualAncestorKeys).
  $c.CommandText = "SELECT MAX(d.Depth) FROM DualTeamTree d WHERE d.IsDeleted=0 AND (d.MemberId=@gw OR EXISTS (SELECT 1 FROM dbo.DualAncestorKeys(d.BinaryPath, 0) a WHERE a.AncestorKey = (SELECT x.BinaryPath FROM DualTeamTree x WHERE x.MemberId = @gw AND x.IsDeleted = 0)))"
  [void]$c.Parameters.AddWithValue('@gw',$gw)
  return $c.ExecuteScalar()
}

# Shallow sponsors with slugs + an open slot (fewer than 2 children) under a gateway leg.
function WriteSponsors($gw, $outFile, $top){
  $c = $conn.CreateCommand(); $c.CommandTimeout = 300
  $c.CommandText = @"
SELECT TOP ($top) d.MemberId, mp.ReplicateSiteSlug AS Slug, d.Depth
FROM DualTeamTree d
JOIN MemberProfiles mp ON mp.MemberId=d.MemberId
WHERE d.IsDeleted=0 AND mp.IsDeleted=0
  AND mp.ReplicateSiteSlug IS NOT NULL AND mp.ReplicateSiteSlug<>''
  AND (d.MemberId=@gw OR EXISTS (SELECT 1 FROM dbo.DualAncestorKeys(d.BinaryPath, 0) a WHERE a.AncestorKey = (SELECT x.BinaryPath FROM DualTeamTree x WHERE x.MemberId = @gw AND x.IsDeleted = 0)))
ORDER BY d.Depth ASC, d.MemberId
"@
  [void]$c.Parameters.AddWithValue('@gw',$gw)
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
