using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class PuntosPorMembresiaActiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plan Q (DQ16): cada suscripcion activa guarda lo que aporta (DualTeamContribution = EnrollmentContribution);
            // salir de Active resta lo guardado. Se rellena SIN cambiar ningun punto: la activa de mayor nivel de cada
            // miembro aporta sus PersonalPoints actuales (lo que ya esta en las piernas y en EnrollmentPoints) y las demas,
            // 0. Los miembros con puntos y sin suscripcion activa (o activos sin puntos) no se tocan: realinearlos cambia lo
            // que se paga y lo decide Nick (Q3).
            migrationBuilder.Sql(@"
UPDATE MembershipSubscriptions SET DualTeamContribution = 0, EnrollmentContribution = 0
WHERE DualTeamContribution <> 0 OR EnrollmentContribution <> 0;
WITH a AS (
    SELECT s.Id, ROW_NUMBER() OVER (PARTITION BY s.MemberId ORDER BY s.MembershipLevelId DESC, s.Id) AS Rn
    FROM MembershipSubscriptions s WHERE s.SubscriptionStatus = 1)
UPDATE s SET DualTeamContribution = p.PP, EnrollmentContribution = p.PP
FROM MembershipSubscriptions s
JOIN a ON a.Id = s.Id AND a.Rn = 1
CROSS APPLY (SELECT TOP 1 ms.PersonalPoints AS PP FROM MemberStatistics ms WHERE ms.MemberId = s.MemberId ORDER BY ms.Id) p
WHERE p.PP <> 0;");
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions",
                column: "MemberId",
                principalTable: "MemberProfiles",
                principalColumn: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions",
                column: "MembershipLevelId",
                principalTable: "MembershipLevels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MemberProfiles_MemberId",
                table: "MembershipSubscriptions",
                column: "MemberId",
                principalTable: "MemberProfiles",
                principalColumn: "MemberId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId",
                table: "MembershipSubscriptions",
                column: "MembershipLevelId",
                principalTable: "MembershipLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
