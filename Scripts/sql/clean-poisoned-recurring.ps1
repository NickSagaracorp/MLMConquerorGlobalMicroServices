$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
function Exec($t){ $c=$conn.CreateCommand(); $c.CommandText=$t; $c.CommandTimeout=60; return $c.ExecuteNonQuery() }

# Remove the poisoned recurring-job bookkeeping so SignupAPI's RecurringJob.AddOrUpdate
# recreates it fresh (pointing at the new Repository.Jobs type). Hangfire offers no API to
# clear a recurring job's scheduler error state, so deleting the Hash + Set rows is the
# standard remediation.
$jobId = 'apply-member-statistic-deltas'
$h = Exec "DELETE FROM [HangFire].[Hash] WHERE [Key]='recurring-job:$jobId'"
$s = Exec "DELETE FROM [HangFire].[Set] WHERE [Key]='recurring-jobs' AND [Value]='$jobId'"
"Deleted Hash rows:  $h"
"Deleted Set rows:   $s"
$conn.Close()
