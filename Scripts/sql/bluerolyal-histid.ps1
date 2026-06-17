$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$c = $conn.CreateCommand()
$c.CommandText = "SELECT TOP 1 h.Id FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='AMB-700829' AND rd.SortOrder=18 ORDER BY h.CreationDate DESC"
"BlueRoyalHistId=" + $c.ExecuteScalar()
$conn.Close()
