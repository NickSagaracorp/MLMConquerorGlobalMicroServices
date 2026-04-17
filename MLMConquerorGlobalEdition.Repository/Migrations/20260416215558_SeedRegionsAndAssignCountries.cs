using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SeedRegionsAndAssignCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Insert the 5 billing regions ──────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO Regions (Name, Code, Description, IsActive, SortOrder, CreatedBy, CreationDate)
                VALUES
                ('North America',        'NA',   'United States and Canada',                                       1, 1, 'system', GETUTCDATE()),
                ('Latin America',        'LATAM','Mexico, Central America, South America and Caribbean',           1, 2, 'system', GETUTCDATE()),
                ('Europe',               'EU',   'European countries including Russia and Turkey',                 1, 3, 'system', GETUTCDATE()),
                ('Asia Pacific',         'APAC', 'Asia and Pacific countries including Australia and New Zealand', 1, 4, 'system', GETUTCDATE()),
                ('Middle East & Africa', 'MEA',  'Middle Eastern and African countries',                          1, 5, 'system', GETUTCDATE());
            ");

            // ── 2. Assign countries to regions by ISO2 code ──────────────────
            //        Uses subquery so IDs are resolved at runtime.

            // North America
            migrationBuilder.Sql(@"
                UPDATE Countries
                SET RegionId = (SELECT Id FROM Regions WHERE Code = 'NA')
                WHERE Iso2 IN ('US','CA');
            ");

            // Latin America — Central America + Caribbean + South America
            migrationBuilder.Sql(@"
                UPDATE Countries
                SET RegionId = (SELECT Id FROM Regions WHERE Code = 'LATAM')
                WHERE Iso2 IN (
                    'MX','GT','BZ','HN','SV','NI','CR','PA',
                    'CU','JM','HT','DO','TT','BB','LC','VC','GD','AG','DM','KN','BS',
                    'CO','VE','GY','SR','BR','EC','PE','BO','PY','CL','AR','UY'
                );
            ");

            // Europe — including Caucasus (AM, AZ, GE) and Turkey (TR)
            migrationBuilder.Sql(@"
                UPDATE Countries
                SET RegionId = (SELECT Id FROM Regions WHERE Code = 'EU')
                WHERE Iso2 IN (
                    'AD','AL','AM','AT','AZ','BA','BE','BG','BY','CH',
                    'CY','CZ','DE','DK','EE','ES','FI','FR','GB','GE',
                    'GR','HR','HU','IE','IS','IT','LI','LT','LU','LV',
                    'MC','MD','ME','MT','NL','NO','PL','PT','RO','RS',
                    'RU','SE','SI','SK','SM','TR','UA'
                );
            ");

            // Asia Pacific — East, Southeast, South, Central Asia + Oceania
            migrationBuilder.Sql(@"
                UPDATE Countries
                SET RegionId = (SELECT Id FROM Regions WHERE Code = 'APAC')
                WHERE Iso2 IN (
                    'CN','JP','TW','MN',
                    'ID','MY','PH','SG','TH','VN','MM','LA','KH','BN','TL',
                    'IN','PK','BD','LK','NP','BT','MV',
                    'KZ','KG','TJ','TM','UZ',
                    'AU','NZ','FJ','PG','SB','VU','TO','WS','KI','MH','FM','PW','NR','TV',
                    'SC'
                );
            ");

            // Middle East & Africa
            migrationBuilder.Sql(@"
                UPDATE Countries
                SET RegionId = (SELECT Id FROM Regions WHERE Code = 'MEA')
                WHERE Iso2 IN (
                    'AE','AF','BH','IQ','IR','IL','JO','KW','LB','OM','QA','SA','SY','YE',
                    'DZ','EG','LY','MA','TN',
                    'BF','BJ','CV','GA','GM','GH','GN','GW','LR','ML','MR','NE','NG','SL','SN','ST','TG',
                    'AO','CD','CF','CG','CM','GQ','RW','BI',
                    'DJ','ER','ET','KE','KM','MG','MU','MW','MZ','SO','SS','TZ','UG',
                    'BW','LS','NA','SZ','ZA','ZM','ZW','TD','SD'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Countries SET RegionId = NULL;");
            migrationBuilder.Sql("DELETE FROM Regions WHERE Code IN ('NA','LATAM','EU','APAC','MEA');");
        }
    }
}
