using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Add2faStepUpAndSmsTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredTwoFactorChannel",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Las filas existentes quedan en Email, que es el canal que ya usan hoy los miembros
            // con 2FA activo. Sin esto heredarian Authenticator (valor 0 del enum) y se les
            // pediria un codigo TOTP que nunca enrolaron.
            migrationBuilder.Sql("UPDATE AspNetUsers SET PreferredTwoFactorChannel = 1;");

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorEnrolledAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorPhoneConfirmed",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorPhoneEncrypted",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorPhoneLast4",
                table: "AspNetUsers",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthSecurityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChallengeJti = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthSecurityEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmsTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StepUpPolicies",
                columns: table => new
                {
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequiredChannel = table.Column<int>(type: "int", nullable: true),
                    FreshnessWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepUpPolicies", x => x.OperationKey);
                });

            migrationBuilder.CreateTable(
                name: "SmsTemplateLocalizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SmsTemplateId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(480)", maxLength: 480, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplateLocalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsTemplateLocalizations_SmsTemplates_SmsTemplateId",
                        column: x => x.SmsTemplateId,
                        principalTable: "SmsTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSecurityEvents_EventType_CreationDate",
                table: "AuthSecurityEvents",
                columns: new[] { "EventType", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSecurityEvents_OperationKey_CreationDate",
                table: "AuthSecurityEvents",
                columns: new[] { "OperationKey", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSecurityEvents_UserId_CreationDate",
                table: "AuthSecurityEvents",
                columns: new[] { "UserId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsTemplateLocalizations_SmsTemplateId_LanguageCode",
                table: "SmsTemplateLocalizations",
                columns: new[] { "SmsTemplateId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsTemplates_EventType",
                table: "SmsTemplates",
                column: "EventType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthSecurityEvents");

            migrationBuilder.DropTable(
                name: "SmsTemplateLocalizations");

            migrationBuilder.DropTable(
                name: "StepUpPolicies");

            migrationBuilder.DropTable(
                name: "SmsTemplates");

            migrationBuilder.DropColumn(
                name: "PreferredTwoFactorChannel",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnrolledAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorPhoneConfirmed",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorPhoneEncrypted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorPhoneLast4",
                table: "AspNetUsers");
        }
    }
}
