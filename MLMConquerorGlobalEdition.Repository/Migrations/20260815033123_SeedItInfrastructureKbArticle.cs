using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Crea el apartado "IT / Infrastructure" en la Knowledge Base y siembra el artículo que
    /// documenta el cifrado de las credenciales de gateway.
    ///
    /// Va como migración y no como seeder para que exista en TODOS los entornos: es
    /// documentación operativa que IT necesita justamente cuando algo no arranca, y en ese
    /// momento nadie va a estar corriendo un seeder.
    ///
    /// El artículo es Visibility = Internal (2) a propósito: describe dónde viven las llaves
    /// y cómo se rotan. No debe ser público.
    /// </summary>
    public partial class SeedItInfrastructureKbArticle : Migration
    {
        private const int    CategoryId = 8;
        private const string ArticleId  = "kb-gateway-credential-encryption";
        private const string Slug       = "gateway-credential-encryption";
        private const string Actor      = "migration:SeedItInfrastructureKbArticle";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Apartado nuevo ──────────────────────────────────────────────────
            // Las 7 categorías existentes son de negocio (Commissions, Signups, Billing…).
            // Esta es la primera de infraestructura. SortOrder alto para que quede al final,
            // separada de las que usa el equipo de soporte todos los días.
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [TicketCategories] WHERE [Id] = {CategoryId})
BEGIN
    SET IDENTITY_INSERT [TicketCategories] ON;
    INSERT INTO [TicketCategories]
        ([Id],[Name],[Description],[DefaultPriority],[SortOrder],[IsActive],[CreationDate],[CreatedBy])
    VALUES
        ({CategoryId}, N'IT / Infrastructure',
         N'Internal runbooks for the platform itself: encryption, key management, deployment and environment configuration. Not customer facing.',
         N'Normal', 100, 1, SYSUTCDATETIME(), N'{Actor}');
    SET IDENTITY_INSERT [TicketCategories] OFF;
END;");

            // ── Artículo ────────────────────────────────────────────────────────
            // El body es HTML porque la pantalla de KB usa un editor WYSIWYG
            // (SfRichTextEditor), no markdown — pese a lo que dice el comentario de la entidad.
            var body = ArticleBodyHtml.Replace("'", "''");

            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [KbArticles] WHERE [Slug] = N'{Slug}')
BEGIN
    INSERT INTO [KbArticles]
        ([Id],[Title],[Slug],[Body],[CategoryId],[TagsJson],[Visibility],[AuthorAgentId],
         [ViewCount],[HelpfulCount],[NotHelpfulCount],[PublishedAt],[Version],
         [CreationDate],[CreatedBy],[LastUpdateDate],[IsDeleted])
    VALUES
        (N'{ArticleId}',
         N'Gateway credential encryption - how it works',
         N'{Slug}',
         N'{body}',
         {CategoryId},
         N'[""encryption"",""data-protection"",""certificates"",""payquicker"",""i-payout"",""runbook""]',
         2,                      -- Internal: staff only, never public
         N'{Actor}',
         0, 0, 0,
         SYSUTCDATETIME(), 1,
         SYSUTCDATETIME(), N'{Actor}', SYSUTCDATETIME(), 0);
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM [KbArticles] WHERE [Slug] = N'{Slug}';");

            // La categoría sólo se borra si quedó vacía: si alguien escribió más artículos de
            // infraestructura ahí, borrarla se los llevaría por delante.
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [KbArticles] WHERE [CategoryId] = {CategoryId})
   AND NOT EXISTS (SELECT 1 FROM [Tickets] WHERE [CategoryId] = {CategoryId})
    DELETE FROM [TicketCategories] WHERE [Id] = {CategoryId} AND [CreatedBy] = N'{Actor}';");
        }

        /// <summary>
        /// Cuerpo del artículo. En inglés, igual que el resto de la KB y de la UI admin.
        /// </summary>
        private const string ArticleBodyHtml = @"
