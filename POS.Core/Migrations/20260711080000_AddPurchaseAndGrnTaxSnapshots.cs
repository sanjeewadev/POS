using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711080000_AddPurchaseAndGrnTaxSnapshots")]
    public class AddPurchaseAndGrnTaxSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 7B2 is additive only. Historical values remain null and are
            // explicitly marked LegacyUnknown rather than reconstructed from
            // legacy tax-code text or current tax rates.
            migrationBuilder.Sql(
                """
                ALTER TABLE "PoHeaders"
                ADD COLUMN "TaxableAmountTotal" decimal(18,2) NULL;

                ALTER TABLE "PoHeaders"
                ADD COLUMN "StandardRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "PoHeaders"
                ADD COLUMN "ZeroRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "PoHeaders"
                ADD COLUMN "ExemptAmount" decimal(18,2) NULL;

                ALTER TABLE "PoHeaders"
                ADD COLUMN "OutOfScopeAmount" decimal(18,2) NULL;

                ALTER TABLE "PoHeaders"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_PoHeaders_TaxSnapshotStatus"
                ON "PoHeaders" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "PoLines"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_PoLines_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxRateId" INTEGER NULL
                    CONSTRAINT "FK_PoLines_TaxRates_TaxRateId"
                    REFERENCES "TaxRates" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxCodeSnapshot" TEXT NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxNameSnapshot" TEXT NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxRatePercentSnapshot" decimal(7,4) NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "IsTaxInclusiveSnapshot" INTEGER NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxableAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "VatAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxInclusiveAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "PoLines"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_PoLines_TaxCategoryId"
                ON "PoLines" ("TaxCategoryId");

                CREATE INDEX "IX_PoLines_TaxRateId"
                ON "PoLines" ("TaxRateId");

                CREATE INDEX "IX_PoLines_TaxSnapshotStatus"
                ON "PoLines" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "GrnHeaders"
                ADD COLUMN "IsTaxInclusive" INTEGER NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "TaxableAmountTotal" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "StandardRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "ZeroRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "ExemptAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "OutOfScopeAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "FreightTaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "FreightTaxableAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "FreightVatAmount" decimal(18,2) NULL;

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "FreightTaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                ALTER TABLE "GrnHeaders"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_GrnHeaders_TaxSnapshotStatus"
                ON "GrnHeaders" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_GrnLines_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxRateId" INTEGER NULL
                    CONSTRAINT "FK_GrnLines_TaxRates_TaxRateId"
                    REFERENCES "TaxRates" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxCodeSnapshot" TEXT NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxNameSnapshot" TEXT NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxRatePercentSnapshot" decimal(7,4) NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "IsTaxInclusiveSnapshot" INTEGER NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxableAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "VatAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxInclusiveAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "GrnLines"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_GrnLines_TaxCategoryId"
                ON "GrnLines" ("TaxCategoryId");

                CREATE INDEX "IX_GrnLines_TaxRateId"
                ON "GrnLines" ("TaxRateId");

                CREATE INDEX "IX_GrnLines_TaxSnapshotStatus"
                ON "GrnLines" ("TaxSnapshotStatus");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_GrnLines_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_GrnLines_TaxRateId";
                DROP INDEX IF EXISTS "IX_GrnLines_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_GrnHeaders_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_PoLines_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_PoLines_TaxRateId";
                DROP INDEX IF EXISTS "IX_PoLines_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_PoHeaders_TaxSnapshotStatus";

                ALTER TABLE "GrnLines" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxInclusiveAmountSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "VatAmountSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxableAmountSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "IsTaxInclusiveSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxRatePercentSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxNameSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxCodeSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxCategoryCodeSnapshot";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxRateId";
                ALTER TABLE "GrnLines" DROP COLUMN "TaxCategoryId";

                ALTER TABLE "GrnHeaders" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "GrnHeaders" DROP COLUMN "FreightTaxSnapshotStatus";
                ALTER TABLE "GrnHeaders" DROP COLUMN "FreightVatAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "FreightTaxableAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "FreightTaxCategoryCodeSnapshot";
                ALTER TABLE "GrnHeaders" DROP COLUMN "OutOfScopeAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "ExemptAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "ZeroRatedAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "StandardRatedAmount";
                ALTER TABLE "GrnHeaders" DROP COLUMN "TaxableAmountTotal";
                ALTER TABLE "GrnHeaders" DROP COLUMN "IsTaxInclusive";

                ALTER TABLE "PoLines" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "PoLines" DROP COLUMN "TaxInclusiveAmountSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "VatAmountSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxableAmountSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "IsTaxInclusiveSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxRatePercentSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxNameSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxCodeSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxCategoryCodeSnapshot";
                ALTER TABLE "PoLines" DROP COLUMN "TaxRateId";
                ALTER TABLE "PoLines" DROP COLUMN "TaxCategoryId";

                ALTER TABLE "PoHeaders" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "PoHeaders" DROP COLUMN "OutOfScopeAmount";
                ALTER TABLE "PoHeaders" DROP COLUMN "ExemptAmount";
                ALTER TABLE "PoHeaders" DROP COLUMN "ZeroRatedAmount";
                ALTER TABLE "PoHeaders" DROP COLUMN "StandardRatedAmount";
                ALTER TABLE "PoHeaders" DROP COLUMN "TaxableAmountTotal";
                """);
        }
    }
}
