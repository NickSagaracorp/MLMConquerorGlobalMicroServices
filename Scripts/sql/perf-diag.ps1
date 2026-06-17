$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
function Scalar($t){ $c=$conn.CreateCommand(); $c.CommandText=$t; $c.CommandTimeout=300; return $c.ExecuteScalar() }
function Timed($label,$t){
  $c=$conn.CreateCommand(); $c.CommandText=$t; $c.CommandTimeout=300
  $sw=[System.Diagnostics.Stopwatch]::StartNew()
  $v=$c.ExecuteScalar()
  $sw.Stop()
  "{0,-46} {1,8} ms   (result={2})" -f $label, $sw.ElapsedMilliseconds, $v
}

$A='AMB-700829'
$gpath = Scalar "SELECT HierarchyPath FROM GenealogyTree WHERE MemberId='$A'"
"A genealogy HierarchyPath = $gpath  (len=$($gpath.Length))"

# indexes on the two tree tables
"`nIndexes on GenealogyTree:"
$c=$conn.CreateCommand(); $c.CommandText="SELECT i.name + ' (' + STUFF((SELECT ', '+col.name FROM sys.index_columns ic JOIN sys.columns col ON col.object_id=ic.object_id AND col.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.key_ordinal FOR XML PATH('')),1,2,'') + ')' FROM sys.indexes i WHERE i.object_id=OBJECT_ID('GenealogyTree') AND i.type>0"
$r=$c.ExecuteReader(); while($r.Read()){ "  " + $r[0] }; $r.Close()
"Indexes on DualTeamTree:"
$c=$conn.CreateCommand(); $c.CommandText="SELECT i.name + ' (' + STUFF((SELECT ', '+col.name FROM sys.index_columns ic JOIN sys.columns col ON col.object_id=ic.object_id AND col.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.key_ordinal FOR XML PATH('')),1,2,'') + ')' FROM sys.indexes i WHERE i.object_id=OBJECT_ID('DualTeamTree') AND i.type>0"
$r=$c.ExecuteReader(); while($r.Read()){ "  " + $r[0] }; $r.Close()

"`n--- timings ---"
$prefix = $gpath.Replace("'","''")
Timed "GEN subtree count (StartsWith, nvarchar(max))" "SELECT COUNT_BIG(1) FROM GenealogyTree WHERE HierarchyPath LIKE N'$prefix%'"
Timed "DUAL subtree count (LIKE)" "SELECT COUNT_BIG(1) FROM DualTeamTree WHERE HierarchyPath LIKE N'%$A%'"
Timed "GEN direct children (adjacency ParentMemberId)" "SELECT COUNT_BIG(1) FROM GenealogyTree WHERE ParentMemberId='$A'"
$conn.Close()
