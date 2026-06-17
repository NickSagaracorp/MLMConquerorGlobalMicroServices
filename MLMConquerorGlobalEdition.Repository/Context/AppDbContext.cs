using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Email;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Interceptors;

namespace MLMConquerorGlobalEdition.Repository.Context;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public DbSet<GeneralAuditTracking> AuditTracking => Set<GeneralAuditTracking>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<ErrorMessage> ErrorMessages => Set<ErrorMessage>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CountryProduct> CountryProducts => Set<CountryProduct>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<RegionGateway> RegionGateways => Set<RegionGateway>();
    public DbSet<CompanyInfo> CompanyInfo => Set<CompanyInfo>();
    public DbSet<CountryPayoutDefault> CountryPayoutDefaults => Set<CountryPayoutDefault>();
    public DbSet<SignupRiskFingerprint> SignupRiskFingerprints => Set<SignupRiskFingerprint>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<MemberStatusHistory> MemberStatusHistories => Set<MemberStatusHistory>();
    public DbSet<MemberAddressHistory> MemberAddressHistories => Set<MemberAddressHistory>();
    public DbSet<MemberCredentialChangeLog> MemberCredentialChangeLogs => Set<MemberCredentialChangeLog>();
    public DbSet<MemberIdentificationType> MemberIdentificationTypes => Set<MemberIdentificationType>();
    public DbSet<MemberStatisticEntity> MemberStatistics => Set<MemberStatisticEntity>();
    public DbSet<MemberStatisticHistoryEntity> MemberStatisticHistories => Set<MemberStatisticHistoryEntity>();
    public DbSet<MemberStatisticDelta> MemberStatisticDeltas => Set<MemberStatisticDelta>();
    public DbSet<MemberProfileNotificationTracking> MemberNotifications => Set<MemberProfileNotificationTracking>();
    public DbSet<MemberFcmToken> MemberFcmTokens => Set<MemberFcmToken>();

    public DbSet<MemberProfilesWallet> Wallets => Set<MemberProfilesWallet>();
    public DbSet<MemberProfilesWalletHistory> WalletHistories => Set<MemberProfilesWalletHistory>();
    public DbSet<MemberWalletApiLog> WalletApiLogs => Set<MemberWalletApiLog>();
    public DbSet<PaymentGatewayInfo> PaymentGateways => Set<PaymentGatewayInfo>();
    public DbSet<MemberCreditCard> CreditCards => Set<MemberCreditCard>();

    public DbSet<MembershipLevel> MembershipLevels => Set<MembershipLevel>();
    public DbSet<MembershipLevelBenefit> MembershipLevelBenefits => Set<MembershipLevelBenefit>();
    public DbSet<MembershipSubscription> MembershipSubscriptions => Set<MembershipSubscription>();

    public DbSet<RankDefinition> RankDefinitions => Set<RankDefinition>();
    public DbSet<RankRequirement> RankRequirements => Set<RankRequirement>();
    public DbSet<MemberRankHistory> MemberRankHistories => Set<MemberRankHistory>();
    public DbSet<RankEvaluationQueue> RankEvaluationQueue => Set<RankEvaluationQueue>();

    public DbSet<GenealogyEntity> GenealogyTree => Set<GenealogyEntity>();
    public DbSet<DualTeamEntity> DualTeamTree => Set<DualTeamEntity>();
    public DbSet<DualTeamLegFrontier> DualTeamLegFrontiers => Set<DualTeamLegFrontier>();
    public DbSet<PlacementLog> PlacementLogs => Set<PlacementLog>();
    public DbSet<GhostPointEntity> GhostPoints => Set<GhostPointEntity>();

    public DbSet<CommissionCategory> CommissionCategories => Set<CommissionCategory>();
    public DbSet<CommissionType> CommissionTypes => Set<CommissionType>();
    public DbSet<CommissionEarning> CommissionEarnings => Set<CommissionEarning>();
    public DbSet<MemberCommissionCountDown> CommissionCountDowns => Set<MemberCommissionCountDown>();
    public DbSet<MemberCommissionCountDownHistory> CommissionCountDownHistories => Set<MemberCommissionCountDownHistory>();
    public DbSet<DailyResidualEarning> DailyResidualEarnings => Set<DailyResidualEarning>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCommission> ProductCommissions => Set<ProductCommission>();
    public DbSet<ProductCommissionPromo> ProductCommissionPromos => Set<ProductCommissionPromo>();
    public DbSet<ProductLoyaltyPointsSetting> ProductLoyaltySettings => Set<ProductLoyaltyPointsSetting>();
    public DbSet<Orders> Orders => Set<Orders>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<PaymentHistory> PaymentHistories => Set<PaymentHistory>();

    public DbSet<LoyaltyPoints> LoyaltyPoints => Set<LoyaltyPoints>();

    public DbSet<TokenType> TokenTypes => Set<TokenType>();
    public DbSet<TokenTypeCommission> TokenTypeCommissions => Set<TokenTypeCommission>();
    public DbSet<TokenTypeProduct> TokenTypeProducts => Set<TokenTypeProduct>();
    public DbSet<TokenBalance> TokenBalances => Set<TokenBalance>();
    public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();

    public DbSet<CorporateEvent> CorporateEvents => Set<CorporateEvent>();
    public DbSet<CorporatePromo> CorporatePromos => Set<CorporatePromo>();
    public DbSet<CorporateContest> CorporateContests => Set<CorporateContest>();
    public DbSet<CorporateContestTranslation> CorporateContestTranslations => Set<CorporateContestTranslation>();
    public DbSet<CorporateContestEarning> CorporateContestEarnings => Set<CorporateContestEarning>();

    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailTemplateLocalization> EmailTemplateLocalizations => Set<EmailTemplateLocalization>();
    public DbSet<EmailTemplateVariable> EmailTemplateVariables => Set<EmailTemplateVariable>();

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
    public DbSet<TicketSequence> TicketSequences => Set<TicketSequence>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaBreach> SlaBreaches => Set<SlaBreach>();
    public DbSet<CannedResponse> CannedResponses => Set<CannedResponse>();
    public DbSet<KnowledgeBaseArticle> KbArticles => Set<KnowledgeBaseArticle>();
    public DbSet<KbArticleVersion> KbArticleVersions => Set<KbArticleVersion>();
    public DbSet<SupportTeam> SupportTeams => Set<SupportTeam>();
    public DbSet<SupportAgent> SupportAgents => Set<SupportAgent>();
    public DbSet<TicketMetricsDaily> TicketMetrics => Set<TicketMetricsDaily>();

    public DbSet<DocumentType>      DocumentTypes      => Set<DocumentType>();
    public DbSet<MarketingDocument> MarketingDocuments => Set<MarketingDocument>();
    public DbSet<S3StorageConfig>   S3StorageConfigs   => Set<S3StorageConfig>();

    // ── Recurring Billing Engine ──────────────────────────────────────────
    public DbSet<RecurringBillingPlan>        RecurringBillingPlans        => Set<RecurringBillingPlan>();
    public DbSet<RecurringBillingPlanProduct> RecurringBillingPlanProducts => Set<RecurringBillingPlanProduct>();
    public DbSet<SubscriptionBillingState>    SubscriptionBillingStates    => Set<SubscriptionBillingState>();
    public DbSet<RecurringBillingAttempt>     RecurringBillingAttempts     => Set<RecurringBillingAttempt>();

    // ── High-Volume Recurring Billing Pipeline (§10) ──────────────────────
    public DbSet<RecurringBillingBatch>       RecurringBillingBatches      => Set<RecurringBillingBatch>();
    public DbSet<RecurringBillingBatchShard>  RecurringBillingBatchShards  => Set<RecurringBillingBatchShard>();
    public DbSet<PointDeltaEvent>             PointDeltaEvents             => Set<PointDeltaEvent>();
    public DbSet<CommissionTriggerQueue>      CommissionTriggerQueues      => Set<CommissionTriggerQueue>();

    // ── Global Parameters ─────────────────────────────────────────────────
    public DbSet<GlobalParameter> GlobalParameters => Set<GlobalParameter>();

    // ── Payout Gateway + Audit ────────────────────────────────────────────
    // Payout gateway config (active flag + minimum payout + fees) lives on the single
    // PaymentGatewayInfo catalog (DbSet PaymentGateways); the old per-WalletType
    // PayoutGatewaySetting table was retired in favor of that one source of truth.
    public DbSet<PayoutAttempt>         PayoutAttempts        => Set<PayoutAttempt>();
    public DbSet<PayoutAttemptEarning>  PayoutAttemptEarnings => Set<PayoutAttemptEarning>();
    public DbSet<PayoutBatch>           PayoutBatches         => Set<PayoutBatch>();

    // ── Billing Gateway Routing Engine ────────────────────────────────────
    public DbSet<PaymentGatewayCatalog>  GatewayCatalog        => Set<PaymentGatewayCatalog>();
    public DbSet<CountryGroup>           CountryGroups         => Set<CountryGroup>();
    public DbSet<CountryGroupCountry>    CountryGroupCountries => Set<CountryGroupCountry>();
    public DbSet<GatewayRoutingRule>     GatewayRoutingRules   => Set<GatewayRoutingRule>();
    public DbSet<GatewayRoutingRuleSplit> GatewayRoutingRuleSplits => Set<GatewayRoutingRuleSplit>();
    public DbSet<CurrencyPolicy>         CurrencyPolicies      => Set<CurrencyPolicy>();
    public DbSet<GatewayFallbackRule>    GatewayFallbackRules  => Set<GatewayFallbackRule>();
    public DbSet<ApiCredential>          ApiCredentials        => Set<ApiCredential>();
    public DbSet<ExchangeRateSnapshot>   ExchangeRateSnapshots => Set<ExchangeRateSnapshot>();
    public DbSet<GatewayRoutingCounter>  GatewayRoutingCounters => Set<GatewayRoutingCounter>();
    public DbSet<GatewayChargeAttempt>   GatewayChargeAttempts => Set<GatewayChargeAttempt>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new AuditInterceptor());
    }
}
