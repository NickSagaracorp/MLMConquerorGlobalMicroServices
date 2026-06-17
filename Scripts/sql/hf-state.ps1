$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$c = $conn.CreateCommand()
$c.CommandText = "SELECT Field, Value FROM [HangFire].[Hash] WHERE [Key]='recurring-job:apply-member-statistic-deltas'"
$r = $c.ExecuteReader()
"recurring-job:apply-member-statistic-deltas hash:"
while($r.Read()){ "  {0} = {1}" -f $r['Field'], $r['Value'] }
$r.Close()
# servers + queue lengths
$c2 = $conn.CreateCommand(); $c2.CommandText = "SELECT COUNT(1) FROM [HangFire].[Server]"
"Hangfire servers: " + $c2.ExecuteScalar()
$c3 = $conn.CreateCommand(); $c3.CommandText = "SELECT TOP 5 Id, StateName, CreatedAt FROM [HangFire].[Job] ORDER BY Id DESC"
$r3 = $c3.ExecuteReader()
"Recent jobs:"
while($r3.Read()){ "  job {0} {1} {2}" -f $r3['Id'], $r3['StateName'], $r3['CreatedAt'] }
$r3.Close()
$conn.Close()
