using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditTracking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTracking", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionOperationType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionOperationType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorporateEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorporatePromos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BannerUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporatePromos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditCards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaskedCardNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Last4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    First6 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardBrand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiryMonth = table.Column<int>(type: "int", nullable: false),
                    ExpiryYear = table.Column<int>(type: "int", nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GatewayToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsExpired = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DualTeamTree",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParentMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Side = table.Column<int>(type: "int", nullable: false),
                    HierarchyPath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LeftLegPoints = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RightLegPoints = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DualTeamTree", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodeSection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullException = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InnerException = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserFriendlyMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GenealogyTree",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParentMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HierarchyPath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenealogyTree", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GhostPoints",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LegMemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GhostPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberIdentificationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberIdentificationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RenewalPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberStatistics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonalPoints = table.Column<int>(type: "int", nullable: false),
                    ExternalCustomerPoints = table.Column<int>(type: "int", nullable: false),
                    DualTeamSize = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamSize = table.Column<int>(type: "int", nullable: false),
                    DualTeamPoints = table.Column<int>(type: "int", nullable: false),
                    EnrollmentPoints = table.Column<int>(type: "int", nullable: false),
                    QualifiedSponsoredMembers = table.Column<int>(type: "int", nullable: false),
                    QualifiedSponsoredExternalCustomers = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamGrowth = table.Column<int>(type: "int", nullable: false),
                    DualteamGrowth = table.Column<int>(type: "int", nullable: false),
                    EnrollmentTeamPointsGrowth = table.Column<int>(type: "int", nullable: false),
                    DualTeamPointsGrowth = table.Column<int>(type: "int", nullable: false),
                    CurrentWeekIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentMonthIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentYearIncomeGrowth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MembershipSubscriptionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckoutScreenshotUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlacementLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlacedUnderMemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnplacementCount = table.Column<int>(type: "int", nullable: false),
                    FirstPlacementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductLoyaltySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsPerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiredSuccessfulPayments = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLoyaltySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CertificateTemplateUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    DistributedToMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsedByMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeneratedPdfUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsGuestPass = table.Column<bool>(type: "bit", nullable: false),
                    TemplateUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalletHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WalletType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AccountIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    eWalletPasswordEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionCategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentDelayDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRealTime = table.Column<bool>(type: "bit", nullable: false),
                    IsPaidOnSignup = table.Column<bool>(type: "bit", nullable: false),
                    IsPaidOnRenewal = table.Column<bool>(type: "bit", nullable: false),
                    Cummulative = table.Column<bool>(type: "bit", nullable: false),
                    TriggerOrder = table.Column<int>(type: "int", nullable: false),
                    NewMembers = table.Column<int>(type: "int", nullable: false),
                    DaysAfterJoining = table.Column<int>(type: "int", nullable: false),
                    MembersRebill = table.Column<int>(type: "int", nullable: false),
                    LifeTimeRank = table.Column<int>(type: "int", nullable: false),
                    CurrentRank = table.Column<int>(type: "int", nullable: false),
                    LevelNo = table.Column<int>(type: "int", nullable: false),
                    ResidualBased = table.Column<bool>(type: "bit", nullable: false),
                    ResidualOverCommissionType = table.Column<int>(type: "int", nullable: false),
                    ResidualPercentage = table.Column<double>(type: "float", nullable: false),
                    PersonalPoints = table.Column<int>(type: "int", nullable: false),
                    TeamPoints = table.Column<int>(type: "int", nullable: false),
                    MaxTeamPointsPerBranch = table.Column<double>(type: "float", nullable: false),
                    EnrollmentTeam = table.Column<int>(type: "int", nullable: false),
                    MaxEnrollmentTeamPointsPerBranch = table.Column<double>(type: "float", nullable: false),
                    ExternalMembers = table.Column<int>(type: "int", nullable: false),
                    SponsoredMembers = table.Column<int>(type: "int", nullable: false),
                    IsSponsorBonus = table.Column<bool>(type: "bit", nullable: false),
                    ReverseId = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionTypes_CommissionCategories_CommissionCategoryId",
                        column: x => x.CommissionCategoryId,
                        principalTable: "CommissionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipLevelBenefits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MembershipLevelId = table.Column<int>(type: "int", nullable: false),
                    BenefitName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BenefitDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BenefitValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipLevelBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipLevelBenefits_MembershipLevels_MembershipLevelId",
                        column: x => x.MembershipLevelId,
                        principalTable: "MembershipLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyFee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SetupFee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Price90Days = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Price180Days = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DescriptionPromo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MonthlyFeePromo = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SetupFeePromo = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ImageUrlPromo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QualificationPoinsPromo = table.Column<int>(type: "int", nullable: false),
                    QualificationPoins = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OldSystemProductId = table.Column<int>(type: "int", nullable: false),
                    MembershipLevelId = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_MembershipLevels_MembershipLevelId",
                        column: x => x.MembershipLevelId,
                        principalTable: "MembershipLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrdersId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderDetails_Orders_OrdersId",
                        column: x => x.OrdersId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GatewayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GatewayTransactionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionStatus = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrdersId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentHistories_Orders_OrdersId",
                        column: x => x.OrdersId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MemberRankHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RankDefinitionId = table.Column<int>(type: "int", nullable: false),
                    PreviousRankId = table.Column<int>(type: "int", nullable: true),
                    AchievedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedCertificateUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRankHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberRankHistories_RankDefinitions_RankDefinitionId",
                        column: x => x.RankDefinitionId,
                        principalTable: "RankDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RankRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RankDefinitionId = table.Column<int>(type: "int", nullable: false),
                    LevelNo = table.Column<int>(type: "int", nullable: false),
                    PersonalPoints = table.Column<int>(type: "int", nullable: false),
                    TeamPoints = table.Column<int>(type: "int", nullable: false),
                    MaxTeamPointsPerBranch = table.Column<double>(type: "float", nullable: false),
                    EnrollmentTeam = table.Column<int>(type: "int", nullable: false),
                    PlacementQualifiedTeamMembers = table.Column<int>(type: "int", nullable: false),
                    EnrollmentQualifiedTeamMembers = table.Column<int>(type: "int", nullable: false),
                    MaxEnrollmentTeamPointsPerBranch = table.Column<double>(type: "float", nullable: false),
                    ExternalMembers = table.Column<int>(type: "int", nullable: false),
                    SponsoredMembers = table.Column<int>(type: "int", nullable: false),
                    SalesVolume = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RankBonus = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DailyBonus = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MonthlyBonus = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LifetimeHoldingDuration = table.Column<int>(type: "int", nullable: false),
                    RankDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CurrentRankDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AchievementMessage = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    CertificateUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankRequirements_RankDefinitions_RankDefinitionId",
                        column: x => x.RankDefinitionId,
                        principalTable: "RankDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MergedIntoTicketId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionEarnings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BeneficiaryMemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceOrderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CommissionTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EarnedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommissionOperationTypeId = table.Column<int>(type: "int", nullable: true),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    CsvImportBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionEarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionEarnings_CommissionOperationType_CommissionOperationTypeId",
                        column: x => x.CommissionOperationTypeId,
                        principalTable: "CommissionOperationType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommissionEarnings_CommissionTypes_CommissionTypeId",
                        column: x => x.CommissionTypeId,
                        principalTable: "CommissionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCommissionPromos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId1 = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CorporatePromoId = table.Column<long>(type: "bigint", nullable: false),
                    CorporatePromoId1 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TriggerSponsorBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerSponsorBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerFastStartBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBoostBonus = table.Column<bool>(type: "bit", nullable: false),
                    CarBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    PresidentialBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    EligibleMembershipResidual = table.Column<bool>(type: "bit", nullable: false),
                    EligibleDailyResidual = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCommissionPromos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCommissionPromos_CorporatePromos_CorporatePromoId1",
                        column: x => x.CorporatePromoId1,
                        principalTable: "CorporatePromos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductCommissionPromos_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId1 = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TriggerSponsorBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerSponsorBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBuilderBonusTurbo = table.Column<bool>(type: "bit", nullable: false),
                    TriggerFastStartBonus = table.Column<bool>(type: "bit", nullable: false),
                    TriggerBoostBonus = table.Column<bool>(type: "bit", nullable: false),
                    CarBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    PresidentialBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    EligibleMembershipResidual = table.Column<bool>(type: "bit", nullable: false),
                    EligibleDailyResidual = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCommissions_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAttachments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketComments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketComments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionCountDownHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountDownId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId1 = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FastStartBonus1Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus1End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus2Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus2End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus3Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus3End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionCountDownHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionCountDowns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId1 = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FastStartBonus1Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus1End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus2Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus2End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus3Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FastStartBonus3End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionCountDowns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPoints",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsEarned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    MissedPayment = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfSuccessPayments = table.Column<int>(type: "int", nullable: false),
                    MonthNo = table.Column<int>(type: "int", nullable: false),
                    YearNo = table.Column<int>(type: "int", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MemberProfileId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirebaseMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MemberProfileId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowBusinessName = table.Column<bool>(type: "bit", nullable: false),
                    MemberType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnrollDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SponsorMemberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplicateSiteSlug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProfilePhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsNamePublic = table.Column<bool>(type: "bit", nullable: false),
                    IsEmailPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsPhonePublic = table.Column<bool>(type: "bit", nullable: false),
                    ActiveMembershipId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EnrollmentNodeId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BinaryNodeId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberProfiles", x => x.Id);
                    table.UniqueConstraint("AK_MemberProfiles_MemberId", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_MemberProfiles_DualTeamTree_BinaryNodeId",
                        column: x => x.BinaryNodeId,
                        principalTable: "DualTeamTree",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MemberProfiles_GenealogyTree_EnrollmentNodeId",
                        column: x => x.EnrollmentNodeId,
                        principalTable: "GenealogyTree",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MembershipSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    MembershipLevelId = table.Column<int>(type: "int", nullable: false),
                    PreviousMembershipLevelId = table.Column<int>(type: "int", nullable: true),
                    ChangeReason = table.Column<int>(type: "int", nullable: false),
                    SubscriptionStatus = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoldDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    LastOrderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastOrderId1 = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                        column: x => x.MemberId,
                        principalTable: "MemberProfiles",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                        column: x => x.MembershipLevelId,
                        principalTable: "MembershipLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipSubscriptions_Orders_LastOrderId1",
                        column: x => x.LastOrderId1,
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MemberStatusHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MemberProfileId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberStatusHistories_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TokenBalances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<int>(type: "int", nullable: false),
                    MemberProfileId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenBalances_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "CommissionCategories",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "LastUpdateBy", "LastUpdateDate", "Name" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "One-time bonuses triggered on new member signup.", true, null, null, "Signup Bonuses" },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Recurring commissions calculated on binary team volume.", true, null, null, "Residual Commissions" },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Bonuses awarded for reaching leadership thresholds.", true, null, null, "Leadership Bonuses" },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Negative-amount entries that reverse previously paid commissions.", true, null, null, "Reversals" }
                });

            migrationBuilder.InsertData(
                table: "ErrorMessages",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "ErrorCode", "IsActive", "Language", "LastUpdateBy", "LastUpdateDate", "UserFriendlyMessage" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "DEFAULT", true, "en", null, null, "Something went wrong. Please try again or contact support." },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "DEFAULT", true, "es", null, null, "Algo salió mal. Intente de nuevo o comuníquese con soporte." },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "INTERNAL_ERROR", true, "en", null, null, "An unexpected error occurred. Please try again later." },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "INTERNAL_ERROR", true, "es", null, null, "Ocurrió un error inesperado. Por favor intente más tarde." },
                    { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "ORDER_NOT_FOUND", true, "en", null, null, "The requested order could not be found." },
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "ORDER_NOT_FOUND", true, "es", null, null, "No se encontró la orden solicitada." },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBER_NOT_FOUND", true, "en", null, null, "The member account could not be found." },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBER_NOT_FOUND", true, "es", null, null, "No se encontró la cuenta del miembro." },
                    { 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBER_ALREADY_EXISTS", true, "en", null, null, "An account with this information already exists." },
                    { 10, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBER_ALREADY_EXISTS", true, "es", null, null, "Ya existe una cuenta con esta información." },
                    { 11, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "SPONSOR_NOT_FOUND", true, "en", null, null, "The sponsor ID you entered could not be found. Please verify and try again." },
                    { 12, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "SPONSOR_NOT_FOUND", true, "es", null, null, "El ID de patrocinador ingresado no fue encontrado. Verifíquelo e intente de nuevo." },
                    { 13, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "DUPLICATE_REPLICATE_SITE", true, "en", null, null, "This website address is already taken. Please choose a different one." },
                    { 14, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "DUPLICATE_REPLICATE_SITE", true, "es", null, null, "Esta dirección de sitio web ya está en uso. Por favor elija una diferente." },
                    { 15, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_LEVEL_NOT_FOUND", true, "en", null, null, "The selected membership plan is not available." },
                    { 16, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_LEVEL_NOT_FOUND", true, "es", null, null, "El plan de membresía seleccionado no está disponible." },
                    { 17, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PRODUCT_NOT_FOUND", true, "en", null, null, "One or more of the selected products are not available." },
                    { 18, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PRODUCT_NOT_FOUND", true, "es", null, null, "Uno o más de los productos seleccionados no están disponibles." },
                    { 19, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MINIMUM_AGE_REQUIRED", true, "en", null, null, "You must be at least 18 years old to register." },
                    { 20, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MINIMUM_AGE_REQUIRED", true, "es", null, null, "Debes tener al menos 18 años para registrarte." },
                    { 21, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PLACEMENT_WINDOW_EXPIRED", true, "en", null, null, "The 30-day placement window has expired for this member." },
                    { 22, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PLACEMENT_WINDOW_EXPIRED", true, "es", null, null, "El período de 30 días para colocar a este miembro ha expirado." },
                    { 23, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNPLACEMENT_LIMIT_EXCEEDED", true, "en", null, null, "The maximum number of placement changes for this member has been reached." },
                    { 24, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNPLACEMENT_LIMIT_EXCEEDED", true, "es", null, null, "Se alcanzó el límite máximo de cambios de posición para este miembro." },
                    { 25, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNPLACEMENT_WINDOW_EXPIRED", true, "en", null, null, "The 72-hour unplacement window has expired." },
                    { 26, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNPLACEMENT_WINDOW_EXPIRED", true, "es", null, null, "El período de 72 horas para retirar la posición ha expirado." },
                    { 27, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_CHANGE_NOT_ALLOWED", true, "en", null, null, "This membership change is not permitted at this time." },
                    { 28, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_CHANGE_NOT_ALLOWED", true, "es", null, null, "Este cambio de membresía no está permitido en este momento." },
                    { 29, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_NOT_FOUND", true, "en", null, null, "No active membership was found for this account." },
                    { 30, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "MEMBERSHIP_NOT_FOUND", true, "es", null, null, "No se encontró una membresía activa para esta cuenta." },
                    { 31, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "NO_SPONSOR_BONUS_TYPE", true, "en", null, null, "The system could not process the bonus at this time. Please contact support." },
                    { 32, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "NO_SPONSOR_BONUS_TYPE", true, "es", null, null, "El sistema no pudo procesar el bono en este momento. Contacte soporte." },
                    { 33, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "NO_REVERSE_TYPE", true, "en", null, null, "The reversal could not be processed. Please contact support." },
                    { 34, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "NO_REVERSE_TYPE", true, "es", null, null, "No se pudo procesar el reverso. Por favor contacte soporte." },
                    { 35, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "REVERSE_TYPE_NOT_FOUND", true, "en", null, null, "The reversal could not be processed. Please contact support." },
                    { 36, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "REVERSE_TYPE_NOT_FOUND", true, "es", null, null, "No se pudo procesar el reverso. Por favor contacte soporte." },
                    { 37, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "COMMISSION_PERIOD_NOT_FOUND", true, "en", null, null, "Commission data for the requested period could not be found." },
                    { 38, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "COMMISSION_PERIOD_NOT_FOUND", true, "es", null, null, "No se encontraron datos de comisión para el período solicitado." },
                    { 39, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "INSUFFICIENT_TOKEN_BALANCE", true, "en", null, null, "You do not have enough tokens to complete this action." },
                    { 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "INSUFFICIENT_TOKEN_BALANCE", true, "es", null, null, "No tienes suficientes tokens para completar esta acción." },
                    { 41, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "WALLET_NOT_FOUND", true, "en", null, null, "No payment method was found for this account." },
                    { 42, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "WALLET_NOT_FOUND", true, "es", null, null, "No se encontró un método de pago para esta cuenta." },
                    { 43, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "WALLET_PASSWORD_NOT_ENCRYPTED", true, "en", null, null, "A security error occurred. Please contact support immediately." },
                    { 44, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "WALLET_PASSWORD_NOT_ENCRYPTED", true, "es", null, null, "Ocurrió un error de seguridad. Contacte soporte inmediatamente." },
                    { 45, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "RANK_NOT_FOUND", true, "en", null, null, "The requested rank information could not be found." },
                    { 46, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "RANK_NOT_FOUND", true, "es", null, null, "No se encontró la información del rango solicitado." },
                    { 47, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PAYMENT_FAILED", true, "en", null, null, "Your payment could not be processed. Please verify your payment details." },
                    { 48, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "PAYMENT_FAILED", true, "es", null, null, "No se pudo procesar tu pago. Por favor verifica tus datos de pago." },
                    { 49, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "REFUND_FAILED", true, "en", null, null, "The refund could not be processed at this time. Please contact support." },
                    { 50, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "REFUND_FAILED", true, "es", null, null, "No se pudo procesar el reembolso en este momento. Contacte soporte." },
                    { 51, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNAUTHORIZED", true, "en", null, null, "You are not authorized to perform this action." },
                    { 52, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "UNAUTHORIZED", true, "es", null, null, "No tienes autorización para realizar esta acción." },
                    { 53, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "VALIDATION_ERROR", true, "en", null, null, "The information you provided is invalid. Please review and try again." },
                    { 54, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "VALIDATION_ERROR", true, "es", null, null, "La información proporcionada no es válida. Por favor revísela e intente de nuevo." }
                });

            migrationBuilder.InsertData(
                table: "MembershipLevels",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "IsAutoRenew", "IsFree", "LastUpdateBy", "LastUpdateDate", "Name", "Price", "RenewalPrice", "SortOrder" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Customer account. No team-building access. No commission eligibility.", true, false, true, null, null, "External Member", 0m, 0m, 1 },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Entry-level ambassador. Qualifies for Sponsor Bonus and Fast Start Bonus.", true, true, false, null, null, "Ambassador – Basic", 99m, 79m, 2 },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Mid-tier ambassador. Qualifies for Daily Residual and Boost Bonus.", true, true, false, null, null, "Ambassador – Advanced", 199m, 169m, 3 },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Top-tier ambassador. Qualifies for all bonuses including Presidential and Matching.", true, true, false, null, null, "Ambassador – Premium", 399m, 349m, 4 }
                });

            migrationBuilder.InsertData(
                table: "RankDefinitions",
                columns: new[] { "Id", "CertificateTemplateUrl", "CreatedBy", "CreationDate", "Description", "LastUpdateBy", "LastUpdateDate", "Name", "SortOrder", "Status" },
                values: new object[,]
                {
                    { 1, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Member", 1, 1 },
                    { 2, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Bronze", 2, 1 },
                    { 3, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Silver", 3, 1 },
                    { 4, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Gold", 4, 1 },
                    { 5, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Platinum", 5, 1 },
                    { 6, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Diamond", 6, 1 },
                    { 7, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Presidential", 7, 1 }
                });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "IsActive", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 1, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "One-time bonus to the direct sponsor when a new ambassador or member signs up.", 0, 0, true, false, true, true, true, null, null, 0, 0, 0.5, 0.5, 0, "Sponsor Bonus", 0, 14, 10m, 0, false, 0, 0.0, 10, 0, 0, 0 },
                    { 2, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 30, "FSB for personal signups in days 1–30 after the ambassador's own enrollment.", 0, 0, true, false, true, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 1", 0, 7, 50m, 0, false, 0, 0.0, 11, 0, 0, 1 },
                    { 3, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 60, "FSB for personal signups in days 31–60 after the ambassador's own enrollment.", 0, 0, true, false, true, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 2", 0, 7, 30m, 0, false, 0, 0.0, 11, 0, 0, 2 },
                    { 4, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 90, "FSB for personal signups in days 61–90 after the ambassador's own enrollment.", 0, 0, true, false, true, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 3", 0, 7, 20m, 0, false, 0, 0.0, 11, 0, 0, 3 },
                    { 5, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Nightly binary team volume commission. Calculated from MemberStatisticEntity.DualTeamPoints.", 0, 0, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Daily Residual – Binary", 0, 0, 10m, 0, true, 0, 0.0, 0, 0, 300, 0 },
                    { 6, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Weekly bonus for ambassadors reaching the Gold threshold.", 0, 0, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Boost Bonus – Gold", 0, 3, 5m, 0, false, 0, 0.0, 0, 0, 1000, 0 },
                    { 7, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Weekly bonus for ambassadors reaching the Platinum threshold.", 0, 0, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Boost Bonus – Platinum", 0, 3, 8m, 0, false, 0, 0.0, 0, 0, 3000, 0 },
                    { 8, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Monthly bonus calculated on total organizational volume for Presidential-rank ambassadors.", 0, 0, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Presidential Bonus", 0, 7, 3m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 9, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Percentage of direct downline daily residual earnings, paid to the upline ambassador.", 0, 0, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Matching Bonus", 0, 0, 20m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 10, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Negative-amount reversal of the Sponsor Bonus when a signup cancels within 14 days.", 0, 0, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Sponsor Bonus Reversal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 11, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Negative-amount reversal of any Fast Start Bonus window when a signup cancels.", 0, 0, true, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Fast Start Bonus Reversal", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionCountDownHistories_CountDownId",
                table: "CommissionCountDownHistories",
                column: "CountDownId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionCountDownHistories_MemberId1",
                table: "CommissionCountDownHistories",
                column: "MemberId1");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionCountDowns_MemberId1",
                table: "CommissionCountDowns",
                column: "MemberId1");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_BeneficiaryMemberId_Status",
                table: "CommissionEarnings",
                columns: new[] { "BeneficiaryMemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_CommissionOperationTypeId",
                table: "CommissionEarnings",
                column: "CommissionOperationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_CommissionTypeId",
                table: "CommissionEarnings",
                column: "CommissionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_PeriodDate",
                table: "CommissionEarnings",
                column: "PeriodDate");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_SourceOrderId_CommissionTypeId",
                table: "CommissionEarnings",
                columns: new[] { "SourceOrderId", "CommissionTypeId" },
                unique: true,
                filter: "[SourceOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTypes_CommissionCategoryId",
                table: "CommissionTypes",
                column: "CommissionCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_HierarchyPath",
                table: "DualTeamTree",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_DualTeamTree_MemberId",
                table: "DualTeamTree",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorMessages_ErrorCode_Language",
                table: "ErrorMessages",
                columns: new[] { "ErrorCode", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_HierarchyPath",
                table: "GenealogyTree",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogyTree_MemberId_CreationDate",
                table: "GenealogyTree",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GhostPoints_LegMemberId",
                table: "GhostPoints",
                column: "LegMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_GhostPoints_MemberId",
                table: "GhostPoints",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_MemberProfileId",
                table: "LoyaltyPoints",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberNotifications_MemberProfileId",
                table: "MemberNotifications",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_ActiveMembershipId",
                table: "MemberProfiles",
                column: "ActiveMembershipId",
                unique: true,
                filter: "[ActiveMembershipId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_BinaryNodeId",
                table: "MemberProfiles",
                column: "BinaryNodeId",
                unique: true,
                filter: "[BinaryNodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_EnrollmentNodeId",
                table: "MemberProfiles",
                column: "EnrollmentNodeId",
                unique: true,
                filter: "[EnrollmentNodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_MemberId",
                table: "MemberProfiles",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_ReplicateSiteSlug",
                table: "MemberProfiles",
                column: "ReplicateSiteSlug",
                unique: true,
                filter: "[ReplicateSiteSlug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_UserId",
                table: "MemberProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberRankHistories_RankDefinitionId",
                table: "MemberRankHistories",
                column: "RankDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipLevelBenefits_MembershipLevelId",
                table: "MembershipLevelBenefits",
                column: "MembershipLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_LastOrderId1",
                table: "MembershipSubscriptions",
                column: "LastOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_MemberId",
                table: "MembershipSubscriptions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_MembershipLevelId",
                table: "MembershipSubscriptions",
                column: "MembershipLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberStatusHistories_MemberProfileId",
                table: "MemberStatusHistories",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrdersId",
                table: "OrderDetails",
                column: "OrdersId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_OrdersId",
                table: "PaymentHistories",
                column: "OrdersId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissionPromos_CorporatePromoId1",
                table: "ProductCommissionPromos",
                column: "CorporatePromoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissionPromos_ProductId1",
                table: "ProductCommissionPromos",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissions_ProductId1",
                table: "ProductCommissions",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MembershipLevelId",
                table: "Products",
                column: "MembershipLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RankRequirements_RankDefinitionId",
                table: "RankRequirements",
                column: "RankDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_TicketId",
                table: "TicketAttachments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_TicketId",
                table: "TicketComments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_MemberProfileId",
                table: "TokenBalances",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_MemberId_CreationDate",
                table: "TokenTransactions",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionCountDownHistories_CommissionCountDowns_CountDownId",
                table: "CommissionCountDownHistories",
                column: "CountDownId",
                principalTable: "CommissionCountDowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionCountDownHistories_MemberProfiles_MemberId1",
                table: "CommissionCountDownHistories",
                column: "MemberId1",
                principalTable: "MemberProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CommissionCountDowns_MemberProfiles_MemberId1",
                table: "CommissionCountDowns",
                column: "MemberId1",
                principalTable: "MemberProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyPoints_MemberProfiles_MemberProfileId",
                table: "LoyaltyPoints",
                column: "MemberProfileId",
                principalTable: "MemberProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberNotifications_MemberProfiles_MemberProfileId",
                table: "MemberNotifications",
                column: "MemberProfileId",
                principalTable: "MemberProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberProfiles_MembershipSubscriptions_ActiveMembershipId",
                table: "MemberProfiles",
                column: "ActiveMembershipId",
                principalTable: "MembershipSubscriptions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropTable(
                name: "AuditTracking");

            migrationBuilder.DropTable(
                name: "CommissionCountDownHistories");

            migrationBuilder.DropTable(
                name: "CommissionEarnings");

            migrationBuilder.DropTable(
                name: "CorporateEvents");

            migrationBuilder.DropTable(
                name: "CreditCards");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "ErrorMessages");

            migrationBuilder.DropTable(
                name: "GhostPoints");

            migrationBuilder.DropTable(
                name: "LoyaltyPoints");

            migrationBuilder.DropTable(
                name: "MemberIdentificationTypes");

            migrationBuilder.DropTable(
                name: "MemberNotifications");

            migrationBuilder.DropTable(
                name: "MemberRankHistories");

            migrationBuilder.DropTable(
                name: "MembershipLevelBenefits");

            migrationBuilder.DropTable(
                name: "MemberStatistics");

            migrationBuilder.DropTable(
                name: "MemberStatusHistories");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "PaymentHistories");

            migrationBuilder.DropTable(
                name: "PlacementLogs");

            migrationBuilder.DropTable(
                name: "ProductCommissionPromos");

            migrationBuilder.DropTable(
                name: "ProductCommissions");

            migrationBuilder.DropTable(
                name: "ProductLoyaltySettings");

            migrationBuilder.DropTable(
                name: "RankRequirements");

            migrationBuilder.DropTable(
                name: "TicketAttachments");

            migrationBuilder.DropTable(
                name: "TicketComments");

            migrationBuilder.DropTable(
                name: "TokenBalances");

            migrationBuilder.DropTable(
                name: "TokenTransactions");

            migrationBuilder.DropTable(
                name: "TokenTypes");

            migrationBuilder.DropTable(
                name: "WalletHistories");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "CommissionCountDowns");

            migrationBuilder.DropTable(
                name: "CommissionOperationType");

            migrationBuilder.DropTable(
                name: "CommissionTypes");

            migrationBuilder.DropTable(
                name: "CorporatePromos");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "RankDefinitions");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "CommissionCategories");

            migrationBuilder.DropTable(
                name: "TicketCategories");

            migrationBuilder.DropTable(
                name: "MemberProfiles");

            migrationBuilder.DropTable(
                name: "DualTeamTree");

            migrationBuilder.DropTable(
                name: "GenealogyTree");

            migrationBuilder.DropTable(
                name: "MembershipSubscriptions");

            migrationBuilder.DropTable(
                name: "MembershipLevels");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
