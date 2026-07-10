using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginLockoutAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsernameAttempted = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockoutEndUtc",
                table: "Users",
                column: "LockoutEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_ApplicationName",
                table: "LoginAuditEvents",
                column: "ApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_EventTimeUtc",
                table: "LoginAuditEvents",
                column: "EventTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_EventType",
                table: "LoginAuditEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_UserId",
                table: "LoginAuditEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_UsernameAttempted",
                table: "LoginAuditEvents",
                column: "UsernameAttempted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_Users_LockoutEndUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Users");
        }
    }
}
