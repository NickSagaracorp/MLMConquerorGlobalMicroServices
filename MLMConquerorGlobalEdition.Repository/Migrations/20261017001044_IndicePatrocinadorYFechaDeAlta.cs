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
            // que truncar un patrocinador. 450 es la longitud de GenealogyTree.MemberId, con el que se une.
            // DATALENGTH y no LEN: LEN no cuenta los espacios finales, que también se truncarían.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MemberProfiles] WHERE DATALENGTH([SponsorMemberId]) / 2 > 450)
    THROW 51001, N'MemberProfiles.SponsorMemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan J, Tarea 18).', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "SponsorMemberId",
                table: "MemberProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
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
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
