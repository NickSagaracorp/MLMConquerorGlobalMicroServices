$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$c = $conn.CreateCommand()
$c.CommandText = "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('MemberProfiles') AND (name LIKE '%eplicat%' OR name LIKE '%lug%' OR name LIKE '%ite%')"
$r = $c.ExecuteReader()
"MemberProfiles slug-like columns:"
while($r.Read()){ "  " + $r['name'] }
$r.Close()
$conn.Close()
