using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class IndicePatrocinadorYFechaDeAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan J (R2): no estrechar la columna si algún valor no cabe; mejor fallar el despliegue
            // que truncar un patrocinador.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MemberProfiles] WHERE LEN([SponsorMemberId]) > 20)
    THROW 51001, N'MemberProfiles.SponsorMemberId tiene valores de mas de 20 caracteres: revisar antes de migrar (Plan J, Tarea 18).', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "SponsorMemberId",
                table: "MemberProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_SponsorMemberId_EnrollDate",
                table: "MemberProfiles",
                columns: new[] { "SponsorMemberId", "EnrollDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_SponsorMemberId_EnrollDate",
                table: "MemberProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "SponsorMemberId",
                table: "MemberProfiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
