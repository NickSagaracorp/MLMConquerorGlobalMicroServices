$cs = "Server=.;Database=MLMConquerorGlobalEditionDb;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
function Q($t){ $c=$conn.CreateCommand(); $c.CommandText=$t; $c.CommandTimeout=180; $c.ExecuteScalar() }
$A = 'AMB-700829'
"A Left leg:        " + (Q "SELECT LeftLegPoints FROM DualTeamTree WHERE MemberId='$A'")
"A Right leg:       " + (Q "SELECT RightLegPoints FROM DualTeamTree WHERE MemberId='$A'")
"A PersonalPoints:  " + (Q "SELECT PersonalPoints FROM MemberStatistics WHERE MemberId='$A'")
"A EnrollmentPts:   " + (Q "SELECT EnrollmentPoints FROM MemberStatistics WHERE MemberId='$A'")
"A directs:         " + (Q "SELECT COUNT(1) FROM MemberProfiles WHERE SponsorMemberId='$A' AND IsDeleted=0")
"A current rank:    " + (Q "SELECT TOP 1 CONCAT(rd.Name,' SO',rd.SortOrder) FROM MemberRankHistories h JOIN RankDefinitions rd ON rd.Id=h.RankDefinitionId WHERE h.MemberId='$A' ORDER BY h.CreationDate DESC")
"RtGW 375401 side:  " + (Q "SELECT Side FROM DualTeamTree WHERE MemberId='AMB-375401'")
"RtGW 375401 parent:" + (Q "SELECT ParentMemberId FROM DualTeamTree WHERE MemberId='AMB-375401'")
"LfGW 550531 side:  " + (Q "SELECT Side FROM DualTeamTree WHERE MemberId='AMB-550531'")
$conn.Close()
