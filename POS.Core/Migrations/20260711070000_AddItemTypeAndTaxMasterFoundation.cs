using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Core.Data;

#nullable disable

namespace POS.Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260711070000_AddItemTypeAndTaxMasterFoundation")]
    public class AddItemTypeAndTaxMasterFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "TaxCategories"
                (
                    "Id" INTEGER NOT NULL
                        CONSTRAINT "PK_TaxCategories"
                        PRIMARY KEY AUTOINCREMENT,

                    "CategoryCode" TEXT COLLATE NOCASE NOT NULL,
                    "CategoryName" TEXT NOT NULL,
                    "TreatmentType" TEXT NOT NULL,
                    "IsRateBased" INTEGER NOT NULL DEFAULT 0,
                    "IsActive" INTEGER NOT NULL DEFAULT 1,
                    "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    "UpdatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    "DeactivatedAt" TEXT NULL
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX
                    "IX_TaxCategories_CategoryCode"
                ON "TaxCategories" ("CategoryCode");

                CREATE INDEX
                    "IX_TaxCategories_TreatmentType"
                ON "TaxCategories" ("TreatmentType");

                CREATE INDEX
                    "IX_TaxCategories_IsActive"
                ON "TaxCategories" ("IsActive");

                CREATE INDEX
                    "IX_TaxCategories_DisplayOrder"
                ON "TaxCategories" ("DisplayOrder");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "TaxCategories"
                (
                    "Id",
                    "CategoryCode",
                    "CategoryName",
                    "TreatmentType",
                    "IsRateBased",
                    "IsActive",
                    "DisplayOrder",
                    "CreatedAt",
                    "UpdatedAt",
                    "DeactivatedAt"
                )
                VALUES
                (
                    1,
                    'STANDARD',
                    'Standard VAT',
                    'StandardRated',
                    1,
                    1,
                    10,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                ),
                (
                    2,
                    'ZERO',
                    'Zero Rated',
                    'ZeroRated',
                    0,
                    1,
                    20,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                ),
                (
                    3,
                    'EXEMPT',
                    'Exempt',
                    'Exempt',
                    0,
                    1,
                    30,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                ),
                (
                    4,
                    'OUT_OF_SCOPE',
                    'Out of Scope',
                    'OutOfScope',
                    0,
                    1,
                    40,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                );
                """);

            // SQLite supports additive columns directly. Nullable FK columns are
            // declared with inline REFERENCES so no existing table rebuild is needed.
            migrationBuilder.Sql(
                """
                ALTER TABLE "ItemParents"
                ADD COLUMN "ItemType" TEXT NOT NULL DEFAULT 'StockItem';

                ALTER TABLE "ItemParents"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_ItemParents_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                CREATE INDEX
                    "IX_ItemParents_ItemType"
                ON "ItemParents" ("ItemType");

                CREATE INDEX
                    "IX_ItemParents_TaxCategoryId"
                ON "ItemParents" ("TaxCategoryId");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "TaxRates"
                ADD COLUMN "TaxCategoryId" INTEGER NULL
                    CONSTRAINT "FK_TaxRates_TaxCategories_TaxCategoryId"
                    REFERENCES "TaxCategories" ("Id")
                    ON DELETE RESTRICT;

                ALTER TABLE "TaxRates"
                ADD COLUMN "EffectiveFrom" TEXT NULL;

                ALTER TABLE "TaxRates"
                ADD COLUMN "EffectiveTo" TEXT NULL;

                ALTER TABLE "TaxRates"
                ADD COLUMN "ChangeReason" TEXT NULL;

                ALTER TABLE "TaxRates"
                ADD COLUMN "CreatedBy" TEXT NULL;

                ALTER TABLE "TaxRates"
                ADD COLUMN "UpdatedBy" TEXT NULL;

                CREATE INDEX
                    "IX_TaxRates_TaxCategoryId"
                ON "TaxRates" ("TaxCategoryId");

                CREATE INDEX
                    "IX_TaxRates_TaxCategoryId_EffectiveFrom"
                ON "TaxRates" ("TaxCategoryId", "EffectiveFrom");

                CREATE INDEX
                    "IX_TaxRates_TaxCategoryId_IsActive"
                ON "TaxRates" ("TaxCategoryId", "IsActive");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "StoreSettings"
                ADD COLUMN "IsVatRegistered" INTEGER NOT NULL DEFAULT 0;

                ALTER TABLE "StoreSettings"
                ADD COLUMN "TaxpayerIdentificationNumber" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "StoreSettings"
                ADD COLUMN "VatRegistrationNumber" TEXT NOT NULL DEFAULT '';

                ALTER TABLE "StoreSettings"
                ADD COLUMN "TaxInvoicePrefix" TEXT NOT NULL DEFAULT 'TI';
                """);

            // Only the unambiguous current standard code is mapped automatically.
            // TAX-FREE and reduced/unknown codes remain unclassified for review.
            migrationBuilder.Sql(
                """
                UPDATE "TaxRates"
                SET "TaxCategoryId" =
                (
                    SELECT "Id"
                    FROM "TaxCategories"
                    WHERE "CategoryCode" = 'STANDARD'
                    LIMIT 1
                )
                WHERE UPPER(TRIM("TaxCode")) = 'VAT-STD';

                UPDATE "ItemParents"
                SET "TaxCategoryId" =
                (
                    SELECT "Id"
                    FROM "TaxCategories"
                    WHERE "CategoryCode" = 'STANDARD'
                    LIMIT 1
                )
                WHERE UPPER(TRIM("TaxCode")) = 'VAT-STD';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ItemParents_ItemType";
                DROP INDEX IF EXISTS "IX_ItemParents_TaxCategoryId";

                DROP INDEX IF EXISTS "IX_TaxRates_TaxCategoryId";
                DROP INDEX IF EXISTS "IX_TaxRates_TaxCategoryId_EffectiveFrom";
                DROP INDEX IF EXISTS "IX_TaxRates_TaxCategoryId_IsActive";

                ALTER TABLE "ItemParents" DROP COLUMN "TaxCategoryId";
                ALTER TABLE "ItemParents" DROP COLUMN "ItemType";

                ALTER TABLE "TaxRates" DROP COLUMN "UpdatedBy";
                ALTER TABLE "TaxRates" DROP COLUMN "CreatedBy";
                ALTER TABLE "TaxRates" DROP COLUMN "ChangeReason";
                ALTER TABLE "TaxRates" DROP COLUMN "EffectiveTo";
                ALTER TABLE "TaxRates" DROP COLUMN "EffectiveFrom";
                ALTER TABLE "TaxRates" DROP COLUMN "TaxCategoryId";

                ALTER TABLE "StoreSettings" DROP COLUMN "TaxInvoicePrefix";
                ALTER TABLE "StoreSettings" DROP COLUMN "VatRegistrationNumber";
                ALTER TABLE "StoreSettings" DROP COLUMN "TaxpayerIdentificationNumber";
                ALTER TABLE "StoreSettings" DROP COLUMN "IsVatRegistered";

                DROP TABLE "TaxCategories";
                """);
        }
    }
}
