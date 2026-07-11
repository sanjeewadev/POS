using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711100000_ExtendPriceChangeHistorySourceAudit")]
    public class ExtendPriceChangeHistorySourceAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 7B4 is additive only. Existing price-change history is
            // preserved and receives blank source-document values. Future GRN
            // price updates will populate these fields explicitly.
            migrationBuilder.Sql(
                """
                ALTER TABLE "PriceChangeHistories"
                ADD COLUMN "SourceDocumentType" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "PriceChangeHistories"
                ADD COLUMN "SourceDocumentId" INTEGER NULL;

                ALTER TABLE "PriceChangeHistories"
                ADD COLUMN "SourceDocumentLineId" INTEGER NULL;

                ALTER TABLE "PriceChangeHistories"
                ADD COLUMN "SourceDocumentNo" TEXT NOT NULL DEFAULT '';

                CREATE INDEX "IX_PriceChangeHistories_SourceDocumentNo"
                ON "PriceChangeHistories" ("SourceDocumentNo");

                CREATE INDEX "IX_PriceChangeHistories_SourceDocumentType"
                ON "PriceChangeHistories" ("SourceDocumentType");

                CREATE INDEX "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId"
                ON "PriceChangeHistories" ("SourceDocumentType", "SourceDocumentId");

                CREATE INDEX "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId_SourceDocumentLineId"
                ON "PriceChangeHistories" ("SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId_SourceDocumentLineId";
                DROP INDEX IF EXISTS "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId";
                DROP INDEX IF EXISTS "IX_PriceChangeHistories_SourceDocumentType";
                DROP INDEX IF EXISTS "IX_PriceChangeHistories_SourceDocumentNo";

                ALTER TABLE "PriceChangeHistories" DROP COLUMN "SourceDocumentNo";
                ALTER TABLE "PriceChangeHistories" DROP COLUMN "SourceDocumentLineId";
                ALTER TABLE "PriceChangeHistories" DROP COLUMN "SourceDocumentId";
                ALTER TABLE "PriceChangeHistories" DROP COLUMN "SourceDocumentType";
                """);
        }
    }
}
