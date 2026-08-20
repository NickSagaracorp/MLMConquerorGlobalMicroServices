using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEWalletIdentifiersToMemberId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // En eWallet el identificador es el UserName de i-Payout, y ese UserName ES el
            // MemberId: lo asigna la compania, no el proveedor. Habia 198 wallets con un email
            // ahi, herencia de cuando el modelo se creia email-based. Bajo el modelo corregido
            // un email NO es un identificador valido de eWallet: cualquier intento de guardar
            // esas wallets o de marcarlas como default era rechazado por la validacion.
            //
            // Se normalizan SOLO las que tienen email. Un admin puede haber apuntado
            // deliberadamente el eWallet de OTRO ambassador —cuando alguien mas paga esa
            // membresia— pero ese valor es un MemberId, no un email, asi que no se toca.
            migrationBuilder.Sql(@"
UPDATE [Wallets]
SET [AccountIdentifier] = [MemberId]
WHERE [WalletType] = 4              -- eWallet
  AND [AccountIdentifier] LIKE '%@%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: los emails que habia eran datos sembrados sin significado real
            // del lado del proveedor. Restaurarlos volveria a dejar las wallets sin poder
            // guardarse. Si hiciera falta, hay que hacerlo desde un backup previo.
        }
    }
}
