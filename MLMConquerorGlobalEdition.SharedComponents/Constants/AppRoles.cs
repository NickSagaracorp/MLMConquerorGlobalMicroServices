namespace MLMConquerorGlobalEdition.SharedComponents.Constants;

public static class AppRoles
{
    public const string SuperAdmin        = "SuperAdmin";
    public const string Admin             = "Admin";
    public const string CommissionManager = "CommissionManager";
    public const string BillingManager    = "BillingManager";
    public const string SupportManager    = "SupportManager";
    public const string SupportLevel1     = "SupportLevel1";
    public const string SupportLevel2     = "SupportLevel2";
    public const string SupportLevel3     = "SupportLevel3";
    public const string IT                = "IT";
    public const string Ambassador        = "Ambassador";
    public const string Member            = "Member";

    public static readonly string[] AdminRoles =
    [
        SuperAdmin, Admin, CommissionManager, BillingManager,
        SupportManager, SupportLevel1, SupportLevel2, SupportLevel3, IT
    ];

    public static readonly string[] SupportRoles =
    [
        SupportManager, SupportLevel1, SupportLevel2, SupportLevel3, IT
    ];

    public static readonly string[] CanImpersonate =
    [
        SuperAdmin, Admin, SupportManager
    ];
}
