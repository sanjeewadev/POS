using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260712080000_AddSalesDocumentAudit")]
    public class AddSalesDocumentAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "SalesDocumentAudits" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SalesDocumentAudits" PRIMARY KEY AUTOINCREMENT,
                    "SalesHeaderId" INTEGER NOT NULL,
                    "DocumentType" TEXT COLLATE NOCASE NOT NULL,
                    "DocumentNumber" TEXT COLLATE NOCASE NOT NULL,
                    "EventType" TEXT COLLATE NOCASE NOT NULL,
                    "CopyNumber" INTEGER NOT NULL,
                    "IsSuccessful" INTEGER NOT NULL,
                    "OccurredAtUtc" TEXT NOT NULL,
                    "PerformedBy" TEXT NOT NULL,
                    "TerminalNo" TEXT COLLATE NOCASE NOT NULL,
                    "PrinterName" TEXT NOT NULL,
                    "ErrorMessage" TEXT NOT NULL,
                    CONSTRAINT "FK_SalesDocumentAudits_SalesHeaders_SalesHeaderId"
                        FOREIGN KEY ("SalesHeaderId")
                        REFERENCES "SalesHeaders" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX "IX_SalesDocumentAudits_SalesHeaderId"
                ON "SalesDocumentAudits" ("SalesHeaderId");

                CREATE INDEX "IX_SalesDocumentAudits_DocumentType"
                ON "SalesDocumentAudits" ("DocumentType");

                CREATE INDEX "IX_SalesDocumentAudits_DocumentNumber"
                ON "SalesDocumentAudits" ("DocumentNumber");

                CREATE INDEX "IX_SalesDocumentAudits_EventType"
                ON "SalesDocumentAudits" ("EventType");

                CREATE INDEX "IX_SalesDocumentAudits_OccurredAtUtc"
                ON "SalesDocumentAudits" ("OccurredAtUtc");

                CREATE INDEX "IX_SalesDocumentAudits_SalesHeaderId_DocumentType_IsSuccessful"
                ON "SalesDocumentAudits" ("SalesHeaderId", "DocumentType", "IsSuccessful");

                DROP INDEX IF EXISTS "IX_SalesHeaders_TaxInvoiceNo";

                CREATE UNIQUE INDEX "IX_SalesHeaders_TaxInvoiceNo"
                ON "SalesHeaders" ("TaxInvoiceNo")
                WHERE "TaxInvoiceNo" IS NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "SalesDocumentAudits";

                DROP INDEX IF EXISTS "IX_SalesHeaders_TaxInvoiceNo";

                CREATE INDEX "IX_SalesHeaders_TaxInvoiceNo"
                ON "SalesHeaders" ("TaxInvoiceNo");
                """);
        }
    }
}
