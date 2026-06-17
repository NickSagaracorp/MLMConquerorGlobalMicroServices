$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
foreach($so in 16,17){
  $c = $conn.CreateCommand()
  $c.CommandText = "SELECT TOP 1 h.Id FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='AMB-700829' AND rd.SortOrder=$so ORDER BY h.CreationDate DESC"
  "SO${so}=" + $c.ExecuteScalar()
}
$conn.Close()
