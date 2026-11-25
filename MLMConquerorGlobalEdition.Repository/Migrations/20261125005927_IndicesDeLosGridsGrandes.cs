using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDeLosGridsGrandes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SignupRiskFingerprints_Cleared_CreationDate",
                table: "SignupRiskFingerprints",
                columns: new[] { "Cleared", "CreationDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_CreationDate",
                table: "PaymentHistories",
                column: "CreationDate",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "TransactionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions",
                column: "CreationDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_Status_EndDate",
                table: "MembershipSubscriptions",
                columns: new[] { "SubscriptionStatus", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionEarnings_Status_EarnedDate",
                table: "CommissionEarnings",
                columns: new[] { "Status", "EarnedDate" },
                descending: new[] { false, true },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignupRiskFingerprints_Cleared_CreationDate",
                table: "SignupRiskFingerprints");

            migrationBuilder.DropIndex(
                name: "IX_PaymentHistories_CreationDate",
                table: "PaymentHistories");

            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_Status_EndDate",
                table: "MembershipSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CommissionEarnings_Status_EarnedDate",
                table: "CommissionEarnings");
        }
    }
}
