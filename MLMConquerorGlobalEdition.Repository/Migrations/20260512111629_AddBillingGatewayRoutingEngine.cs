using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingGatewayRoutingEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApiKeyEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SecretKeyEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MerchantIdEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdditionalSecretEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PresentmentCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MarkupPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Processor = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SupportsRefund = table.Column<bool>(type: "bit", nullable: false),
                    SupportsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayChargeAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteBucketKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CardProcessor = table.Column<int>(type: "int", nullable: false),
                    FallbackStepIndex = table.Column<int>(type: "int", nullable: false),
                    PresentmentCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OriginalAmountUsd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ConvertedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExchangeRateUsed = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayTransactionId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OperationType = table.Column<int>(type: "int", nullable: false),
                    CardBrand = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayChargeAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayFallbackRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationType = table.Column<int>(type: "int", nullable: false),
                    PrimaryProcessor = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    NextProcessor = table.Column<int>(type: "int", nullable: false),
                    DelayMinutes = table.Column<int>(type: "int", nullable: false),
                    ForceUsdOnFallback = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayFallbackRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayRoutingCounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteBucketKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CardProcessor = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<long>(type: "bigint", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayRoutingCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryGroupCountries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryGroupId = table.Column<int>(type: "int", nullable: false),
                    IsoCountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryGroupCountries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryGroupCountries_CountryGroups_CountryGroupId",
                        column: x => x.CountryGroupId,
                        principalTable: "CountryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatewayRoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationType = table.Column<int>(type: "int", nullable: false),
                    CardBrand = table.Column<int>(type: "int", nullable: true),
                    IsoCountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CountryGroupId = table.Column<int>(type: "int", nullable: true),
                    IsCatchAll = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyPolicyId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayRoutingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatewayRoutingRules_CountryGroups_CountryGroupId",
                        column: x => x.CountryGroupId,
                        principalTable: "CountryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GatewayRoutingRules_CurrencyPolicies_CurrencyPolicyId",
                        column: x => x.CurrencyPolicyId,
                        principalTable: "CurrencyPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GatewayRoutingRuleSplits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GatewayRoutingRuleId = table.Column<int>(type: "int", nullable: false),
                    CardProcessor = table.Column<int>(type: "int", nullable: false),
                    WeightPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayRoutingRuleSplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatewayRoutingRuleSplits_GatewayRoutingRules_GatewayRoutingRuleId",
                        column: x => x.GatewayRoutingRuleId,
                        principalTable: "GatewayRoutingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_ServiceKey_Environment",
                table: "ApiCredentials",
                columns: new[] { "ServiceKey", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountryGroupCountries_CountryGroupId_IsoCountryCode",
                table: "CountryGroupCountries",
                columns: new[] { "CountryGroupId", "IsoCountryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountryGroups_Code",
                table: "CountryGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyPolicies_PresentmentCurrency",
                table: "CurrencyPolicies",
                column: "PresentmentCurrency",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateSnapshots_QuoteCurrency_ExpiresAtUtc",
                table: "ExchangeRateSnapshots",
                columns: new[] { "QuoteCurrency", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCatalog_IsActive",
                table: "GatewayCatalog",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCatalog_Processor",
                table: "GatewayCatalog",
                column: "Processor",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayChargeAttempts_AttemptedAtUtc",
                table: "GatewayChargeAttempts",
                column: "AttemptedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayChargeAttempts_MemberId",
                table: "GatewayChargeAttempts",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayChargeAttempts_PaymentHistoryId",
                table: "GatewayChargeAttempts",
                column: "PaymentHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayFallbackRules_OperationType_PrimaryProcessor_StepOrder",
                table: "GatewayFallbackRules",
                columns: new[] { "OperationType", "PrimaryProcessor", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingCounters_RouteBucketKey",
                table: "GatewayRoutingCounters",
                column: "RouteBucketKey");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingCounters_RouteBucketKey_CardProcessor",
                table: "GatewayRoutingCounters",
                columns: new[] { "RouteBucketKey", "CardProcessor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingRules_CountryGroupId",
                table: "GatewayRoutingRules",
                column: "CountryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingRules_CurrencyPolicyId",
                table: "GatewayRoutingRules",
                column: "CurrencyPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingRules_IsActive",
                table: "GatewayRoutingRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingRules_OperationType_CardBrand",
                table: "GatewayRoutingRules",
                columns: new[] { "OperationType", "CardBrand" });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayRoutingRuleSplits_GatewayRoutingRuleId",
                table: "GatewayRoutingRuleSplits",
                column: "GatewayRoutingRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentials");

            migrationBuilder.DropTable(
                name: "CountryGroupCountries");

            migrationBuilder.DropTable(
                name: "ExchangeRateSnapshots");

            migrationBuilder.DropTable(
                name: "GatewayCatalog");

            migrationBuilder.DropTable(
                name: "GatewayChargeAttempts");

            migrationBuilder.DropTable(
                name: "GatewayFallbackRules");

            migrationBuilder.DropTable(
                name: "GatewayRoutingCounters");

            migrationBuilder.DropTable(
                name: "GatewayRoutingRuleSplits");

            migrationBuilder.DropTable(
                name: "GatewayRoutingRules");

            migrationBuilder.DropTable(
                name: "CountryGroups");

            migrationBuilder.DropTable(
                name: "CurrencyPolicies");
        }
    }
}
