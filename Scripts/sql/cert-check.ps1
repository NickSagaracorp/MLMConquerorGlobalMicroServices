$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$c = $conn.CreateCommand()
$c.CommandText = "SELECT TOP 3 rd.Name, rd.SortOrder, h.GeneratedCertificateUrl, h.CreationDate FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='AMB-700829' ORDER BY h.CreationDate DESC"
$r = $c.ExecuteReader()
"Latest rank history for AMB-700829:"
while($r.Read()){ "  {0} SO{1}  cert={2}  at={3}" -f $r['Name'],$r['SortOrder'],$r['GeneratedCertificateUrl'],$r['CreationDate'] }
$r.Close()
$conn.Close()