<h3>What this protects</h3>
<p>Payout gateway credentials - PayQuicker client id and secret, the i-Payout merchant GUID and password, the company funding account token, and the admin portal sign-in details. They live in the <strong>ApiCredentials</strong> table and are stored encrypted, never in plain text.</p>
<p>Two services touch them: <strong>AdminAPI</strong> writes them (someone fills the form at Admin &rarr; Billing &rarr; API Credentials) and <strong>Billing</strong> reads them when a payout runs. That split is why the setup below matters - both processes must be able to derive the same keys.</p>

<h3>How it works: two layers</h3>
<pre>
  secret                key ring key              certificate
  (ApiCredentials)      (DataProtectionKeys)      (outside the database)
       |                        |                        |
       +---- encrypted with ----+                        |
                                +------ wrapped with ----+
</pre>
<ol>
  <li><strong>The secret</strong> is encrypted with AES-256-CBC + HMAC-SHA256 by ASP.NET Core Data Protection and stored with an <code>ENC:</code> prefix.</li>
  <li><strong>The key</strong> that did the encrypting lives in the <code>DataProtectionKeys</code> table of the same database both services already share.</li>
  <li><strong>That key is itself wrapped</strong> with an X.509 certificate. The database only ever holds the wrapped form.</li>
</ol>
<p>The point of layer 3: a database backup on its own is not enough to decrypt anything. Whoever restores it still needs the certificate private key, which is kept outside the database. Two factors, deliberately separated.</p>

<h3>Where everything lives</h3>
<table border=""1"" cellpadding=""6"" cellspacing=""0"">
  <tr><th>Item</th><th>Location</th><th>Notes</th></tr>
  <tr><td>Encrypted secrets</td><td><code>ApiCredentials</code> table</td><td>Values start with <code>ENC:CfDJ8</code></td></tr>
  <tr><td>Key ring</td><td><code>DataProtectionKeys</code> table</td><td>Same database as the app. Rotates itself every 90 days</td></tr>
  <tr><td>Certificate</td><td>Windows cert store, or a PFX file</td><td>Configured per host, must be identical on AdminAPI and Billing</td></tr>
  <tr><td>Application name</td><td><code>MLMConqueror.GatewayCredentials.v1</code></td><td>Hardcoded. Changing it makes existing ciphertext unreadable</td></tr>
</table>

<h3>Configuration</h3>
<p>Both AdminAPI and Billing read the same keys. Prefer <code>Thumbprint</code> in production - the private key never touches the application disk:</p>
<pre>
""DataProtection"": {
  ""Certificate"": {
    ""Thumbprint"": ""&lt;cert thumbprint in LocalMachine\My&gt;"",
    ""Path"": """",
    ""Password"": """"
  }
}
</pre>
<p>Environment variable form, for container or systemd deployments:</p>
<pre>
DataProtection__Certificate__Thumbprint=...
</pre>
<p>In <strong>Development only</strong>, if the PFX path does not exist a self-signed certificate is generated automatically so a fresh clone runs without setup. This never happens in any other environment - see Troubleshooting.</p>

<h3>Setting up a new environment</h3>
<ol>
  <li>Obtain or issue a certificate for key wrapping. It needs a private key and the Key Encipherment usage. A 10 year validity is reasonable - when it expires the key ring can no longer be unwrapped.</li>
  <li>Install it on both the AdminAPI and Billing hosts (same certificate, not one each).</li>
  <li>Set <code>DataProtection:Certificate:Thumbprint</code> on both.</li>
  <li>Start both services. The <code>DataProtectionKeys</code> table fills itself on first use.</li>
  <li>Enter the gateway credentials at Admin &rarr; Billing &rarr; API Credentials.</li>
  <li>Verify: run a payout in sandbox. If Billing can read the credential, the wiring is correct.</li>
</ol>

