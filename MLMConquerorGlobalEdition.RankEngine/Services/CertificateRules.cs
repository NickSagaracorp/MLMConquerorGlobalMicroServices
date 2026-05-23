namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Rules governing which ranks receive an achievement certificate.
/// Lifestyle Consultant (SortOrder 0) is the default starting rank — it has no template
/// and no certificate. Every real rank (Silver SortOrder 1 … Black Royal 19) is eligible.
/// </summary>
public static class CertificateRules
{
    public const int MinCertificateSortOrder = 1;

    public static bool IsCertificateEligible(int rankSortOrder)
        => rankSortOrder >= MinCertificateSortOrder;
}
