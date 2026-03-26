using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MLMConquerorGlobalEdition.Repository.Context;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260326200000_RestoreEliteProduct")]
    public partial class RestoreEliteProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Upsert Travel Advantage Elite — restores the row if it was deleted or never seeded.
            migrationBuilder.Sql(@"
                MERGE INTO [Products] AS target
                USING (SELECT '00000003-prod-0000-0000-000000000003' AS Id) AS source
                ON target.[Id] = source.[Id]
                WHEN MATCHED THEN
                    UPDATE SET
                        [Name]               = 'Travel Advantage Elite',
                        [Description]        = 'Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.',
                        [ImageUrl]           = '',
                        [MonthlyFee]         = 99.00,
                        [SetupFee]           = 0.00,
                        [Price90Days]        = 0.00,
                        [Price180Days]       = 0.00,
                        [AnnualPrice]        = 0.00,
                        [MonthlyFeePromo]    = 0.00,
                        [SetupFeePromo]      = 0.00,
                        [QualificationPoins] = 6,
                        [QualificationPoinsPromo] = 0,
                        [IsActive]           = 1,
                        [IsDeleted]          = 0,
                        [DeletedAt]          = NULL,
                        [DeletedBy]          = NULL,
                        [CorporateFee]       = 0,
                        [JoinPageMembership] = 1,
                        [OldSystemProductId] = 3,
                        [MembershipLevelId]  = 3,
                        [LastUpdateDate]     = '2026-03-16T00:00:00.000',
                        [LastUpdateBy]       = 'migration-restore'
                WHEN NOT MATCHED THEN
                    INSERT ([Id],[Name],[Description],[ImageUrl],[MonthlyFee],[SetupFee],
                            [Price90Days],[Price180Days],[AnnualPrice],[MonthlyFeePromo],[SetupFeePromo],
                            [QualificationPoins],[QualificationPoinsPromo],
                            [IsActive],[IsDeleted],[DeletedAt],[DeletedBy],
                            [CorporateFee],[JoinPageMembership],[OldSystemProductId],[MembershipLevelId],
                            [CreationDate],[CreatedBy],[LastUpdateDate],[LastUpdateBy])
                    VALUES ('00000003-prod-0000-0000-000000000003',
                            'Travel Advantage Elite',
                            'Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.',
                            '', 99.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00,
                            6, 0, 1, 0, NULL, NULL, 0, 1, 3, 3,
                            '2026-03-16T00:00:00.000', 'migration-restore',
                            '2026-03-16T00:00:00.000', 'migration-restore');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Products]
                SET [IsDeleted] = 1, [DeletedAt] = GETUTCDATE(), [DeletedBy] = 'migration-restore-rollback'
                WHERE [Id] = '00000003-prod-0000-0000-000000000003';
            ");
        }
    }
}
