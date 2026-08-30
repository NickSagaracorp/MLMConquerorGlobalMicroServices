namespace MLMConquerorGlobalEdition.SharedKernel.Constants;

/// <summary>
/// Los nombres de rol, una sola vez.
///
/// POR QUÉ ESTÁ EN SharedKernel Y NO EN SharedComponents: aquí lo necesitan los dos lados. El
/// portal esconde botones con estos nombres, pero quien de verdad decide es el servidor, y
/// AdminAPI y SignupAPI no pueden referenciar SharedComponents —es una biblioteca Razor con
/// Syncfusion dentro—. SharedKernel es lo único que ven los cuatro anfitriones web, las dos MAUI
/// y las dos APIs, y no arrastra ASP.NET Core, así que sigue viajando al móvil igual que antes.
///
/// Los conjuntos de abajo son cadenas const a propósito, no arrays: [Authorize(Roles = ...)]
/// exige una constante en tiempo de compilación, y la concatenación de const lo es. Así el mismo
/// símbolo sirve para el atributo del controlador, para el atributo de la página y para la
/// prueba que los compara.
/// </summary>
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

    /// <summary>
    /// Quién puede confirmar un cobro en cripto. Lo fijó el dueño del producto: confirmar aquí
    /// activa una membresía y dispara comisiones al upline, así que la lista es corta y no
    /// coincide con ninguna de las de abajo.
    /// </summary>
    public const string CryptoPaymentApprovers =
        Admin + "," + SuperAdmin + "," + SupportLevel3 + "," + BillingManager;

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

    /// <summary>Los cuatro roles de <see cref="CryptoPaymentApprovers"/>, ya partidos.</summary>
    public static readonly string[] CryptoPaymentApproverRoles =
        CryptoPaymentApprovers.Split(',');
}
