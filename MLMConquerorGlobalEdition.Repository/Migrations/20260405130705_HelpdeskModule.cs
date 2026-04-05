using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class HelpdeskModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberProfileId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

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
                name: "MemberFcmTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberFcmTokens", x => x.Id);
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
                    ProductId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PointsPerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
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
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstResponseCriticalMinutes = table.Column<int>(type: "int", nullable: false),
                    FirstResponseHighMinutes = table.Column<int>(type: "int", nullable: false),
                    FirstResponseNormalMinutes = table.Column<int>(type: "int", nullable: false),
                    FirstResponseLowMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionCriticalMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionHighMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionNormalMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionLowMinutes = table.Column<int>(type: "int", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkdaysJson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BusinessHoursStart = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BusinessHoursEnd = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WarningThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    NotifyAgentAtMinutes = table.Column<int>(type: "int", nullable: false),
                    NotifySupervisorAtMinutes = table.Column<int>(type: "int", nullable: false),
                    NotifyManagerAtMinutes = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupervisorAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RoutingMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCreated = table.Column<int>(type: "int", nullable: false),
                    TotalResolved = table.Column<int>(type: "int", nullable: false),
                    TotalClosed = table.Column<int>(type: "int", nullable: false),
                    AvgFirstResponseMinutes = table.Column<double>(type: "float", nullable: false),
                    AvgResolutionMinutes = table.Column<double>(type: "float", nullable: false),
                    FirstContactResolutionRate = table.Column<double>(type: "float", nullable: false),
                    SlaComplianceRate = table.Column<double>(type: "float", nullable: false),
                    CsatAverage = table.Column<double>(type: "float", nullable: false),
                    CsatResponseCount = table.Column<int>(type: "int", nullable: false),
                    FrtBreaches = table.Column<int>(type: "int", nullable: false),
                    ResolutionBreaches = table.Column<int>(type: "int", nullable: false),
                    DeflectionAttempts = table.Column<int>(type: "int", nullable: false),
                    DeflectionSuccesses = table.Column<int>(type: "int", nullable: false),
                    TicketsByPriorityJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TicketsByCategoryJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TicketsByChannelJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TicketsByAgentJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketSequences",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSequences", x => x.Date);
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
                name: "TokenTypeCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    CommissionTypeId = table.Column<int>(type: "int", nullable: false),
                    CommissionPerToken = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
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
                    table.PrimaryKey("PK_TokenTypeCommissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTypeProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenTypeId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    QuantityGranted = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTypeProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsGuestPass = table.Column<bool>(type: "bit", nullable: false),
                    TemplateUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    FixedAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
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
                    IsEnrollmentBased = table.Column<bool>(type: "bit", nullable: false),
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
                    CorporateFee = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    JoinPageMembership = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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
                name: "CannedResponses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CannedResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CannedResponses_SupportTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "SupportTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupportAgents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LanguagesJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaxConcurrentTickets = table.Column<int>(type: "int", nullable: false),
                    CurrentTicketCount = table.Column<int>(type: "int", nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SupportAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportAgents_SupportTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "SupportTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ParentCategoryId = table.Column<int>(type: "int", nullable: true),
                    DefaultTeamId = table.Column<int>(type: "int", nullable: true),
                    DefaultPriority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultSlaPolicyId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId",
                        column: x => x.DefaultSlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketCategories_SupportTeams_DefaultTeamId",
                        column: x => x.DefaultTeamId,
                        principalTable: "SupportTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketCategories_TicketCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    ProductId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                        name: "FK_ProductCommissionPromos_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                        name: "FK_ProductCommissions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KbArticles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    TagsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    AuthorAgentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    HelpfulCount = table.Column<int>(type: "int", nullable: false),
                    NotHelpfulCount = table.Column<int>(type: "int", nullable: false),
                    SourceTicketId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_KbArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbArticles_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AssignedTeamId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MergedIntoTicketId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CustomerTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SlaPolicyId = table.Column<string>(type: "nvarchar(36)", nullable: true),
                    SlaFirstResponseDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaFirstResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaResolutionDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSlaFirstResponseBreached = table.Column<bool>(type: "bit", nullable: false),
                    IsSlaResolutionBreached = table.Column<bool>(type: "bit", nullable: false),
                    IsSlaPaused = table.Column<bool>(type: "bit", nullable: false),
                    SlaPausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaPausedMinutes = table.Column<double>(type: "float", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedByAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CsatScore = table.Column<int>(type: "int", nullable: true),
                    CsatComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CsatSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpSent = table.Column<bool>(type: "bit", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_SupportTeams_AssignedTeamId",
                        column: x => x.AssignedTeamId,
                        principalTable: "SupportTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KbArticleVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    BodySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditedByAgentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbArticleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbArticleVersions_KbArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KbArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaBreaches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SlaPolicyId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricType = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BreachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BreachDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    AssignedAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedTeamId = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaBreaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaBreaches_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    AuthorType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                name: "TicketHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketHistories_Tickets_TicketId",
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
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "One-time bonuses paid to the direct enroller on new member signup (Member Bonus).", true, null, null, "Enrollment Bonuses" },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "3-window FSB paid when the enroller qualifies within each countdown window.", true, null, null, "Fast Start Bonus" },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Fixed daily earnings based on current binary rank qualification.", true, null, null, "Dual Team Residuals" },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).", true, null, null, "Leadership Bonuses" },
                    { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Negative-amount entries that reverse previously paid commissions within the chargeback window.", true, null, null, "Reversals" },
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.", true, null, null, "Builder Bonus" },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.", true, null, null, "Builder Bonus Turbo" },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Administrative fee deductions and token-related deductions applied at payout or on token consumption.", true, null, null, "Deductions" }
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
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Annual business fee for team-building ambassadors. Qualifies for all commissions.", true, true, false, null, null, "Lifestyle Ambassador", 99m, 99m, 1 },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.", true, true, false, null, null, "Travel Advantage – VIP", 40m, 40m, 2 },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.", true, true, false, null, null, "Travel Advantage – Elite", 99m, 99m, 3 },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.", true, true, false, null, null, "Travel Advantage – Turbo", 199m, 199m, 4 }
                });

            migrationBuilder.InsertData(
                table: "ProductLoyaltySettings",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "IsActive", "LastUpdateBy", "LastUpdateDate", "PointsPerUnit", "ProductId", "RequiredSuccessfulPayments" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 3m, "00000002-prod-0000-0000-000000000002", 1 },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 6m, "00000003-prod-0000-0000-000000000003", 1 },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, 6m, "00000004-prod-0000-0000-000000000004", 1 }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "AnnualPrice", "CreatedBy", "CreationDate", "DeletedAt", "DeletedBy", "Description", "DescriptionPromo", "ImageUrl", "ImageUrlPromo", "IsActive", "IsDeleted", "LastUpdateBy", "LastUpdateDate", "MembershipLevelId", "MonthlyFee", "MonthlyFeePromo", "Name", "OldSystemProductId", "Price180Days", "Price90Days", "QualificationPoins", "QualificationPoinsPromo", "SetupFee", "SetupFeePromo" },
                values: new object[,]
                {
                    { "00000001-prod-0000-0000-000000000001", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, 0m, "Travel Advantage Guest Member", 1, 0m, 0m, 0, 0, 0m, 0m },
                    { "00000006-prod-0000-0000-000000000006", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, 0m, "Monthly Subscription", 6, 0m, 0m, 0, 0, 0m, 0m }
                });

            migrationBuilder.InsertData(
                table: "RankDefinitions",
                columns: new[] { "Id", "CertificateTemplateUrl", "CreatedBy", "CreationDate", "Description", "LastUpdateBy", "LastUpdateDate", "Name", "SortOrder", "Status" },
                values: new object[,]
                {
                    { 1, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "18 ET points (3 Elite/Turbo members). DTR: $4/day.", null, null, "Silver", 1, 1 },
                    { 2, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "72 ET points (12 Elite/Turbo members). DTR: $10/day.", null, null, "Gold", 2, 1 },
                    { 3, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "175 ET points. DTR: $15/day. Boost Bonus unlocked.", null, null, "Platinum", 3, 1 },
                    { 4, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "350 DT / 175 ET points. DTR: $25/day.", null, null, "Titanium", 4, 1 },
                    { 5, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.", null, null, "Jade", 5, 1 },
                    { 6, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "1,500 DT / 750 ET points. DTR: $80/day.", null, null, "Pearl", 6, 1 },
                    { 7, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "3,000 DT / 1,500 ET points. DTR: $150/day.", null, null, "Emerald", 7, 1 },
                    { 8, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "6,000 DT / 3,000 ET points. DTR: $300/day.", null, null, "Ruby", 8, 1 },
                    { 9, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "10,000 DT / 5,000 ET points. DTR: $500/day.", null, null, "Sapphire", 9, 1 },
                    { 10, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "15,000 DT / 7,500 ET points. DTR: $750/day.", null, null, "Diamond", 10, 1 },
                    { 11, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "20,000 DT / 10,000 ET points. DTR: $1,000/day.", null, null, "Double Diamond", 11, 1 },
                    { 12, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "30,000 DT / 15,000 ET points. DTR: $1,500/day.", null, null, "Triple Diamond", 12, 1 },
                    { 13, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "60,000 DT / 30,000 ET points. DTR: $2,000/day.", null, null, "Blue Diamond", 13, 1 },
                    { 14, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "120,000 DT / 60,000 ET points. DTR: $3,000/day.", null, null, "Black Diamond", 14, 1 },
                    { 15, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "200,000 DT / 100,000 ET points. DTR: $4,000/day.", null, null, "Royal", 15, 1 },
                    { 16, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "300,000 DT / 150,000 ET points. DTR: $5,000/day.", null, null, "Double Royal", 16, 1 },
                    { 17, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "400,000 DT / 200,000 ET points. DTR: $7,500/day.", null, null, "Triple Royal", 17, 1 },
                    { 18, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "500,000 DT / 250,000 ET points. DTR: $10,000/day.", null, null, "Blue Royal", 18, 1 },
                    { 19, null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "700,000 DT / 350,000 ET points. DTR: $15,000/day.", null, null, "Black Royal", 19, 1 }
                });

            migrationBuilder.InsertData(
                table: "TokenTypeCommissions",
                columns: new[] { "Id", "CarBonusEligible", "CommissionPerToken", "CommissionTypeId", "CreatedBy", "CreationDate", "EligibleDailyResidual", "EligibleMembershipResidual", "LastUpdateBy", "LastUpdateDate", "PresidentialBonusEligible", "TokenTypeId", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[,]
                {
                    { 1, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 1, true, true, false, true, true, false },
                    { 2, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 2, true, true, false, true, true, false },
                    { 3, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 3, false, false, false, false, false, false },
                    { 4, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 4, false, false, false, false, false, false },
                    { 5, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 5, true, true, false, true, true, false },
                    { 6, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 6, false, false, false, false, false, false },
                    { 7, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 7, false, false, false, false, false, false },
                    { 8, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 8, false, false, false, false, false, false },
                    { 9, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 9, false, false, false, false, false, false },
                    { 10, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 10, false, false, false, false, false, false },
                    { 11, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 11, true, true, false, true, true, false },
                    { 12, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 12, true, true, false, true, true, false },
                    { 13, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 13, true, true, false, true, true, false },
                    { 14, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 14, false, false, false, false, false, false },
                    { 15, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 15, true, true, false, true, true, false },
                    { 16, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 16, true, true, false, true, true, false },
                    { 17, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 17, false, false, false, false, false, false },
                    { 19, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 19, true, true, false, true, true, false },
                    { 20, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 20, false, false, false, false, false, false },
                    { 21, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 21, false, false, false, false, false, false },
                    { 22, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 22, false, false, false, false, false, false },
                    { 23, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 23, false, false, false, false, false, false },
                    { 24, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 24, false, false, false, false, false, false },
                    { 25, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 25, false, false, false, false, false, false },
                    { 26, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 26, true, true, false, true, true, false },
                    { 27, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 27, true, true, false, true, true, false },
                    { 28, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 28, true, true, false, true, true, false },
                    { 29, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 29, true, true, false, true, true, false },
                    { 30, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 30, true, true, false, true, true, false },
                    { 31, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 31, true, true, false, true, true, false },
                    { 32, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 32, true, true, false, true, true, false },
                    { 33, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 33, true, true, false, true, true, false },
                    { 34, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 34, true, true, false, true, true, false },
                    { 35, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 35, false, false, false, false, false, false },
                    { 36, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 36, false, false, false, false, false, false },
                    { 37, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 37, false, false, false, false, false, false },
                    { 38, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 38, false, false, false, false, false, false },
                    { 39, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 39, false, false, false, false, false, false },
                    { 40, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 40, false, false, false, false, false, false },
                    { 41, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 41, false, false, false, false, false, false },
                    { 42, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 42, false, false, false, false, false, false },
                    { 43, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 43, false, false, false, false, false, false },
                    { 44, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 44, false, false, false, false, false, false },
                    { 45, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 45, false, false, false, false, false, false },
                    { 46, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 46, false, false, false, false, false, false },
                    { 47, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 47, false, false, false, false, false, false },
                    { 48, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 48, false, false, false, false, false, false },
                    { 49, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 49, true, true, false, true, true, false },
                    { 50, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 50, true, true, false, true, true, false },
                    { 51, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 51, false, false, false, false, false, false },
                    { 52, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 52, false, false, false, false, false, false },
                    { 53, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 53, false, false, false, false, false, false },
                    { 54, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 54, true, true, false, true, true, false },
                    { 55, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 55, false, false, false, false, false, false },
                    { 56, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 56, true, true, false, false, true, false },
                    { 57, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 57, true, true, false, false, true, false },
                    { 58, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 58, true, true, false, false, true, false },
                    { 59, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 59, true, true, false, false, true, false },
                    { 60, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 60, true, true, false, false, true, false },
                    { 61, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 61, false, false, false, false, false, false },
                    { 62, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 62, false, false, false, false, false, false },
                    { 63, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 63, false, false, false, false, false, false },
                    { 64, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 64, true, true, false, true, true, false },
                    { 65, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 65, true, true, false, true, true, false },
                    { 66, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 66, false, false, false, false, false, false },
                    { 67, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 67, true, true, false, true, true, false },
                    { 68, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 68, true, true, false, true, true, false },
                    { 69, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 69, true, true, true, true, true, true },
                    { 70, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 70, true, true, true, true, true, true },
                    { 71, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 71, true, true, false, true, true, false },
                    { 72, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 72, true, true, false, true, true, false },
                    { 73, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 73, true, true, true, true, true, true },
                    { 74, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 74, true, true, true, true, true, true },
                    { 75, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 75, true, true, false, true, true, false },
                    { 76, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 76, true, true, false, true, true, false },
                    { 77, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 77, true, true, false, false, true, false },
                    { 78, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, false, 78, false, false, false, false, false, false },
                    { 79, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 79, true, true, false, true, true, false },
                    { 80, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 80, true, true, true, true, true, true },
                    { 81, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 81, false, false, false, false, false, false },
                    { 82, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 82, false, false, false, false, false, false },
                    { 83, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 83, false, false, false, false, false, false },
                    { 84, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 84, false, false, false, false, false, false },
                    { 85, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 85, false, false, false, false, false, false },
                    { 86, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 86, false, false, false, false, false, false },
                    { 87, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 87, false, false, false, false, false, false },
                    { 88, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 88, false, false, false, false, false, false },
                    { 89, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 89, false, false, false, false, false, false },
                    { 90, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 90, false, false, false, false, false, false },
                    { 91, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 91, false, false, false, false, false, false },
                    { 92, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 92, false, false, false, false, false, false },
                    { 93, false, 0m, 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, null, false, 93, false, false, false, false, false, false },
                    { 94, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 94, true, true, false, true, true, false },
                    { 95, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 95, true, true, false, true, true, false },
                    { 96, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 96, true, true, true, true, true, true },
                    { 97, true, 0m, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 97, true, true, false, true, true, false },
                    { 98, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 98, true, true, false, true, true, false },
                    { 99, true, 0m, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, 99, true, true, true, true, true, true }
                });

            migrationBuilder.InsertData(
                table: "TokenTypes",
                columns: new[] { "Id", "CreatedBy", "CreationDate", "Description", "IsActive", "IsGuestPass", "LastUpdateBy", "LastUpdateDate", "Name", "TemplateUrl" },
                values: new object[,]
                {
                    { 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro", null },
                    { 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, true, null, null, "Guest Member", null },
                    { 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite", null },
                    { 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: VIP", null },
                    { 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite", null },
                    { 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Travel Advantage Elite (Signup)", null },
                    { 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Travel Advantage Lite", null },
                    { 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador", null },
                    { 9, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment Pro ($99.97 cost / no commission)", null },
                    { 10, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual Fee", null },
                    { 11, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event", null },
                    { 12, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro + Event", null },
                    { 13, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP Member", null },
                    { 14, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Mobile App", null },
                    { 15, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Pro Member", null },
                    { 16, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member", null },
                    { 17, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Elite", null },
                    { 19, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Special", null },
                    { 20, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, false, false, null, null, "--Available--", null },
                    { 21, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual: VIP 365", null },
                    { 22, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro", null },
                    { 23, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Annual: Biz Center", null },
                    { 24, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Legacy Biz Center Fee", null },
                    { 25, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Mall", null },
                    { 26, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite180", null },
                    { 27, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite180 + Event", null },
                    { 28, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro180", null },
                    { 29, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Pro180 + Event", null },
                    { 30, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus180", null },
                    { 31, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus180 + Event", null },
                    { 32, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite180 Member", null },
                    { 33, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Pro180 Member", null },
                    { 34, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus180 Member", null },
                    { 35, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Pro", null },
                    { 36, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Pro180", null },
                    { 37, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Elite", null },
                    { 38, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus180 to Elite180", null },
                    { 39, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Pro180", null },
                    { 40, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro to Elite180", null },
                    { 41, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro180 to Elite", null },
                    { 42, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Pro180 to Elite180", null },
                    { 43, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Elite to Elite180", null },
                    { 44, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 Level 2 (79.97)", null },
                    { 45, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 Level 3 (39.97)", null },
                    { 46, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro180 Level 2", null },
                    { 47, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Pro180 Level 3", null },
                    { 48, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Plus180", null },
                    { 49, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus", null },
                    { 50, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus + Event", null },
                    { 51, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Plus", null },
                    { 52, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus to Elite", null },
                    { 53, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Plus to Elite180", null },
                    { 54, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus Member", null },
                    { 55, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Monthly: Elite180 (59.97)", null },
                    { 56, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP", null },
                    { 57, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP 365", null },
                    { 58, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Plus", null },
                    { 59, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Elite", null },
                    { 60, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to Elite180", null },
                    { 61, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Plus", null },
                    { 62, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Elite", null },
                    { 63, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: VIP to Elite180", null },
                    { 64, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP", null },
                    { 65, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP + Event", null },
                    { 66, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Elite to Turbo", null },
                    { 67, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 365", null },
                    { 68, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 365 + Event", null },
                    { 69, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO", null },
                    { 70, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event + TURBO", null },
                    { 71, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Coupon)", null },
                    { 72, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event (Coupon)", null },
                    { 73, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + Event + TURBO (Coupon)", null },
                    { 74, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Coupon)", null },
                    { 75, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180", null },
                    { 76, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180 + Event", null },
                    { 77, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Upgrade: Guest to VIP 180", null },
                    { 78, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Recurring: VIP 180", null },
                    { 79, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP 180 Member", null },
                    { 80, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member + TURBO", null },
                    { 81, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite FREE", null },
                    { 82, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Coupon) FREE", null },
                    { 83, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO FREE", null },
                    { 84, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Coupon) FREE", null },
                    { 85, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus FREE", null },
                    { 86, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP FREE", null },
                    { 87, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + VIP 180 FREE", null },
                    { 88, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador FREE", null },
                    { 89, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member FREE", null },
                    { 90, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Elite Member + TURBO FREE", null },
                    { 91, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Plus Member FREE", null },
                    { 92, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP Member FREE", null },
                    { 93, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: VIP 180 FREE", null },
                    { 94, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Elite Ambassador SpecialPromo", null },
                    { 95, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Plus Ambassador SpecialPromo", null },
                    { 96, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Turbo Ambassador SpecialPromo", null },
                    { 97, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Plus (Help a Friend)", null },
                    { 98, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite (Help a Friend)", null },
                    { 99, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, null, null, "Enrollment: Ambassador + Elite + TURBO (Help a Friend)", null }
                });

            migrationBuilder.InsertData(
                table: "CommissionTypes",
                columns: new[] { "Id", "CommissionCategoryId", "CreatedBy", "CreationDate", "Cummulative", "CurrentRank", "DaysAfterJoining", "Description", "EnrollmentTeam", "ExternalMembers", "FixedAmount", "IsActive", "IsEnrollmentBased", "IsPaidOnRenewal", "IsPaidOnSignup", "IsRealTime", "IsSponsorBonus", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifeTimeRank", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MembersRebill", "Name", "NewMembers", "PaymentDelayDays", "Percentage", "PersonalPoints", "ResidualBased", "ResidualOverCommissionType", "ResidualPercentage", "ReverseId", "SponsoredMembers", "TeamPoints", "TriggerOrder" },
                values: new object[,]
                {
                    { 1, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "One-time $20 bonus to the direct enroller when a VIP member signs up.", 0, 0, 20m, true, false, false, true, true, true, null, null, 2, 0, 0.5, 0.5, 0, "Member Bonus – VIP", 0, 4, 0m, 0, false, 0, 0.0, 29, 0, 0, 0 },
                    { 2, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "One-time $40 bonus to the direct enroller when an Elite member signs up.", 0, 0, 40m, true, false, false, true, true, true, null, null, 3, 0, 0.5, 0.5, 0, "Member Bonus – Elite", 0, 4, 0m, 0, false, 0, 0.0, 30, 0, 0, 0 },
                    { 3, 1, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "One-time $80 bonus to the direct enroller when a Turbo member signs up.", 0, 0, 80m, true, false, false, true, true, true, null, null, 4, 0, 0.5, 0.5, 0, "Member Bonus – Turbo", 0, 4, 0m, 0, false, 0, 0.0, 31, 0, 0, 0 },
                    { 4, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 14, "Earn $150 when enrolling within your first 14 days as an ambassador.", 0, 0, 150m, true, false, false, true, true, false, null, null, 1, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 1", 0, 0, 0m, 0, false, 0, 0.0, 32, 0, 0, 1 },
                    { 5, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 7, "Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).", 0, 0, 150m, true, false, false, true, true, false, null, null, 1, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 2", 0, 0, 0m, 0, false, 0, 0.0, 32, 0, 0, 2 },
                    { 6, 2, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 7, "Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).", 0, 0, 150m, true, false, false, true, true, false, null, null, 1, 0, 0.5, 0.5, 0, "Fast Start Bonus – Window 3", 0, 0, 0m, 0, false, 0, 0.0, 32, 0, 0, 3 },
                    { 7, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).", 0, 0, 4m, true, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Silver", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 18, 0 },
                    { 8, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).", 0, 0, 10m, true, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Gold", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 72, 0 },
                    { 9, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).", 0, 0, 15m, true, true, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Platinum", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 175, 0 },
                    { 10, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $25/day when qualifying at Titanium rank (350 Dual Team points).", 0, 0, 25m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Titanium", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 350, 0 },
                    { 11, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $40/day when qualifying at Jade rank (700 Dual Team points).", 0, 0, 40m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Jade", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 700, 0 },
                    { 12, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).", 0, 0, 80m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Pearl", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 1500, 0 },
                    { 13, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).", 0, 0, 150m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Emerald", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 3000, 0 },
                    { 14, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).", 0, 0, 300m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Ruby", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 6000, 0 },
                    { 15, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).", 0, 0, 500m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Sapphire", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 10000, 0 },
                    { 16, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).", 0, 0, 750m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 15000, 0 },
                    { 17, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).", 0, 0, 1000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Double Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 20000, 0 },
                    { 18, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).", 0, 0, 1500m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Triple Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 30000, 0 },
                    { 19, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).", 0, 0, 2000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Blue Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 60000, 0 },
                    { 20, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).", 0, 0, 3000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Black Diamond", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 120000, 0 },
                    { 21, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $4,000/day when qualifying at Royal rank (200,000 DT points).", 0, 0, 4000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 200000, 0 },
                    { 22, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $5,000/day when qualifying at Double Royal (300,000 DT points).", 0, 0, 5000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Double Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 300000, 0 },
                    { 23, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).", 0, 0, 7500m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Triple Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 400000, 0 },
                    { 24, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).", 0, 0, 10000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Blue Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 500000, 0 },
                    { 25, 3, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $15,000/day when qualifying at Black Royal (700,000 DT points).", 0, 0, 15000m, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "DTR – Black Royal", 0, 4, 0m, 0, true, 0, 0.0, 0, 0, 700000, 0 },
                    { 26, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.", 0, 0, 600m, true, false, false, false, false, false, null, null, 0, 4, 0.5, 0.5, 0, "Boost Bonus – Gold", 6, 15, 0m, 0, false, 0, 0.0, 0, 0, 0, 1 },
                    { 27, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.", 0, 0, 1200m, true, false, false, false, false, false, null, null, 0, 5, 0.5, 0.5, 0, "Boost Bonus – Platinum", 12, 15, 0m, 0, false, 0, 0.0, 0, 0, 0, 2 },
                    { 28, 4, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.", 0, 0, null, true, false, false, false, false, false, null, null, 0, 6, 0.5, 0.5, 0, "Presidential Bonus", 0, 15, 20m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 29, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a VIP Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 30, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses an Elite Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 31, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Turbo Member Bonus when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Member Bonus Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 32, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses any FSB window earning when a signup cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Fast Start Bonus", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 33, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.", 0, 0, 25m, true, false, false, true, true, true, null, null, 2, 0, 0.5, 0.5, 0, "Builder Bonus – VIP", 0, 4, 0m, 0, false, 0, 0.0, 41, 0, 0, 0 },
                    { 34, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.", 0, 0, 60m, true, false, false, true, true, true, null, null, 3, 0, 0.5, 0.5, 0, "Builder Bonus – Elite", 0, 4, 0m, 0, false, 0, 0.0, 42, 0, 0, 0 },
                    { 35, 6, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.", 0, 0, 120m, true, false, false, true, true, true, null, null, 4, 0, 0.5, 0.5, 0, "Builder Bonus – Turbo", 0, 4, 0m, 0, false, 0, 0.0, 43, 0, 0, 0 },
                    { 36, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.", 0, 0, 30m, true, false, false, true, true, true, null, null, 2, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – VIP", 0, 4, 0m, 0, false, 0, 0.0, 44, 0, 0, 0 },
                    { 37, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.", 0, 0, 80m, true, false, false, true, true, true, null, null, 3, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – Elite", 0, 4, 0m, 0, false, 0, 0.0, 45, 0, 0, 0 },
                    { 38, 7, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.", 0, 0, 160m, true, false, false, true, true, true, null, null, 4, 0, 0.5, 0.5, 0, "Builder Bonus Turbo – Turbo", 0, 4, 0m, 0, false, 0, 0.0, 46, 0, 0, 0 },
                    { 39, 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.", 0, 0, null, true, false, false, false, false, false, null, null, 0, 0, 0.5, 0.5, 0, "Admin Fee", 0, 0, 5m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 40, 8, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.", 0, 0, 1m, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Token Deduction", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 41, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 42, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 43, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 44, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo VIP", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 45, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo Elite", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 },
                    { 46, 5, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, 0, "Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.", 0, 0, null, true, false, false, false, true, false, null, null, 0, 0, 0.5, 0.5, 0, "Reversal – Builder Bonus Turbo Turbo", 0, 0, 0m, 0, false, 0, 0.0, 0, 0, 0, 0 }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "AnnualPrice", "CreatedBy", "CreationDate", "DeletedAt", "DeletedBy", "Description", "DescriptionPromo", "ImageUrl", "ImageUrlPromo", "IsActive", "IsDeleted", "LastUpdateBy", "LastUpdateDate", "MembershipLevelId", "MonthlyFee", "MonthlyFeePromo", "Name", "OldSystemProductId", "Price180Days", "Price90Days", "QualificationPoins", "QualificationPoinsPromo", "SetupFee", "SetupFeePromo" },
                values: new object[] { "00000002-prod-0000-0000-000000000002", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 2, 40m, 0m, "Travel Advantage VIP", 2, 0m, 0m, 3, 0, 0m, 0m });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "AnnualPrice", "CreatedBy", "CreationDate", "DeletedAt", "DeletedBy", "Description", "DescriptionPromo", "ImageUrl", "ImageUrlPromo", "IsActive", "IsDeleted", "JoinPageMembership", "LastUpdateBy", "LastUpdateDate", "MembershipLevelId", "MonthlyFee", "MonthlyFeePromo", "Name", "OldSystemProductId", "Price180Days", "Price90Days", "QualificationPoins", "QualificationPoinsPromo", "SetupFee", "SetupFeePromo" },
                values: new object[,]
                {
                    { "00000003-prod-0000-0000-000000000003", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.", null, "", null, true, false, true, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 3, 99m, 0m, "Travel Advantage Elite", 3, 0m, 0m, 6, 0, 0m, 0m },
                    { "00000004-prod-0000-0000-000000000004", 0m, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.", null, "", null, true, false, true, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 4, 199m, 0m, "Travel Advantage Turbo", 4, 0m, 0m, 6, 0, 0m, 0m }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "AnnualPrice", "CorporateFee", "CreatedBy", "CreationDate", "DeletedAt", "DeletedBy", "Description", "DescriptionPromo", "ImageUrl", "ImageUrlPromo", "IsActive", "IsDeleted", "LastUpdateBy", "LastUpdateDate", "MembershipLevelId", "MonthlyFee", "MonthlyFeePromo", "Name", "OldSystemProductId", "Price180Days", "Price90Days", "QualificationPoins", "QualificationPoinsPromo", "SetupFee", "SetupFeePromo" },
                values: new object[] { "00000005-prod-0000-0000-000000000005", 99m, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.", null, "", null, true, false, null, new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), 1, 0m, 0m, "Subscription", 5, 0m, 0m, 0, 0, 99m, 0m });

            migrationBuilder.InsertData(
                table: "RankRequirements",
                columns: new[] { "Id", "AchievementMessage", "CertificateUrl", "CreatedBy", "CreationDate", "CurrentRankDescription", "DailyBonus", "EnrollmentQualifiedTeamMembers", "EnrollmentTeam", "ExternalMembers", "LastUpdateBy", "LastUpdateDate", "LevelNo", "LifetimeHoldingDuration", "MaxEnrollmentTeamPointsPerBranch", "MaxTeamPointsPerBranch", "MonthlyBonus", "PersonalPoints", "PlacementQualifiedTeamMembers", "RankBonus", "RankDefinitionId", "RankDescription", "SalesVolume", "SponsoredMembers", "TeamPoints" },
                values: new object[,]
                {
                    { 1, "Congratulations! You have reached Silver rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.", 4m, 0, 3, 1, null, null, 1, 0, 0.5, 0.5, 0m, 1, 0, 100m, 1, "Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).", 0m, 1, 18 },
                    { 2, "Congratulations! You have reached Gold rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.", 10m, 0, 12, 1, null, null, 2, 0, 0.5, 0.5, 0m, 1, 0, 300m, 2, "Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).", 0m, 1, 72 },
                    { 3, "Congratulations! You have reached Platinum rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.", 15m, 0, 29, 1, null, null, 3, 0, 0.5, 0.5, 0m, 1, 0, 500m, 3, "Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.", 0m, 2, 175 },
                    { 4, "Congratulations! You have reached Titanium rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.", 25m, 0, 0, 1, null, null, 4, 0, 0.5, 0.5, 0m, 1, 0, 1000m, 4, "Qualify with 350 Dual Team points (max 50% per branch).", 0m, 2, 350 },
                    { 5, "Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.", 40m, 0, 0, 1, null, null, 5, 0, 0.5, 0.5, 0m, 1, 0, 2500m, 5, "Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked.", 0m, 3, 700 },
                    { 6, "Congratulations! You have reached Pearl rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.", 80m, 0, 0, 1, null, null, 6, 0, 0.5, 0.5, 0m, 1, 0, 5000m, 6, "Qualify with 1,500 Dual Team points (max 50% per branch).", 0m, 3, 1500 },
                    { 7, "Congratulations! You have reached Emerald rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.", 150m, 0, 0, 1, null, null, 7, 0, 0.5, 0.5, 0m, 1, 0, 10000m, 7, "Qualify with 3,000 Dual Team points (max 50% per branch).", 0m, 4, 3000 },
                    { 8, "Congratulations! You have reached Ruby rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.", 300m, 0, 0, 1, null, null, 8, 0, 0.5, 0.5, 0m, 1, 0, 25000m, 8, "Qualify with 6,000 Dual Team points (max 50% per branch).", 0m, 5, 6000 },
                    { 9, "Congratulations! You have reached Sapphire rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.", 500m, 0, 0, 1, null, null, 9, 0, 0.5, 0.5, 0m, 1, 0, 50000m, 9, "Qualify with 10,000 Dual Team points (max 50% per branch).", 0m, 5, 10000 },
                    { 10, "Congratulations! You have reached Diamond rank and unlocked the Car Bonus!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.", 750m, 0, 0, 1, null, null, 10, 0, 0.5, 0.5, 500m, 1, 0, 100000m, 10, "Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked.", 0m, 6, 15000 },
                    { 11, "Congratulations! You have reached Double Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.", 1000m, 0, 0, 1, null, null, 11, 0, 0.5, 0.5, 750m, 1, 0, 150000m, 11, "Qualify with 20,000 Dual Team points (max 50% per branch).", 0m, 6, 20000 },
                    { 12, "Congratulations! You have reached Triple Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.", 1500m, 0, 0, 1, null, null, 12, 0, 0.5, 0.5, 1000m, 1, 0, 200000m, 12, "Qualify with 30,000 Dual Team points (max 50% per branch).", 0m, 7, 30000 },
                    { 13, "Congratulations! You have reached Blue Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.", 2000m, 0, 0, 1, null, null, 13, 0, 0.5, 0.5, 1500m, 1, 0, 300000m, 13, "Qualify with 60,000 Dual Team points (max 50% per branch).", 0m, 8, 60000 },
                    { 14, "Congratulations! You have reached Black Diamond rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.", 3000m, 0, 0, 1, null, null, 14, 0, 0.5, 0.5, 2500m, 1, 0, 500000m, 14, "Qualify with 120,000 Dual Team points (max 50% per branch).", 0m, 10, 120000 },
                    { 15, "Congratulations! You have reached Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.", 4000m, 0, 0, 1, null, null, 15, 0, 0.5, 0.5, 4000m, 1, 0, 750000m, 15, "Qualify with 200,000 Dual Team points (max 50% per branch).", 0m, 12, 200000 },
                    { 16, "Congratulations! You have reached Double Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.", 5000m, 0, 0, 1, null, null, 16, 0, 0.5, 0.5, 5000m, 1, 0, 1000000m, 16, "Qualify with 300,000 Dual Team points (max 50% per branch).", 0m, 15, 300000 },
                    { 17, "Congratulations! You have reached Triple Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.", 7500m, 0, 0, 1, null, null, 17, 0, 0.5, 0.5, 7500m, 1, 0, 1500000m, 17, "Qualify with 400,000 Dual Team points (max 50% per branch).", 0m, 20, 400000 },
                    { 18, "Congratulations! You have reached Blue Royal rank!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.", 10000m, 0, 0, 1, null, null, 18, 0, 0.5, 0.5, 10000m, 1, 0, 2000000m, 18, "Qualify with 500,000 Dual Team points (max 50% per branch).", 0m, 25, 500000 },
                    { 19, "Congratulations! You have reached Black Royal — the highest rank in the company!", null, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.", 15000m, 0, 0, 1, null, null, 19, 0, 0.5, 0.5, 15000m, 1, 0, 3000000m, 19, "Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey.", 0m, 30, 700000 }
                });

            migrationBuilder.InsertData(
                table: "ProductCommissions",
                columns: new[] { "Id", "CarBonusEligible", "CreatedBy", "CreationDate", "EligibleDailyResidual", "EligibleMembershipResidual", "LastUpdateBy", "LastUpdateDate", "PresidentialBonusEligible", "ProductId", "TriggerBoostBonus", "TriggerBuilderBonus", "TriggerBuilderBonusTurbo", "TriggerFastStartBonus", "TriggerSponsorBonus", "TriggerSponsorBonusTurbo" },
                values: new object[,]
                {
                    { 1, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000002-prod-0000-0000-000000000002", true, true, false, true, true, false },
                    { 2, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000003-prod-0000-0000-000000000003", true, true, false, true, true, false },
                    { 3, true, "seed", new DateTime(2026, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), true, true, null, null, true, "00000004-prod-0000-0000-000000000004", true, true, true, true, true, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CannedResponses_TeamId",
                table: "CannedResponses",
                column: "TeamId");

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
                name: "IX_KbArticles_CategoryId",
                table: "KbArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_HelpfulCount",
                table: "KbArticles",
                column: "HelpfulCount");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_Slug",
                table: "KbArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_Visibility_CategoryId",
                table: "KbArticles",
                columns: new[] { "Visibility", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_KbArticleVersions_ArticleId",
                table: "KbArticleVersions",
                column: "ArticleId");

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
                name: "IX_ProductCommissionPromos_ProductId",
                table: "ProductCommissionPromos",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCommissions_ProductId",
                table: "ProductCommissions",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductLoyaltySettings_ProductId",
                table: "ProductLoyaltySettings",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_MembershipLevelId",
                table: "Products",
                column: "MembershipLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_RankRequirements_RankDefinitionId",
                table: "RankRequirements",
                column: "RankDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaBreaches_BreachedAt",
                table: "SlaBreaches",
                column: "BreachedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SlaBreaches_TicketId",
                table: "SlaBreaches",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportAgents_TeamId",
                table: "SupportAgents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportAgents_UserId",
                table: "SupportAgents",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_TicketId",
                table: "TicketAttachments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_DefaultSlaPolicyId",
                table: "TicketCategories",
                column: "DefaultSlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_DefaultTeamId",
                table: "TicketCategories",
                column: "DefaultTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_ParentCategoryId",
                table: "TicketCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_IsInternal",
                table: "TicketComments",
                column: "IsInternal");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_TicketId_CreationDate",
                table: "TicketComments",
                columns: new[] { "TicketId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_TicketId_CreationDate",
                table: "TicketHistories",
                columns: new[] { "TicketId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMetrics_Date",
                table: "TicketMetrics",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedTeamId_Status",
                table: "Tickets",
                columns: new[] { "AssignedTeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedToUserId_Status",
                table: "Tickets",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_MemberId_CreationDate",
                table: "Tickets",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaPolicyId",
                table: "Tickets",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_LastUpdateDate",
                table: "Tickets",
                columns: new[] { "Status", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketNumber",
                table: "Tickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_MemberProfileId",
                table: "TokenBalances",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_MemberId_CreationDate",
                table: "TokenTransactions",
                columns: new[] { "MemberId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeCommissions_TokenTypeId_CommissionTypeId",
                table: "TokenTypeCommissions",
                columns: new[] { "TokenTypeId", "CommissionTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTypeProducts_TokenTypeId_ProductId",
                table: "TokenTypeProducts",
                columns: new[] { "TokenTypeId", "ProductId" },
                unique: true);

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
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditTracking");

            migrationBuilder.DropTable(
                name: "CannedResponses");

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
                name: "KbArticleVersions");

            migrationBuilder.DropTable(
                name: "LoyaltyPoints");

            migrationBuilder.DropTable(
                name: "MemberFcmTokens");

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
                name: "SlaBreaches");

            migrationBuilder.DropTable(
                name: "SupportAgents");

            migrationBuilder.DropTable(
                name: "TicketAttachments");

            migrationBuilder.DropTable(
                name: "TicketComments");

            migrationBuilder.DropTable(
                name: "TicketHistories");

            migrationBuilder.DropTable(
                name: "TicketMetrics");

            migrationBuilder.DropTable(
                name: "TicketSequences");

            migrationBuilder.DropTable(
                name: "TokenBalances");

            migrationBuilder.DropTable(
                name: "TokenTransactions");

            migrationBuilder.DropTable(
                name: "TokenTypeCommissions");

            migrationBuilder.DropTable(
                name: "TokenTypeProducts");

            migrationBuilder.DropTable(
                name: "TokenTypes");

            migrationBuilder.DropTable(
                name: "WalletHistories");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "CommissionCountDowns");

            migrationBuilder.DropTable(
                name: "CommissionOperationType");

            migrationBuilder.DropTable(
                name: "CommissionTypes");

            migrationBuilder.DropTable(
                name: "KbArticles");

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
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "SupportTeams");

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
