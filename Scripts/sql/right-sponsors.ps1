$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
# Revision del bloque 3 del Plan Q: el equipo es el rango de la clave (dual-subtree-bounds.sql), no expandir ascendientes.
$subtreeBounds = Get-Content -Raw (Join-Path $PSScriptRoot 'dual-subtree-bounds.sql')

# Confirm A's recorded rank
$c0 = $conn.CreateCommand()
$c0.CommandText = "SELECT TOP 1 CONCAT(rd.Name,' SO',rd.SortOrder) FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='AMB-700829' ORDER BY h.CreationDate DESC"
"A recorded rank: " + $c0.ExecuteScalar()

# Right-subtree members (binary descendants of AMB-375401, A's right child) that have a replicate slug.
# Plan Q: el binario no tiene ruta de texto; el equipo es el rango de la clave.
$rightGw = 'AMB-375401'
$c = $conn.CreateCommand()
$c.CommandTimeout = 180
$c.CommandText = "DECLARE @k varbinary(1700) = (SELECT x.BinaryPath FROM DualTeamTree x WHERE x.MemberId = @gw AND x.IsDeleted = 0); $subtreeBounds SELECT TOP 60 d.MemberId, mp.ReplicateSiteSlug AS Slug FROM DualTeamTree d JOIN MemberProfiles mp ON mp.MemberId=d.MemberId WHERE d.IsDeleted=0 AND mp.IsDeleted=0 AND mp.ReplicateSiteSlug IS NOT NULL AND mp.ReplicateSiteSlug<>'' AND (d.BinaryPath > @lo AND d.BinaryPath < @hi)"
[void]$c.Parameters.AddWithValue('@gw',$rightGw)
$r = $c.ExecuteReader()
$lines = New-Object System.Collections.Generic.List[string]
while($r.Read()){ $lines.Add([string]$r['MemberId'] + '|' + [string]$r['Slug']) }
$r.Close()
$conn.Close()

$out = "C:\Users\sagar\source\repos\ClaudeRepository\Scripts\SignupLoadTest\sponsors-blueroyal-right.txt"
Set-Content -Path $out -Value $lines -Encoding UTF8
"Wrote $($lines.Count) right-subtree sponsors to $out"
$lines | Select-Object -First 5
