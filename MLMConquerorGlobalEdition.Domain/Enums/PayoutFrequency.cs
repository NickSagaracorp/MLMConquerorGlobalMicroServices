namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// How often the company pushes commission payouts to the member's preferred wallet.
/// Configurable per-member from the BizCenter profile, with a company-wide default
/// in CompanyInfo.DefaultPayoutFrequency.
/// </summary>
public enum PayoutFrequency
{
    Daily   = 1,
    Weekly  = 2,
    Monthly = 3
}
