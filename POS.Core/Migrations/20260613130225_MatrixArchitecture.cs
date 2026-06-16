using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Core.Migrations
{
    /// <inheritdoc />
    public partial class MatrixArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdjustmentNo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TotalImpact = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SupplierName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ContactPerson = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Phone1 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Phone2 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    HasVat = table.Column<bool>(type: "INTEGER", nullable: false),
                    VatNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DefaultCreditDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UomCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    UomDescription = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AllowDecimals = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Mobile = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    PosPinHash = table.Column<string>(type: "TEXT", nullable: false),
                    PosPinSalt = table.Column<string>(type: "TEXT", nullable: false),
                    ForcePasswordReset = table.Column<bool>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeGroups_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubCategoryCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubCategoryName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrnNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierInvoiceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreditDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    GlobalBillDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    FreightAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetPayable = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrnHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Terms = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreditDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    GlobalBillDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetPayable = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReturnNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalInvoiceNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GrossCredit = table.Column<decimal>(type: "TEXT", nullable: false),
                    RestockingFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetCredit = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttributeGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeValues_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryAttributeGroups",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttributeGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryAttributeGroups", x => new { x.CategoryId, x.AttributeGroupId });
                    table.ForeignKey(
                        name: "FK_CategoryAttributeGroups_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryAttributeGroups_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemParents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    PrintName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseUom = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsScaleItem = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasBatchExpiry = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSerialized = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowCashierDiscount = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPurchaseLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSaleLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemParents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemParents_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemParents_SubCategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SubCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemParentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SkuCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VariantDescription = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AverageCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CostPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    RetailPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReorderLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemVariants_ItemParents_ItemParentId",
                        column: x => x.ItemParentId,
                        principalTable: "ItemParents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrnHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Uom = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OrderedQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    FocQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LandedCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    RetailPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrnLines_GrnHeaders_GrnHeaderId",
                        column: x => x.GrnHeaderId,
                        principalTable: "GrnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrnLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemPropertyMappings",
                columns: table => new
                {
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttributeGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttributeValueId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPropertyMappings", x => new { x.ItemVariantId, x.AttributeGroupId, x.AttributeValueId });
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_AttributeValues_AttributeValueId",
                        column: x => x.AttributeValueId,
                        principalTable: "AttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Uom = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OrderQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExpectedCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PoLines_PoHeaders_PoHeaderId",
                        column: x => x.PoHeaderId,
                        principalTable: "PoHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReturnHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReturnQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReasonCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HistoricalCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreditValue = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnLines_ReturnHeaders_ReturnHeaderId",
                        column: x => x.ReturnHeaderId,
                        principalTable: "ReturnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockAdjustmentHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemVariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ActualQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    VarianceQty = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReasonCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CostImpact = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockAdjustmentHeaders_StockAdjustmentHeaderId",
                        column: x => x.StockAdjustmentHeaderId,
                        principalTable: "StockAdjustmentHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeGroups_CategoryId",
                table: "AttributeGroups",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_AttributeGroupId",
                table: "AttributeValues",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAttributeGroups_AttributeGroupId",
                table: "CategoryAttributeGroups",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_SupplierId",
                table: "GrnHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_GrnHeaderId",
                table: "GrnLines",
                column: "GrnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_ItemVariantId",
                table: "GrnLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_CategoryId",
                table: "ItemParents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_SubCategoryId",
                table: "ItemParents",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPropertyMappings_AttributeGroupId",
                table: "ItemPropertyMappings",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPropertyMappings_AttributeValueId",
                table: "ItemPropertyMappings",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_ItemParentId",
                table: "ItemVariants",
                column: "ItemParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_SupplierId",
                table: "PoHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_ItemVariantId",
                table: "PoLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_PoHeaderId",
                table: "PoLines",
                column: "PoHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnHeaders_SupplierId",
                table: "ReturnHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnLines_ItemVariantId",
                table: "ReturnLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnLines_ReturnHeaderId",
                table: "ReturnLines",
                column: "ReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ItemVariantId",
                table: "StockAdjustmentLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_StockAdjustmentHeaderId",
                table: "StockAdjustmentLines",
                column: "StockAdjustmentHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId",
                table: "SubCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryAttributeGroups");

            migrationBuilder.DropTable(
                name: "GrnLines");

            migrationBuilder.DropTable(
                name: "ItemPropertyMappings");

            migrationBuilder.DropTable(
                name: "PoLines");

            migrationBuilder.DropTable(
                name: "ReturnLines");

            migrationBuilder.DropTable(
                name: "StockAdjustmentLines");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "GrnHeaders");

            migrationBuilder.DropTable(
                name: "AttributeValues");

            migrationBuilder.DropTable(
                name: "PoHeaders");

            migrationBuilder.DropTable(
                name: "ReturnHeaders");

            migrationBuilder.DropTable(
                name: "ItemVariants");

            migrationBuilder.DropTable(
                name: "StockAdjustmentHeaders");

            migrationBuilder.DropTable(
                name: "AttributeGroups");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "ItemParents");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
