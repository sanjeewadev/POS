using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711090000_AddSalesAndReturnTaxSnapshots")]
    public class AddSalesAndReturnTaxSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 7B3 is additive only. Historical sales and return rows are
            // marked LegacyUnknown. No tax values are reconstructed from current
            // rates, item codes, GlobalVatRate, or legacy totals.
            migrationBuilder.Sql(
                """
                ALTER TABLE "SalesHeaders"
                ADD COLUMN "DocumentType" TEXT NOT NULL DEFAULT 'Receipt';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "TaxInvoiceNo" TEXT NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "IsVatRegisteredSale" INTEGER NOT NULL DEFAULT 0;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "SupplierTinSnapshot" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "SupplierVatNoSnapshot" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "CustomerTinSnapshot" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "CustomerVatNoSnapshot" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "CustomerAddressSnapshot" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "TaxableAmountTotal" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "TotalVatAmount" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "StandardRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "ZeroRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "ExemptAmount" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "OutOfScopeAmount" decimal(18,2) NULL;

                ALTER TABLE "SalesHeaders"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_SalesHeaders_DocumentType"
                ON "SalesHeaders" ("DocumentType");

                CREATE INDEX "IX_SalesHeaders_TaxInvoiceNo"
                ON "SalesHeaders" ("TaxInvoiceNo");

                CREATE INDEX "IX_SalesHeaders_IsVatRegisteredSale"
                ON "SalesHeaders" ("IsVatRegisteredSale");

                CREATE INDEX "IX_SalesHeaders_TaxSnapshotStatus"
                ON "SalesHeaders" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "SalesLines"
                ADD COLUMN "ItemTypeSnapshot" TEXT NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_SalesLines_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxRateId" INTEGER NULL
                    CONSTRAINT "FK_SalesLines_TaxRates_TaxRateId"
                    REFERENCES "TaxRates" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxCodeSnapshot" TEXT NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxNameSnapshot" TEXT NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxRatePercentSnapshot" decimal(7,4) NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "IsTaxInclusiveSnapshot" INTEGER NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxableAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "VatAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxInclusiveAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SalesLines"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_SalesLines_ItemTypeSnapshot"
                ON "SalesLines" ("ItemTypeSnapshot");

                CREATE INDEX "IX_SalesLines_TaxCategoryId"
                ON "SalesLines" ("TaxCategoryId");

                CREATE INDEX "IX_SalesLines_TaxRateId"
                ON "SalesLines" ("TaxRateId");

                CREATE INDEX "IX_SalesLines_TaxSnapshotStatus"
                ON "SalesLines" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "OriginalSalesHeaderId" INTEGER NULL
                    CONSTRAINT "FK_CustomerReturnHeaders_SalesHeaders_OriginalSalesHeaderId"
                    REFERENCES "SalesHeaders" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "DocumentType" TEXT NOT NULL DEFAULT 'Return';

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "CreditNoteNo" TEXT NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "TaxableAmountTotal" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "TotalVatAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "StandardRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "ZeroRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "ExemptAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "OutOfScopeAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnHeaders"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_CustomerReturnHeaders_OriginalSalesHeaderId"
                ON "CustomerReturnHeaders" ("OriginalSalesHeaderId");

                CREATE INDEX "IX_CustomerReturnHeaders_DocumentType"
                ON "CustomerReturnHeaders" ("DocumentType");

                CREATE INDEX "IX_CustomerReturnHeaders_CreditNoteNo"
                ON "CustomerReturnHeaders" ("CreditNoteNo");

                CREATE INDEX "IX_CustomerReturnHeaders_TaxSnapshotStatus"
                ON "CustomerReturnHeaders" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "SalesLineId" INTEGER NULL
                    CONSTRAINT "FK_CustomerReturnLines_SalesLines_SalesLineId"
                    REFERENCES "SalesLines" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "ItemBatchId" INTEGER NULL
                    CONSTRAINT "FK_CustomerReturnLines_ItemBatches_ItemBatchId"
                    REFERENCES "ItemBatches" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "ItemTypeSnapshot" TEXT NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_CustomerReturnLines_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxRateId" INTEGER NULL
                    CONSTRAINT "FK_CustomerReturnLines_TaxRates_TaxRateId"
                    REFERENCES "TaxRates" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxCodeSnapshot" TEXT NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxNameSnapshot" TEXT NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxRatePercentSnapshot" decimal(7,4) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "IsTaxInclusiveSnapshot" INTEGER NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxableAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "VatAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxInclusiveAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "OriginalTaxableAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "OriginalVatAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "OriginalTaxInclusiveAmount" decimal(18,2) NULL;

                ALTER TABLE "CustomerReturnLines"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_CustomerReturnLines_SalesLineId"
                ON "CustomerReturnLines" ("SalesLineId");

                CREATE INDEX "IX_CustomerReturnLines_ItemBatchId"
                ON "CustomerReturnLines" ("ItemBatchId");

                CREATE INDEX "IX_CustomerReturnLines_ItemTypeSnapshot"
                ON "CustomerReturnLines" ("ItemTypeSnapshot");

                CREATE INDEX "IX_CustomerReturnLines_TaxCategoryId"
                ON "CustomerReturnLines" ("TaxCategoryId");

                CREATE INDEX "IX_CustomerReturnLines_TaxRateId"
                ON "CustomerReturnLines" ("TaxRateId");

                CREATE INDEX "IX_CustomerReturnLines_TaxSnapshotStatus"
                ON "CustomerReturnLines" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "TaxableAmountTotal" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "TotalVatAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "StandardRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "ZeroRatedAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "ExemptAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "OutOfScopeAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnHeaders"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_SupplierReturnHeaders_TaxSnapshotStatus"
                ON "SupplierReturnHeaders" ("TaxSnapshotStatus");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_SupplierReturnLines_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxRateId" INTEGER NULL
                    CONSTRAINT "FK_SupplierReturnLines_TaxRates_TaxRateId"
                    REFERENCES "TaxRates" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxCategoryCodeSnapshot" TEXT NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxCodeSnapshot" TEXT NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxNameSnapshot" TEXT NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxRatePercentSnapshot" decimal(7,4) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "IsTaxInclusiveSnapshot" INTEGER NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxableAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "VatAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxInclusiveAmountSnapshot" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "OriginalTaxableAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "OriginalVatAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "OriginalTaxInclusiveAmount" decimal(18,2) NULL;

                ALTER TABLE "SupplierReturnLines"
                ADD COLUMN "TaxSnapshotStatus" TEXT NOT NULL DEFAULT 'LegacyUnknown';

                CREATE INDEX "IX_SupplierReturnLines_TaxCategoryId"
                ON "SupplierReturnLines" ("TaxCategoryId");

                CREATE INDEX "IX_SupplierReturnLines_TaxRateId"
                ON "SupplierReturnLines" ("TaxRateId");

                CREATE INDEX "IX_SupplierReturnLines_TaxSnapshotStatus"
                ON "SupplierReturnLines" ("TaxSnapshotStatus");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_SupplierReturnLines_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_SupplierReturnLines_TaxRateId";
                DROP INDEX IF EXISTS "IX_SupplierReturnLines_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_SupplierReturnHeaders_TaxSnapshotStatus";

                DROP INDEX IF EXISTS "IX_CustomerReturnLines_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_CustomerReturnLines_TaxRateId";
                DROP INDEX IF EXISTS "IX_CustomerReturnLines_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_CustomerReturnLines_ItemTypeSnapshot";
                DROP INDEX IF EXISTS "IX_CustomerReturnLines_ItemBatchId";
                DROP INDEX IF EXISTS "IX_CustomerReturnLines_SalesLineId";

                DROP INDEX IF EXISTS "IX_CustomerReturnHeaders_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_CustomerReturnHeaders_CreditNoteNo";
                DROP INDEX IF EXISTS "IX_CustomerReturnHeaders_DocumentType";
                DROP INDEX IF EXISTS "IX_CustomerReturnHeaders_OriginalSalesHeaderId";

                DROP INDEX IF EXISTS "IX_SalesLines_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_SalesLines_TaxRateId";
                DROP INDEX IF EXISTS "IX_SalesLines_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_SalesLines_ItemTypeSnapshot";

                DROP INDEX IF EXISTS "IX_SalesHeaders_TaxSnapshotStatus";
                DROP INDEX IF EXISTS "IX_SalesHeaders_IsVatRegisteredSale";
                DROP INDEX IF EXISTS "IX_SalesHeaders_TaxInvoiceNo";
                DROP INDEX IF EXISTS "IX_SalesHeaders_DocumentType";

                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "OriginalTaxInclusiveAmount";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "OriginalVatAmount";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "OriginalTaxableAmount";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxInclusiveAmountSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "VatAmountSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxableAmountSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "IsTaxInclusiveSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxRatePercentSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxNameSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxCodeSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxCategoryCodeSnapshot";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxRateId";
                ALTER TABLE "SupplierReturnLines" DROP COLUMN "TaxCategoryId";

                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "OutOfScopeAmount";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "ExemptAmount";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "ZeroRatedAmount";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "StandardRatedAmount";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "TotalVatAmount";
                ALTER TABLE "SupplierReturnHeaders" DROP COLUMN "TaxableAmountTotal";

                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "OriginalTaxInclusiveAmount";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "OriginalVatAmount";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "OriginalTaxableAmount";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxInclusiveAmountSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "VatAmountSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxableAmountSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "IsTaxInclusiveSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxRatePercentSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxNameSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxCodeSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxCategoryCodeSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxRateId";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "TaxCategoryId";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "ItemTypeSnapshot";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "ItemBatchId";
                ALTER TABLE "CustomerReturnLines" DROP COLUMN "SalesLineId";

                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "OutOfScopeAmount";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "ExemptAmount";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "ZeroRatedAmount";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "StandardRatedAmount";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "TotalVatAmount";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "TaxableAmountTotal";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "CreditNoteNo";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "DocumentType";
                ALTER TABLE "CustomerReturnHeaders" DROP COLUMN "OriginalSalesHeaderId";

                ALTER TABLE "SalesLines" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxInclusiveAmountSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "VatAmountSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxableAmountSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "IsTaxInclusiveSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxRatePercentSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxNameSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxCodeSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxCategoryCodeSnapshot";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxRateId";
                ALTER TABLE "SalesLines" DROP COLUMN "TaxCategoryId";
                ALTER TABLE "SalesLines" DROP COLUMN "ItemTypeSnapshot";

                ALTER TABLE "SalesHeaders" DROP COLUMN "TaxSnapshotStatus";
                ALTER TABLE "SalesHeaders" DROP COLUMN "OutOfScopeAmount";
                ALTER TABLE "SalesHeaders" DROP COLUMN "ExemptAmount";
                ALTER TABLE "SalesHeaders" DROP COLUMN "ZeroRatedAmount";
                ALTER TABLE "SalesHeaders" DROP COLUMN "StandardRatedAmount";
                ALTER TABLE "SalesHeaders" DROP COLUMN "TotalVatAmount";
                ALTER TABLE "SalesHeaders" DROP COLUMN "TaxableAmountTotal";
                ALTER TABLE "SalesHeaders" DROP COLUMN "CustomerAddressSnapshot";
                ALTER TABLE "SalesHeaders" DROP COLUMN "CustomerVatNoSnapshot";
                ALTER TABLE "SalesHeaders" DROP COLUMN "CustomerTinSnapshot";
                ALTER TABLE "SalesHeaders" DROP COLUMN "SupplierVatNoSnapshot";
                ALTER TABLE "SalesHeaders" DROP COLUMN "SupplierTinSnapshot";
                ALTER TABLE "SalesHeaders" DROP COLUMN "IsVatRegisteredSale";
                ALTER TABLE "SalesHeaders" DROP COLUMN "TaxInvoiceNo";
                ALTER TABLE "SalesHeaders" DROP COLUMN "DocumentType";
                """);
        }
    }
}