<h3>Rotating the certificate</h3>
<p>Existing key ring entries stay wrapped with the old certificate, so it cannot simply be swapped:</p>
<ol>
  <li>Install the new certificate alongside the old one on both hosts.</li>
  <li>Point <code>DataProtection:Certificate:Thumbprint</code> at the new one.</li>
  <li>Add the old one under <code>DataProtection:Certificate:Retired:0:Thumbprint</code> so existing keys can still be unwrapped.</li>
  <li>Restart both services. New keys are wrapped with the new certificate; old keys keep working.</li>
  <li>Only after every key has rotated (Data Protection rotates every 90 days, so allow a full cycle) may the retired entry be removed.</li>
</ol>

<h3>Backup and restore</h3>
<table border=""1"" cellpadding=""6"" cellspacing=""0"">
  <tr><th>If you lose...</th><th>Consequence</th></tr>
  <tr><td>The database</td><td>Restore it. The key ring comes back with it.</td></tr>
  <tr><td>The certificate</td><td><strong>Every stored credential becomes unrecoverable.</strong> The key ring cannot be unwrapped. Every gateway credential has to be re-entered by hand.</td></tr>
  <tr><td>Both</td><td>Same as above, plus the credentials themselves.</td></tr>
</table>
<p>So: back the certificate up somewhere that is not the database, and restrict who can read it. Anyone with the certificate and a database backup can read every gateway secret.</p>

<h3>Troubleshooting</h3>
<table border=""1"" cellpadding=""6"" cellspacing=""0"">
  <tr><th>Symptom</th><th>Cause</th><th>Fix</th></tr>
  <tr><td>Service will not start: ""needs a certificate""</td><td>Neither Thumbprint nor Path configured</td><td>Set one. The service refuses to start rather than fail on the first payout.</td></tr>
  <tr><td>Service will not start: ""certificate was not found at...""</td><td>PFX path configured but missing, outside Development</td><td>Restore it from the secret store. It is never regenerated - a fresh certificate could not unwrap the existing key ring.</td></tr>
  <tr><td><code>PAYQUICKER_CREDENTIAL_UNDECRYPTABLE</code></td><td>AdminAPI and Billing are not using the same certificate, or the stored value predates real encryption</td><td>Confirm both thumbprints match, then re-enter the credential.</td></tr>
  <tr><td><code>EWALLET_CREDENTIAL_UNDECRYPTABLE</code></td><td>Same, for i-Payout</td><td>Same.</td></tr>
  <tr><td><code>SECRET_ALREADY_PREFIXED</code> on save</td><td>A caller sent a value starting with <code>ENC:</code></td><td>Send the secret in plain text over TLS. The server encrypts it; the caller must not.</td></tr>
</table>

<h3>Things that will break it</h3>
<ul>
  <li>Changing the application name or the protector purpose in code. Both are constants on <code>GatewayCredentialProtector</code> and are load bearing.</li>
  <li>Giving AdminAPI and Billing different certificates. They will both start, and every payout will fail to read its credential.</li>
  <li>Deleting rows from <code>DataProtectionKeys</code>. Anything encrypted with a deleted key is gone.</li>
  <li>Letting the certificate expire without rotating first.</li>
</ul>

<h3>How to tell a secret is really encrypted</h3>
<p>Real ciphertext begins with <code>ENC:CfDJ8</code>. <code>CfDJ8</code> is the base64url form of the Data Protection magic header. A value that starts with <code>ENC:</code> but not <code>CfDJ8</code> is plain text with a prefix stuck on it - that was the behaviour of an earlier implementation and it is not encryption. Query to check:</p>
<pre>
SELECT ServiceKey, Environment,
       CASE WHEN ApiKeyEncrypted LIKE 'ENC:CfDJ8%' THEN 'encrypted'
            WHEN ApiKeyEncrypted IS NULL          THEN 'not set'
            ELSE 'NOT ENCRYPTED - re-enter it' END AS ApiKeyState
FROM ApiCredentials;
</pre>
";
    }
}
