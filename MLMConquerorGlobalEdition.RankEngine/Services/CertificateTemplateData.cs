namespace MLMConquerorGlobalEdition.RankEngine.Services;

public record CertificateTemplateData(
    string FullName,
    string MemberId,
    string RankName,
    string RankDescription,
    DateTime AchievedAt);
