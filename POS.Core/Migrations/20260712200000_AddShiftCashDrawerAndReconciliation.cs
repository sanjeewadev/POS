using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260712200000_AddShiftCashDrawerAndReconciliation")]
    public class AddShiftCashDrawerAndReconciliation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize historical terminal/status text before adding the one-open-shift
            // database protection. If older code left more than one open shift for the
            // same terminal, keep the newest row open and close the older invalid rows.
            migrationBuilder.Sql(@"
UPDATE ShiftSessions
SET TerminalNo = upper(trim(TerminalNo)),
    Status = CASE lower(trim(Status))
        WHEN 'open' THEN 'Open'
        WHEN 'closing' THEN 'Closing'
        WHEN 'closed' THEN 'Closed'
        ELSE trim(Status)
    END;");

            migrationBuilder.Sql(@"
UPDATE ShiftSessions
SET Status = 'Closed',
    EndTime = COALESCE(EndTime, datetime('now', 'localtime'))
WHERE Status IN ('Open', 'Closing')
  AND Id NOT IN (
      SELECT MAX(Id)
      FROM ShiftSessions
      WHERE Status IN ('Open', 'Closing')
      GROUP BY TerminalNo
  );");

            // Old Float In/Out code changed OpeningCash and also wrote a CashMovement.
            // Normalize historical rows once so the new authoritative formula counts
            // opening cash and movements exactly once.
            migrationBuilder.Sql(@"
UPDATE ShiftSessions
SET OpeningCash =
    CASE
        WHEN (
            OpeningCash
            - COALESCE((
                SELECT SUM(Amount)
                FROM CashMovements
                WHERE CashMovements.ShiftSessionId = ShiftSessions.Id
                  AND lower(trim(MovementType)) = 'paid in'
                  AND lower(trim(ReasonCategory)) = 'opening float'
            ), 0)
            + COALESCE((
                SELECT SUM(Amount)
                FROM CashMovements
                WHERE CashMovements.ShiftSessionId = ShiftSessions.Id
                  AND lower(trim(MovementType)) = 'paid out'
                  AND lower(trim(ReasonCategory)) = 'float out / safe drop'
            ), 0)
        ) < 0 THEN 0
        ELSE ROUND(
            OpeningCash
            - COALESCE((
                SELECT SUM(Amount)
                FROM CashMovements
                WHERE CashMovements.ShiftSessionId = ShiftSessions.Id
                  AND lower(trim(MovementType)) = 'paid in'
                  AND lower(trim(ReasonCategory)) = 'opening float'
            ), 0)
            + COALESCE((
                SELECT SUM(Amount)
                FROM CashMovements
                WHERE CashMovements.ShiftSessionId = ShiftSessions.Id
                  AND lower(trim(MovementType)) = 'paid out'
                  AND lower(trim(ReasonCategory)) = 'float out / safe drop'
            ), 0),
            2)
    END;");

            migrationBuilder.Sql(@"
UPDATE CashMovements
SET ReferenceVoucherNo = 'LEGACY-CM-' || printf('%06d', Id)
WHERE trim(COALESCE(ReferenceVoucherNo, '')) = '';");

            migrationBuilder.CreateTable(
                name: "ShiftCloseSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CloseToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZReportNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedSaleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerReturnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GrossSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CardTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChequeTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftVoucherTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerCreditTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherTenderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidInTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FloatInTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidOutTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FloatOutTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashRefundTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CountedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VarianceNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftCloseSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftCloseSnapshots_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawerEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShiftSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminalNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SalesHeaderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CashMovementId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashDrawerEvents_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerEvents_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerEvents_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_OneOpenPerTerminal",
                table: "ShiftSessions",
                column: "TerminalNo",
                unique: true,
                filter: "\"Status\" IN ('Open', 'Closing')");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_TerminalNo_Status",
                table: "ShiftSessions",
                columns: new[] { "TerminalNo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_StartTime",
                table: "ShiftSessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_EndTime",
                table: "ShiftSessions",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ShiftSessionId_Timestamp",
                table: "CashMovements",
                columns: new[] { "ShiftSessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_MovementType",
                table: "CashMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReasonCategory",
                table: "CashMovements",
                column: "ReasonCategory");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_ShiftSessionId",
                table: "ShiftCloseSnapshots",
                column: "ShiftSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_CloseToken",
                table: "ShiftCloseSnapshots",
                column: "CloseToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_ZReportNo",
                table: "ShiftCloseSnapshots",
                column: "ZReportNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_TerminalNo_ClosedAt",
                table: "ShiftCloseSnapshots",
                columns: new[] { "TerminalNo", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_ShiftSessionId_RequestedAtUtc",
                table: "CashDrawerEvents",
                columns: new[] { "ShiftSessionId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_TerminalNo_RequestedAtUtc",
                table: "CashDrawerEvents",
                columns: new[] { "TerminalNo", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_EventType",
                table: "CashDrawerEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_SalesHeaderId",
                table: "CashDrawerEvents",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_CashMovementId",
                table: "CashDrawerEvents",
                column: "CashMovementId");

            // This migration is intentionally handwritten and has no generated
            // migration designer/target model. Use raw SQL for sequence seeding so
            // EF Core does not require DocumentSequences to be present in the
            // migration target model while generating SQLite commands.
            migrationBuilder.Sql(@"
INSERT OR IGNORE INTO DocumentSequences
    (DocumentType, Prefix, NextSequenceNumber, PaddingLength, UpdatedAt)
VALUES
    ('PAIDIN', 'PI-', 1, 6, '2026-01-01 00:00:00'),
    ('PAIDOUT', 'POT-', 1, 6, '2026-01-01 00:00:00'),
    ('ZREPORT', 'Z-', 1, 6, '2026-01-01 00:00:00');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CashDrawerEvents");
            migrationBuilder.DropTable(name: "ShiftCloseSnapshots");

            migrationBuilder.DropIndex(name: "IX_ShiftSessions_OneOpenPerTerminal", table: "ShiftSessions");
            migrationBuilder.DropIndex(name: "IX_ShiftSessions_TerminalNo_Status", table: "ShiftSessions");
            migrationBuilder.DropIndex(name: "IX_ShiftSessions_StartTime", table: "ShiftSessions");
            migrationBuilder.DropIndex(name: "IX_ShiftSessions_EndTime", table: "ShiftSessions");
            migrationBuilder.DropIndex(name: "IX_CashMovements_ShiftSessionId_Timestamp", table: "CashMovements");
            migrationBuilder.DropIndex(name: "IX_CashMovements_MovementType", table: "CashMovements");
            migrationBuilder.DropIndex(name: "IX_CashMovements_ReasonCategory", table: "CashMovements");

            migrationBuilder.Sql(@"
DELETE FROM DocumentSequences
WHERE DocumentType IN ('PAIDIN', 'PAIDOUT', 'ZREPORT');");
        }
    }
}
