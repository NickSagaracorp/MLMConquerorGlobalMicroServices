namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Data passed to the certificate PDF filler.
/// Templates only expose two AcroForm fields: FullName and AchievedDate.
/// The remaining properties are carried for logging / S3 key construction.
/// </summary>
public record CertificateTemplateData(
    string   FullName,
    string   MemberId,
    string   RankName,
    DateTime AchievedAt);
