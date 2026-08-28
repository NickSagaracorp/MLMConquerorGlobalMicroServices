namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Centralised cache key patterns. Use the static helper methods to build keys.
/// TTLs are defined as static TimeSpan fields alongside each key pattern.
/// </summary>
public static class CacheKeys
{
    public static string MemberProfile(string memberId)      => $"member:{memberId}:profile";
    public static string MemberRank(string memberId)         => $"member:{memberId}:rank";
    public static string MemberStats(string memberId)        => $"member:{memberId}:stats";
    public static string MemberTokenBalances(string memberId)=> $"member:{memberId}:token-balances";

    /// <summary>
    /// Dual-team "My Team" page snapshot, keyed by member + page + page size +
    /// search/from/to fingerprint so different filters don't collide. The
    /// downline shape is expensive to compute; team membership rarely changes
    /// minute-by-minute, so a short TTL is a safe perf win.
    /// </summary>
    public static string DualTeamMyTeam(string memberId, int page, int pageSize, string filterFingerprint)
        => $"member:{memberId}:dual-team:my-team:p{page}:s{pageSize}:f{filterFingerprint}";

    /// <summary>
    /// Aggregated left/right leg point totals for a member's binary-tree
    /// position. Computed by summing MemberStatistics.PersonalPoints across
    /// each leg's subtree. Short TTL because new orders / placements move
    /// the totals, but doing this query every time the visualizer or
    /// residuals page renders is expensive.
    /// </summary>
    public static string DualTreeStats(string memberId)
        => $"member:{memberId}:dual-team:leg-points";

    /// <summary>
    /// Admin members list snapshot, keyed by paging + filter fingerprint.
    /// The query joins MemberProfile + DualTeamTree + MembershipSubscription
    /// + MemberRankHistory and is by far the most-clicked admin grid; even a
    /// short TTL gives a noticeable perf win when admins page through.
    /// </summary>
    public static string AdminMembers(int page, int pageSize, string filterFingerprint)
        => $"admin:members:p{page}:s{pageSize}:f{filterFingerprint}";

    /// <summary>
    /// Top-of-page Members stat row (Active / NewSignups24h / Cancellations24h /
    /// Placements24h). Four COUNT queries against MemberProfile / DualTeamTree /
    /// MemberStatusHistory — cheap individually but rendered on every page load
    /// of the admin members grid. ~30s TTL is short enough that a refresh
    /// button reflects new signups within a heartbeat while still absorbing
    /// rapid page-back navigation.
    /// </summary>
    public const string AdminMemberStats = "admin:member-stats";

    /// <summary>
    /// CEO / executive dashboard snapshot. The dashboard handler runs ~30 DB
    /// queries (counts, sums, group-bys) — perfect cache candidate. TTL is
    /// short so freshly-billed orders still surface within minutes.
    /// </summary>
    public const string AdminCeoDashboard       = "admin:dashboard:ceo";
    public const string AdminFinancialDashboard = "admin:dashboard:financial";
    public const string AdminGrowthDashboard    = "admin:dashboard:growth";
    public const string AdminHealthDashboard    = "admin:dashboard:health";

    /// <summary>
    /// Marca de challenge de 2FA ya redimido, por <c>jti</c>. Sin ella el mismo token
    /// serviría dos veces dentro de su ventana de vida: el challenge es stateless y su
    /// firma sigue siendo válida después de haberlo usado. Se escribe también cuando se
    /// agotan los intentos, para que el challenge quemado no admita ni uno más.
    /// TTL: lo que le quede de vida al challenge, así que no hay constante aquí.
    /// </summary>
    public static string TwoFactorChallengeConsumed(string jti) => $"2fa:challenge:{jti}:consumed";

    /// <summary>
    /// Contador de intentos fallidos de un challenge, por <c>jti</c>. Al superar el máximo
    /// el challenge se quema: hay que pedir otro. Sin este límite, quien tenga un challenge
    /// válido puede recorrer el millón de códigos de seis dígitos hasta acertar.
    /// TTL: lo que le quede de vida al challenge.
    /// </summary>
    public static string TwoFactorAttempts(string jti) => $"2fa:challenge:{jti}:attempts";

    /// <summary>
    /// Ventana de emisiones por usuario. Limita cuántos códigos se generan y despachan en
    /// un intervalo: cada SMS se paga al proveedor, así que sin este contador quien conozca
    /// un correo puede bombardear a un usuario a costa de la empresa.
    /// TTL: lo que le quede a la ventana configurada.
    /// </summary>
    public static string TwoFactorIssueWindow(string userId) => $"2fa:user:{userId}:issues";

    /// <summary>
    /// Marca de código TOTP ya usado, por usuario y hash del código. Identity acepta el
    /// mismo código durante la tolerancia de reloj (~90 s); en una operación de dinero eso
    /// significaría autorizar dos veces con el mismo código.
    /// TTL: la ventana de tolerancia configurada.
    /// </summary>
    public static string TwoFactorTotpUsed(string userId, string codeHash) =>
        $"2fa:user:{userId}:totp:{codeHash}";

    public const string RankDefinitions  = "rank:definitions";
    public const string MembershipLevels = "membership:levels";
    public const string CommissionTypes  = "commission:types";

    public static readonly TimeSpan MemberProfileTtl       = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MemberRankTtl          = TimeSpan.FromHours(1);
    public static readonly TimeSpan MemberStatsTtl         = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MemberTokenBalancesTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DualTeamMyTeamTtl      = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DualTreeStatsTtl       = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan AdminMembersTtl        = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan AdminMemberStatsTtl    = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan AdminCeoDashboardTtl       = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan AdminFinancialDashboardTtl = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan AdminGrowthDashboardTtl    = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan AdminHealthDashboardTtl    = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan RankDefinitionsTtl     = TimeSpan.FromHours(24);
    public static readonly TimeSpan MembershipLevelsTtl    = TimeSpan.FromHours(24);
    public static readonly TimeSpan CommissionTypesTtl     = TimeSpan.FromHours(24);
}
