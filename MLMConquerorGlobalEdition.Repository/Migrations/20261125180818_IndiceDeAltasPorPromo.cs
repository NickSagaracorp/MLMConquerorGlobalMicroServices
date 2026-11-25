using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDeAltasPorPromo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions",
                column: "CreationDate",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "MemberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipSubscriptions_CreationDate",
                table: "MembershipSubscriptions",
                column: "CreationDate",
                descending: new bool[0]);
        }
    }
}
