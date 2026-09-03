using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Database.Setup.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDocumentSequenceLengthProperly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "DocumentSequences",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "DocumentSequences",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_100_CI_AS_SC",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldCollation: "Latin1_General_100_CI_AS_SC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "DocumentSequences",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "DocumentSequences",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                collation: "Latin1_General_100_CI_AS_SC",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_100_CI_AS_SC");
        }
    }
}
