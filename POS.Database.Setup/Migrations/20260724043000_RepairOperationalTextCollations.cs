using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Database.Setup.Migrations
{
    public partial class RepairOperationalTextCollations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[ItemParents] ALTER COLUMN [PrintName] " +
                "nvarchar(50) COLLATE Latin1_General_100_CI_AS_SC NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[ItemParents] ALTER COLUMN [BaseUom] " +
                "nvarchar(20) COLLATE Latin1_General_100_CI_AS_SC NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[PoLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE Latin1_General_100_CI_AS_SC NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[GrnLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE Latin1_General_100_CI_AS_SC NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[SalesLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE Latin1_General_100_CI_AS_SC NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[ItemParents] ALTER COLUMN [PrintName] " +
                "nvarchar(50) COLLATE DATABASE_DEFAULT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[ItemParents] ALTER COLUMN [BaseUom] " +
                "nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[PoLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[GrnLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE [dbo].[SalesLines] ALTER COLUMN [Uom] " +
                "nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL;");
        }
    }
}
