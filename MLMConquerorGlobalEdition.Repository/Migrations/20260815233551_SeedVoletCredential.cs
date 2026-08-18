using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedVoletCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Volet dejo de ser un stub simulado y ahora habla SOAP de verdad, asi que
            // necesita su fila de credencial. Va como migracion porque GatewayRoutingSeeder
            // se saltea el bloque entero si YA existe alguna ApiCredential — o sea, no
            // alcanza a las bases ya sembradas.
            //
            // Sus tres secretos NO siguen el patron de los demas gateways:
            //   ApiKeyEncrypted     -> apiName
            //   SecretKeyEncrypted  -> plantilla del auth token (contiene ##datetime##)
            //   MerchantIdEncrypted -> email de la cuenta merchant
            migrationBuilder.Sql(@"
INSERT INTO [ApiCredentials] ([Id],[ServiceKey],[Environment],[BaseUrl],[PortalUrl],[IsActive],[IsDeleted],[CreationDate],[LastUpdateDate],[CreatedBy])
SELECT CONVERT(nvarchar(50), NEWID()), v.ServiceKey, v.Environment, v.BaseUrl, v.PortalUrl, 0, 0,
       SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration:SeedVoletCredential'
FROM (VALUES
    (N'Volet', N'Production', N'https://wallet.advcash.com/wsm/merchantWebService', N'https://account.volet.com/'),
    (N'Volet', N'Sandbox',    N'https://wallet.advcash.com/wsm/merchantWebService', N'https://account.volet.com/')
) AS v(ServiceKey, Environment, BaseUrl, PortalUrl)
WHERE NOT EXISTS (
    SELECT 1 FROM [ApiCredentials] c
    WHERE c.[ServiceKey] = v.ServiceKey AND c.[Environment] = v.Environment);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se borran si siguen vacias: si alguien ya cargo los secretos, se conservan.
            migrationBuilder.Sql(@"
DELETE FROM [ApiCredentials]
WHERE [CreatedBy] = N'migration:SeedVoletCredential'
  AND [ApiKeyEncrypted] IS NULL AND [SecretKeyEncrypted] IS NULL AND [MerchantIdEncrypted] IS NULL;");
        }
    }
}
