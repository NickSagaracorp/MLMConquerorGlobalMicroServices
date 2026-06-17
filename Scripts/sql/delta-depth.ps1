$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
function Q($t){ $c=$conn.CreateCommand(); $c.CommandText=$t; $c.CommandTimeout=180; $c.ExecuteScalar() }
"Unapplied deltas:       " + (Q "SELECT COUNT_BIG(1) FROM MemberStatisticDeltas WHERE IsApplied=0")
"Applied deltas:         " + (Q "SELECT COUNT_BIG(1) FROM MemberStatisticDeltas WHERE IsApplied=1")
"Unapplied deltas for A: " + (Q "SELECT COUNT_BIG(1) FROM MemberStatisticDeltas WHERE IsApplied=0 AND MemberId='AMB-700829'")
"Sum unapplied EP for A: " + (Q "SELECT ISNULL(SUM(CAST(EnrollmentPointsDelta AS bigint)),0) FROM MemberStatisticDeltas WHERE IsApplied=0 AND MemberId='AMB-700829'")
$conn.Close()
