$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

# Confirm A's recorded rank
$c0 = $conn.CreateCommand()
$c0.CommandText = "SELECT TOP 1 CONCAT(rd.Name,' SO',rd.SortOrder) FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='AMB-700829' ORDER BY h.CreationDate DESC"
"A recorded rank: " + $c0.ExecuteScalar()

# Right-subtree members (binary descendants of AMB-375401, A's right child) that have a replicate slug.
$rightGw = 'AMB-375401'
$pat = '%' + $rightGw + '%'
$c = $conn.CreateCommand()
$c.CommandTimeout = 180
$c.CommandText = "SELECT TOP 60 d.MemberId, mp.ReplicateSiteSlug AS Slug FROM DualTeamTree d JOIN MemberProfiles mp ON mp.MemberId=d.MemberId WHERE d.IsDeleted=0 AND mp.IsDeleted=0 AND mp.ReplicateSiteSlug IS NOT NULL AND mp.ReplicateSiteSlug<>'' AND (d.MemberId=@gw OR d.HierarchyPath LIKE @pat)"
[void]$c.Parameters.AddWithValue('@gw',$rightGw)
[void]$c.Parameters.AddWithValue('@pat',$pat)
$r = $c.ExecuteReader()
$lines = New-Object System.Collections.Generic.List[string]
while($r.Read()){ $lines.Add([string]$r['MemberId'] + '|' + [string]$r['Slug']) }
$r.Close()
$conn.Close()

$out = "C:\Users\sagar\source\repos\ClaudeRepository\Scripts\SignupLoadTest\sponsors-blueroyal-right.txt"
Set-Content -Path $out -Value $lines -Encoding UTF8
"Wrote $($lines.Count) right-subtree sponsors to $out"
$lines | Select-Object -First 5
