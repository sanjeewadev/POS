using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POS.Database.Setup.Migrations
{
    /// <inheritdoc />
    public partial class Phase11B1_InitialSqlServerBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttributeGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BackupFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    BackupFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false, defaultValue: ""),
                    Success = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Checksum = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    MachineName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    TerminalNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, defaultValue: ""),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Birthday = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NicNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    BusinessRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    VatRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Retail", collation: "Latin1_General_100_CI_AS_SC"),
                    IsDiscountEligible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsCreditEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreditStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "None", collation: "Latin1_General_100_CI_AS_SC"),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCreditLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerGroupId = table.Column<int>(type: "int", nullable: true),
                    LoyaltyCardNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    LoyaltyPointsBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscountReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequiresAdminApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ManagerApprovalThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    DocumentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NextSequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PaddingLength = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.DocumentType);
                });

            migrationBuilder.CreateTable(
                name: "FreeIssueReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeIssueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ShopCost", collation: "Latin1_General_100_CI_AS_SC"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSupplier = table.Column<bool>(type: "bit", nullable: false),
                    RequiresClaimReference = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ManagerApprovalThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeIssueReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FreeIssueRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeIssueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ShopCost", collation: "Latin1_General_100_CI_AS_SC"),
                    FreeIssueReasonId = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupplierPromotionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ClaimValueMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Cost", collation: "Latin1_General_100_CI_AS_SC"),
                    FixedClaimValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliesToType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ItemVariant", collation: "Latin1_General_100_CI_AS_SC"),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubCategoryId = table.Column<int>(type: "int", nullable: true),
                    SubCategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemParentId = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxQtyPerInvoice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MaxQtyPerDay = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MaxValuePerInvoice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxValuePerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAdminApproval = table.Column<bool>(type: "bit", nullable: false),
                    AllowCashierWithoutApproval = table.Column<bool>(type: "bit", nullable: false),
                    ManagerApprovalThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeIssueRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstalledLicenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LicenseType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LicenseStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StoreId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: ""),
                    StoreName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    TerminalNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    MachineCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    IssuedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ImportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawLicenseJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    Signature = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledLicenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UsernameAttempted = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ApplicationName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    MachineName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredTerminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminalNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TerminalName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, defaultValue: ""),
                    MachineName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    MachineCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, defaultValue: "Main Store"),
                    IsCashierTerminal = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsBackOfficeAllowed = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LicenseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    LicenseExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LicenseLastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSaleAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredTerminals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCashSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    AdjustmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdjustmentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalIncreaseQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalDecreaseQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Brn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    TaxNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    AddressLine1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, defaultValue: ""),
                    AddressLine2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, defaultValue: ""),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    PostalCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Sri Lanka"),
                    Phone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    GlobalVatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    IsVatRegistered = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TaxpayerIdentificationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    VatRegistrationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    TaxInvoicePrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "TI"),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "LKR"),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Rs."),
                    InvoicePrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "INV"),
                    PurchaseOrderPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PO"),
                    QuotationPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "QT"),
                    ReceiptHeader = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    ReceiptFooter = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "Thank You! Come Again."),
                    InvoiceTerms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, defaultValue: ""),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Sri Lanka Standard Time"),
                    DateFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "dd/MM/yyyy"),
                    FinancialYearStartMonth = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ContactPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Phone1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    HasVat = table.Column<bool>(type: "bit", nullable: false),
                    VatNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DefaultCreditDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TreatmentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsRateBased = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerminalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TerminalName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    MachineName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Main Store"),
                    PrinterMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "WindowsSpooler"),
                    ReceiptPrinterName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    ReceiptPaperWidth = table.Column<int>(type: "int", nullable: false, defaultValue: 80),
                    AutoPrintReceipt = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReceiptCopies = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    EnableCashDrawer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DrawerKickCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "27,112,0,25,250"),
                    OpenDrawerAfterCashSale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScannerSuffixAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Enter"),
                    EnableScale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScaleComPort = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "COM1"),
                    ScaleBaudRate = table.Column<int>(type: "int", nullable: false, defaultValue: 9600),
                    EnablePoleDisplay = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PoleDisplayComPort = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "COM2"),
                    PoleWelcomeMessage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "WELCOME"),
                    EnableEftpos = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EftposProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    EftposPortOrIp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    AutoLockTimeoutMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UomCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    UomDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    AllowDecimals = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttributeGroupId = table.Column<int>(type: "int", nullable: false),
                    ValueName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeValues_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoryAttributeGroups",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AttributeGroupId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryAttributeGroups", x => new { x.CategoryId, x.AttributeGroupId });
                    table.ForeignKey(
                        name: "FK_CategoryAttributeGroups_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoryAttributeGroups_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SubCategoryCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SubCategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerMasterId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    CustomerReturnHeaderId = table.Column<int>(type: "int", nullable: true),
                    CustomerPaymentReceiptId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Open", collation: "Latin1_General_100_CI_AS_SC"),
                    ProcessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLedgers_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DiscountType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Percent", collation: "Latin1_General_100_CI_AS_SC"),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountReasonId = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AppliesToType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "All", collation: "Latin1_General_100_CI_AS_SC"),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubCategoryId = table.Column<int>(type: "int", nullable: true),
                    SubCategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemParentId = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "All", collation: "Latin1_General_100_CI_AS_SC"),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxValuePerInvoice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxValuePerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxQtyPerInvoice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MaxQtyPerDay = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequiresAdminApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ManagerApprovalThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllowBelowMinimumPrice = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountRules_DiscountReasons_DiscountReasonId",
                        column: x => x.DiscountReasonId,
                        principalTable: "DiscountReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReasonCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceVoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerMasterId = table.Column<int>(type: "int", nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerCompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerNicOrBrNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerIsDiscountEligible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CustomerIsCreditEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CustomerCreditStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    IsWholesaleSale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Receipt", collation: "Latin1_General_100_CI_AS_SC"),
                    TaxInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    CheckoutToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsVatRegisteredSale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SupplierTinSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "", collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierVatNoSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "", collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerTinSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "", collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerVatNoSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "", collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerAddressSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    TaxableAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandardRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ZeroRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutOfScopeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    GrossTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftVoucherIssueTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountTendered = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceReturned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesHeaders_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesHeaders_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftCloseSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    CloseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZReportNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedSaleCount = table.Column<int>(type: "int", nullable: false),
                    CustomerReturnCount = table.Column<int>(type: "int", nullable: false),
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
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VarianceNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "PoHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreditDays = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GlobalBillDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandardRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ZeroRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutOfScopeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    IsTaxInclusive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    RatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSystemDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemParents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    PrintName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SubCategoryId = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasureId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    BaseUom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "StockItem"),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsTaxInclusive = table.Column<bool>(type: "bit", nullable: false),
                    HasBatchTracking = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    HasExpiryTracking = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasBatchExpiry = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsScaleItem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSerialized = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AllowCashierDiscount = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsPurchaseLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSaleLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemParents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemParents_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemParents_SubCategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SubCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemParents_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemParents_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReceiptToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerMasterId = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    BankOrCardType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DestinationAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: true),
                    CashMovementId = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentReceipts_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawerEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    CashMovementId = table.Column<int>(type: "int", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "CashierCartSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerMasterId = table.Column<int>(type: "int", nullable: true),
                    CustomerCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerTypeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CustomerSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    IsWholesaleMode = table.Column<bool>(type: "bit", nullable: false),
                    InvoiceDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active", collation: "Latin1_General_100_CI_AS_SC"),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeldAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeldBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecalledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CancellationReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CancellationReasonText = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RecallCount = table.Column<int>(type: "int", nullable: false),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierCartSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierCartSessions_ShiftSessions_ShiftSessionId",
                        column: x => x.ShiftSessionId,
                        principalTable: "ShiftSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiftVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    VoucherAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Created", collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPrintedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoldDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoldSalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    SoldInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SoldCashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoldTerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RedeemedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedSalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    RedeemedInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RedeemedCashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RedeemedTerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RedeemedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ForfeitedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BlockReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StatusBeforeBlock = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftVouchers_SalesHeaders_RedeemedSalesHeaderId",
                        column: x => x.RedeemedSalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftVouchers_SalesHeaders_SoldSalesHeaderId",
                        column: x => x.SoldSalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesDocumentAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CopyNumber = table.Column<int>(type: "int", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    PrinterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesDocumentAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesDocumentAudits_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenderedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CardLastDigits = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    BankOrCardType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    GiftVoucherId = table.Column<int>(type: "int", nullable: true),
                    GiftVoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    GiftVoucherBarcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    GiftVoucherAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftVoucherForfeitedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftVoucherAuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesPayments_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrnNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreditDays = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GlobalBillDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreightAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsTaxInclusive = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandardRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ZeroRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutOfScopeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FreightTaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FreightTaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FreightVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FreightTaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Posted", collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrnHeaders_PoHeaders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PoHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrnHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemParentId = table.Column<int>(type: "int", nullable: false),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    VariantDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    AverageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaximumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemVariants_ItemParents_ItemParentId",
                        column: x => x.ItemParentId,
                        principalTable: "ItemParents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgerAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerMasterId = table.Column<int>(type: "int", nullable: false),
                    DebitLedgerId = table.Column<int>(type: "int", nullable: false),
                    CreditLedgerId = table.Column<int>(type: "int", nullable: false),
                    CustomerPaymentReceiptId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgerAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerLedgers_CreditLedgerId",
                        column: x => x.CreditLedgerId,
                        principalTable: "CustomerLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerLedgers_DebitLedgerId",
                        column: x => x.DebitLedgerId,
                        principalTable: "CustomerLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerMasters_CustomerMasterId",
                        column: x => x.CustomerMasterId,
                        principalTable: "CustomerMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerAllocations_CustomerPaymentReceipts_CustomerPaymentReceiptId",
                        column: x => x.CustomerPaymentReceiptId,
                        principalTable: "CustomerPaymentReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierCartLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashierCartSessionId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierCartLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierCartLines_CashierCartSessions_CashierCartSessionId",
                        column: x => x.CashierCartSessionId,
                        principalTable: "CashierCartSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    OriginalInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    OriginalSalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    ShiftSessionId = table.Column<int>(type: "int", nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccountCreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftVoucherRefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReplacementGiftVoucherId = table.Column<int>(type: "int", nullable: true),
                    ReplacementGiftVoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RefundMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Return", collation: "Latin1_General_100_CI_AS_SC"),
                    CreditNoteNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxableAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandardRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ZeroRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutOfScopeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnHeaders_GiftVouchers_ReplacementGiftVoucherId",
                        column: x => x.ReplacementGiftVoucherId,
                        principalTable: "GiftVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnHeaders_SalesHeaders_OriginalSalesHeaderId",
                        column: x => x.OriginalSalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    GrnHeaderId = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceDocument = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfterTransaction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierLedgers_GrnHeaders_GrnHeaderId",
                        column: x => x.GrnHeaderId,
                        principalTable: "GrnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedgers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    GrnHeaderId = table.Column<int>(type: "int", nullable: true),
                    OriginalInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GrossCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RestockingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableAmountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StandardRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ZeroRatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutOfScopeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnHeaders_GrnHeaders_GrnHeaderId",
                        column: x => x.GrnHeaderId,
                        principalTable: "GrnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpressItemLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    TabCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ButtonColorHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    TextColorHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    GridRow = table.Column<int>(type: "int", nullable: false),
                    GridColumn = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpressItemLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpressItemLayouts_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    InternalBatchBarcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "", collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BarcodePrintedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastBarcodePrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastBarcodePrintedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemBatches_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemPropertyMappings",
                columns: table => new
                {
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    AttributeGroupId = table.Column<int>(type: "int", nullable: false),
                    AttributeValueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPropertyMappings", x => new { x.ItemVariantId, x.AttributeGroupId, x.AttributeValueId });
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_AttributeGroups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "AttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_AttributeValues_AttributeValueId",
                        column: x => x.AttributeValueId,
                        principalTable: "AttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPropertyMappings_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    LastCostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    MinimumOrderQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PoLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoHeaderId = table.Column<int>(type: "int", nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    Uom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    OrderQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ExpectedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDiscountMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Amount", collation: "Latin1_General_100_CI_AS_SC"),
                    LineDiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsVatIncluded = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxRateId = table.Column<int>(type: "int", nullable: true),
                    TaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TaxCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRatePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    IsTaxInclusiveSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxInclusiveAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoLines_PoHeaders_PoHeaderId",
                        column: x => x.PoHeaderId,
                        principalTable: "PoHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PoLines_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiftVoucherTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiftVoucherId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    VoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    VoucherAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ForfeitedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StatusAfter = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: true),
                    SalesPaymentId = table.Column<int>(type: "int", nullable: true),
                    CustomerReturnHeaderId = table.Column<int>(type: "int", nullable: true),
                    ReferenceInvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceReturnNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftVoucherTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftVoucherTransactions_CustomerReturnHeaders_CustomerReturnHeaderId",
                        column: x => x.CustomerReturnHeaderId,
                        principalTable: "CustomerReturnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftVoucherTransactions_GiftVouchers_GiftVoucherId",
                        column: x => x.GiftVoucherId,
                        principalTable: "GiftVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftVoucherTransactions_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftVoucherTransactions_SalesPayments_SalesPaymentId",
                        column: x => x.SalesPaymentId,
                        principalTable: "SalesPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceDocument = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReferenceLineId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceChangeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceChangeNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    PriceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ChangeSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentLineId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VariantDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    BatchExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldMinimumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewMinimumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldRetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewRetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldWholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewWholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldMaximumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewMaximumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ChangeReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceChangeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceChangeHistories_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceChangeHistories_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemTypeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxRateId = table.Column<int>(type: "int", nullable: true),
                    TaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRatePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    IsTaxInclusiveSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxInclusiveAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ManualDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "None", collation: "Latin1_General_100_CI_AS_SC"),
                    IsManualDiscount = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPriceOverridden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PriceOverrideAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PriceOverrideApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceOverrideApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProfitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsReturned = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsGiftVoucherSale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GiftVoucherId = table.Column<int>(type: "int", nullable: true),
                    GiftVoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    GiftVoucherBarcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    IsFreeItem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FreeIssueRuleId = table.Column<int>(type: "int", nullable: true),
                    FreeIssueRuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeIssueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeReasonText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FreeApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FreeIssueAppliedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FreeIssueAppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FreeApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    FreeApprovedRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeIssueRuleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    FreeIssueSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    OriginalUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeIssueCostValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeIssueSellingValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsSupplierRecoverable = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupplierPromotionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierClaimId = table.Column<int>(type: "int", nullable: true),
                    SupplierClaimStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierClaimReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierClaimValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsRuleDiscount = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DiscountRuleId = table.Column<int>(type: "int", nullable: true),
                    DiscountRuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiscountReasonId = table.Column<int>(type: "int", nullable: true),
                    DiscountReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DiscountReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiscountRequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DiscountRequiresAdminApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DiscountApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscountApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLines_DiscountReasons_DiscountReasonId",
                        column: x => x.DiscountReasonId,
                        principalTable: "DiscountReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_DiscountRules_DiscountRuleId",
                        column: x => x.DiscountRuleId,
                        principalTable: "DiscountRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesLines_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockAdjustmentHeaderId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    SystemQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ActualQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    VarianceQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    LineRemarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockAdjustmentHeaders_StockAdjustmentHeaderId",
                        column: x => x.StockAdjustmentHeaderId,
                        principalTable: "StockAdjustmentHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrnHeaderId = table.Column<int>(type: "int", nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    PoLineId = table.Column<int>(type: "int", nullable: true),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDiscountMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Amount", collation: "Latin1_General_100_CI_AS_SC"),
                    LineDiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRatePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsVatIncluded = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LandedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxRateId = table.Column<int>(type: "int", nullable: true),
                    TaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TaxCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRatePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    IsTaxInclusiveSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxInclusiveAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    UpdateSellingPrices = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CurrentRetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewRetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentWholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewWholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentMinimumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewMinimumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentMaximumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewMaximumPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetailMarkupPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WholesaleMarkupPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Posted", collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                        name: "FK_GrnLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrnLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrnLines_PoLines_PoLineId",
                        column: x => x.PoLineId,
                        principalTable: "PoLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrnLines_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrnLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerReturnHeaderId = table.Column<int>(type: "int", nullable: false),
                    SalesLineId = table.Column<int>(type: "int", nullable: true),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    ItemDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotalRefund = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InventoryAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemTypeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxRateId = table.Column<int>(type: "int", nullable: true),
                    TaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRatePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    IsTaxInclusiveSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxInclusiveAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalTaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalTaxInclusiveAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_CustomerReturnHeaders_CustomerReturnHeaderId",
                        column: x => x.CustomerReturnHeaderId,
                        principalTable: "CustomerReturnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_SalesLines_SalesLineId",
                        column: x => x.SalesLineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FreeItemClaimLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: false),
                    SalesLineId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeIssueRuleId = table.Column<int>(type: "int", nullable: true),
                    FreeIssueRuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeReasonText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FreeIssueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "SupplierClaim", collation: "Latin1_General_100_CI_AS_SC"),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupplierPromotionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Uom = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "PCS", collation: "Latin1_General_100_CI_AS_SC"),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeIssueCostValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeIssueSellingValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClaimValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClaimStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending", collation: "Latin1_General_100_CI_AS_SC"),
                    ClaimReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettlementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SettlementReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WrittenOffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WrittenOffBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WriteOffReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FreeApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FreeApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FreeApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    FreeApprovedRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    FreeIssueAppliedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FreeIssueAppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FreeIssueRuleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    FreeIssueSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeItemClaimLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimLogs_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimLogs_SalesLines_SalesLineId",
                        column: x => x.SalesLineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesLineDiscountAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderId = table.Column<int>(type: "int", nullable: false),
                    SalesLineId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TerminalNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DiscountRuleId = table.Column<int>(type: "int", nullable: true),
                    DiscountRuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiscountReasonId = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotalAfterDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProfitAfterDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ItemVariantId = table.Column<int>(type: "int", nullable: true),
                    ItemBatchId = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    SkuCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ItemDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Uom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequiresAdminApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLineDiscountAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLineDiscountAudits_DiscountReasons_DiscountReasonId",
                        column: x => x.DiscountReasonId,
                        principalTable: "DiscountReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLineDiscountAudits_DiscountRules_DiscountRuleId",
                        column: x => x.DiscountRuleId,
                        principalTable: "DiscountRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLineDiscountAudits_SalesHeaders_SalesHeaderId",
                        column: x => x.SalesHeaderId,
                        principalTable: "SalesHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLineDiscountAudits_SalesLines_SalesLineId",
                        column: x => x.SalesLineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnHeaderId = table.Column<int>(type: "int", nullable: false),
                    GrnLineId = table.Column<int>(type: "int", nullable: true),
                    ItemVariantId = table.Column<int>(type: "int", nullable: false),
                    ItemBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    HistoricalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategoryId = table.Column<int>(type: "int", nullable: true),
                    TaxRateId = table.Column<int>(type: "int", nullable: true),
                    TaxCategoryCodeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_CI_AS_SC"),
                    TaxNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRatePercentSnapshot = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    IsTaxInclusiveSnapshot = table.Column<bool>(type: "bit", nullable: true),
                    TaxableAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VatAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxInclusiveAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalTaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalVatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OriginalTaxInclusiveAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxSnapshotStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "LegacyUnknown", collation: "Latin1_General_100_CI_AS_SC"),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    LineRemarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_GrnLines_GrnLineId",
                        column: x => x.GrnLineId,
                        principalTable: "GrnLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalTable: "ItemBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalTable: "ItemVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_SupplierReturnHeaders_ReturnHeaderId",
                        column: x => x.ReturnHeaderId,
                        principalTable: "SupplierReturnHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_TaxCategories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalTable: "TaxCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FreeItemClaimAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FreeItemClaimLogId = table.Column<int>(type: "int", nullable: false),
                    CustomerReturnLineId = table.Column<int>(type: "int", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ClaimValueReduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeItemClaimAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimAdjustments_CustomerReturnLines_CustomerReturnLineId",
                        column: x => x.CustomerReturnLineId,
                        principalTable: "CustomerReturnLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FreeItemClaimAdjustments_FreeItemClaimLogs_FreeItemClaimLogId",
                        column: x => x.FreeItemClaimLogId,
                        principalTable: "FreeItemClaimLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DocumentSequences",
                columns: new[] { "DocumentType", "NextSequenceNumber", "PaddingLength", "Prefix", "UpdatedAt" },
                values: new object[,]
                {
                    { "ADJ", 1, 5, "ADJ-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "GRN", 1, 5, "GRN-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "PAIDIN", 1, 6, "PI-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "PAIDOUT", 1, 6, "POT-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "PAY", 1, 6, "PAY-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "PCH", 1, 5, "PCH-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "PO", 1, 5, "PO-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "RTN", 1, 5, "RTN-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { "ZREPORT", 1, 6, "Z-", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Id", "AllowDecimals", "CreatedAt", "DeactivatedAt", "DisplayOrder", "IsActive", "UomCode", "UomDescription", "UpdatedAt" },
                values: new object[] { 1, false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 10, true, "PCS", "Pieces", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeGroups_DisplayOrder",
                table: "AttributeGroups",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeGroups_GroupName",
                table: "AttributeGroups",
                column: "GroupName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeGroups_IsDeactivated",
                table: "AttributeGroups",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_AttributeGroupId_DisplayOrder",
                table: "AttributeValues",
                columns: new[] { "AttributeGroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_AttributeGroupId_ValueName",
                table: "AttributeValues",
                columns: new[] { "AttributeGroupId", "ValueName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeValues_IsDeactivated",
                table: "AttributeValues",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_ActionType",
                table: "BackupHistory",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_CreatedAt",
                table: "BackupHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_MachineName",
                table: "BackupHistory",
                column: "MachineName");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_Success",
                table: "BackupHistory",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_TerminalNo",
                table: "BackupHistory",
                column: "TerminalNo");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_CashMovementId",
                table: "CashDrawerEvents",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_EventType",
                table: "CashDrawerEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_SalesHeaderId",
                table: "CashDrawerEvents",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_ShiftSessionId_RequestedAtUtc",
                table: "CashDrawerEvents",
                columns: new[] { "ShiftSessionId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerEvents_TerminalNo_RequestedAtUtc",
                table: "CashDrawerEvents",
                columns: new[] { "TerminalNo", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_CashierCartSessionId_LineNumber",
                table: "CashierCartLines",
                columns: new[] { "CashierCartSessionId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_ItemBatchId",
                table: "CashierCartLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartLines_ItemVariantId",
                table: "CashierCartLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ActiveOwner",
                table: "CashierCartSessions",
                columns: new[] { "TerminalNo", "ShiftSessionId", "CashierName" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CartToken",
                table: "CashierCartSessions",
                column: "CartToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CashierName_Status",
                table: "CashierCartSessions",
                columns: new[] { "CashierName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_CustomerMasterId",
                table: "CashierCartSessions",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ReferenceNo",
                table: "CashierCartSessions",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_SalesHeaderId",
                table: "CashierCartSessions",
                column: "SalesHeaderId",
                unique: true,
                filter: "\"SalesHeaderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_ShiftSessionId",
                table: "CashierCartSessions",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_TerminalNo_ShiftSessionId_Status",
                table: "CashierCartSessions",
                columns: new[] { "TerminalNo", "ShiftSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierCartSessions_UpdatedAtUtc",
                table: "CashierCartSessions",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_MovementType",
                table: "CashMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReasonCategory",
                table: "CashMovements",
                column: "ReasonCategory");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReferenceVoucherNo",
                table: "CashMovements",
                column: "ReferenceVoucherNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ShiftSessionId_Timestamp",
                table: "CashMovements",
                columns: new[] { "ShiftSessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_Timestamp",
                table: "CashMovements",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryCode",
                table: "Categories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryName",
                table: "Categories",
                column: "CategoryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DisplayOrder",
                table: "Categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsDeactivated",
                table: "Categories",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAttributeGroups_AttributeGroupId",
                table: "CategoryAttributeGroups",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CreditLedgerId",
                table: "CustomerLedgerAllocations",
                column: "CreditLedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CustomerMasterId",
                table: "CustomerLedgerAllocations",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_CustomerPaymentReceiptId",
                table: "CustomerLedgerAllocations",
                column: "CustomerPaymentReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerAllocations_DebitLedgerId_CreditLedgerId",
                table: "CustomerLedgerAllocations",
                columns: new[] { "DebitLedgerId", "CreditLedgerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerMasterId",
                table: "CustomerLedgers",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerPaymentReceiptId",
                table: "CustomerLedgers",
                column: "CustomerPaymentReceiptId",
                unique: true,
                filter: "CustomerPaymentReceiptId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerReturnHeaderId",
                table: "CustomerLedgers",
                column: "CustomerReturnHeaderId",
                unique: true,
                filter: "CustomerReturnHeaderId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_DocumentRef",
                table: "CustomerLedgers",
                column: "DocumentRef");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_DueDate",
                table: "CustomerLedgers",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_SalesHeaderId",
                table: "CustomerLedgers",
                column: "SalesHeaderId",
                unique: true,
                filter: "SalesHeaderId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_Status",
                table: "CustomerLedgers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_TransactionDate",
                table: "CustomerLedgers",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_TransactionType",
                table: "CustomerLedgers",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_Birthday",
                table: "CustomerMasters",
                column: "Birthday");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_BusinessRegistrationNumber",
                table: "CustomerMasters",
                column: "BusinessRegistrationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_CreditStatus",
                table: "CustomerMasters",
                column: "CreditStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_CustomerCode",
                table: "CustomerMasters",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_CustomerType",
                table: "CustomerMasters",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_FullName",
                table: "CustomerMasters",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_IsActive",
                table: "CustomerMasters",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_IsCreditEnabled",
                table: "CustomerMasters",
                column: "IsCreditEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_IsCreditLocked",
                table: "CustomerMasters",
                column: "IsCreditLocked");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_IsDiscountEligible",
                table: "CustomerMasters",
                column: "IsDiscountEligible");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_NicNumber",
                table: "CustomerMasters",
                column: "NicNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_Phone",
                table: "CustomerMasters",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMasters_VatRegistrationNumber",
                table: "CustomerMasters",
                column: "VatRegistrationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_CashMovementId",
                table: "CustomerPaymentReceipts",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_CustomerMasterId",
                table: "CustomerPaymentReceipts",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_PaymentDate",
                table: "CustomerPaymentReceipts",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_PaymentMethod",
                table: "CustomerPaymentReceipts",
                column: "PaymentMethod");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ReceiptNo",
                table: "CustomerPaymentReceipts",
                column: "ReceiptNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ReceiptToken",
                table: "CustomerPaymentReceipts",
                column: "ReceiptToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentReceipts_ShiftSessionId",
                table: "CustomerPaymentReceipts",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_CreditNoteNo",
                table: "CustomerReturnHeaders",
                column: "CreditNoteNo");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_DocumentType",
                table: "CustomerReturnHeaders",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_OriginalSalesHeaderId",
                table: "CustomerReturnHeaders",
                column: "OriginalSalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherId",
                table: "CustomerReturnHeaders",
                column: "ReplacementGiftVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReplacementGiftVoucherNo",
                table: "CustomerReturnHeaders",
                column: "ReplacementGiftVoucherNo");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_ReturnNo",
                table: "CustomerReturnHeaders",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnHeaders_TaxSnapshotStatus",
                table: "CustomerReturnHeaders",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_CustomerReturnHeaderId",
                table: "CustomerReturnLines",
                column: "CustomerReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_ItemBatchId",
                table: "CustomerReturnLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_ItemTypeSnapshot",
                table: "CustomerReturnLines",
                column: "ItemTypeSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_SalesLineId",
                table: "CustomerReturnLines",
                column: "SalesLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_TaxCategoryId",
                table: "CustomerReturnLines",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_TaxRateId",
                table: "CustomerReturnLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_TaxSnapshotStatus",
                table: "CustomerReturnLines",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_DisplayOrder",
                table: "DiscountReasons",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_IsActive",
                table: "DiscountReasons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_ReasonCode",
                table: "DiscountReasons",
                column: "ReasonCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_ReasonName",
                table: "DiscountReasons",
                column: "ReasonName");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_RequiresAdminApproval",
                table: "DiscountReasons",
                column: "RequiresAdminApproval");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_RequiresManagerApproval",
                table: "DiscountReasons",
                column: "RequiresManagerApproval");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_AllowBelowMinimumPrice",
                table: "DiscountRules",
                column: "AllowBelowMinimumPrice");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_AppliesToType",
                table: "DiscountRules",
                column: "AppliesToType");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_CategoryId",
                table: "DiscountRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_CustomerType",
                table: "DiscountRules",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_DiscountReasonId",
                table: "DiscountRules",
                column: "DiscountReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_DiscountType",
                table: "DiscountRules",
                column: "DiscountType");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_IsActive",
                table: "DiscountRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ItemParentId",
                table: "DiscountRules",
                column: "ItemParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ItemVariantId",
                table: "DiscountRules",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ReasonCode",
                table: "DiscountRules",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_RequiresAdminApproval",
                table: "DiscountRules",
                column: "RequiresAdminApproval");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_RequiresManagerApproval",
                table: "DiscountRules",
                column: "RequiresManagerApproval");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_RuleName",
                table: "DiscountRules",
                column: "RuleName");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_SubCategoryId",
                table: "DiscountRules",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ValidFrom",
                table: "DiscountRules",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRules_ValidTo",
                table: "DiscountRules",
                column: "ValidTo");

            migrationBuilder.CreateIndex(
                name: "IX_ExpressItemLayouts_ItemVariantId",
                table: "ExpressItemLayouts",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpressItemLayouts_TabCategory_GridRow_GridColumn",
                table: "ExpressItemLayouts",
                columns: new[] { "TabCategory", "GridRow", "GridColumn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueReasons_DisplayOrder",
                table: "FreeIssueReasons",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueReasons_FreeIssueType",
                table: "FreeIssueReasons",
                column: "FreeIssueType");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueReasons_IsActive",
                table: "FreeIssueReasons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueReasons_ReasonCode",
                table: "FreeIssueReasons",
                column: "ReasonCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueReasons_ReasonName",
                table: "FreeIssueReasons",
                column: "ReasonName");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_AppliesToType",
                table: "FreeIssueRules",
                column: "AppliesToType");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_Barcode",
                table: "FreeIssueRules",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_CategoryId",
                table: "FreeIssueRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_FreeIssueReasonId",
                table: "FreeIssueRules",
                column: "FreeIssueReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_FreeIssueType",
                table: "FreeIssueRules",
                column: "FreeIssueType");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_IsActive",
                table: "FreeIssueRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_ItemParentId",
                table: "FreeIssueRules",
                column: "ItemParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_ItemVariantId",
                table: "FreeIssueRules",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_RuleName",
                table: "FreeIssueRules",
                column: "RuleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_SkuCode",
                table: "FreeIssueRules",
                column: "SkuCode");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_SubCategoryId",
                table: "FreeIssueRules",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_SupplierId",
                table: "FreeIssueRules",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_ValidFrom",
                table: "FreeIssueRules",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_FreeIssueRules_ValidTo",
                table: "FreeIssueRules",
                column: "ValidTo");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimAdjustments_CustomerReturnLineId",
                table: "FreeItemClaimAdjustments",
                column: "CustomerReturnLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimAdjustments_FreeItemClaimLogId",
                table: "FreeItemClaimAdjustments",
                column: "FreeItemClaimLogId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_Barcode",
                table: "FreeItemClaimLogs",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_CancelledAt",
                table: "FreeItemClaimLogs",
                column: "CancelledAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_ClaimReferenceNo",
                table: "FreeItemClaimLogs",
                column: "ClaimReferenceNo");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_ClaimStatus",
                table: "FreeItemClaimLogs",
                column: "ClaimStatus");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_CreatedAt",
                table: "FreeItemClaimLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_FreeIssueRuleId",
                table: "FreeItemClaimLogs",
                column: "FreeIssueRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_FreeIssueType",
                table: "FreeItemClaimLogs",
                column: "FreeIssueType");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_FreeReasonCode",
                table: "FreeItemClaimLogs",
                column: "FreeReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_InvoiceDate",
                table: "FreeItemClaimLogs",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_InvoiceNo",
                table: "FreeItemClaimLogs",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_ItemBatchId",
                table: "FreeItemClaimLogs",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_ItemVariantId",
                table: "FreeItemClaimLogs",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_RejectedAt",
                table: "FreeItemClaimLogs",
                column: "RejectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SalesHeaderId",
                table: "FreeItemClaimLogs",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SalesLineId",
                table: "FreeItemClaimLogs",
                column: "SalesLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SettledAt",
                table: "FreeItemClaimLogs",
                column: "SettledAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SkuCode",
                table: "FreeItemClaimLogs",
                column: "SkuCode");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SubmittedAt",
                table: "FreeItemClaimLogs",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SupplierId",
                table: "FreeItemClaimLogs",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_SupplierName",
                table: "FreeItemClaimLogs",
                column: "SupplierName");

            migrationBuilder.CreateIndex(
                name: "IX_FreeItemClaimLogs_WrittenOffAt",
                table: "FreeItemClaimLogs",
                column: "WrittenOffAt");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_ActivatedAt",
                table: "GiftVouchers",
                column: "ActivatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_Barcode",
                table: "GiftVouchers",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_BatchNo",
                table: "GiftVouchers",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_CreatedAt",
                table: "GiftVouchers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_ExpiryDate",
                table: "GiftVouchers",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_RedeemedDate",
                table: "GiftVouchers",
                column: "RedeemedDate");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_RedeemedSalesHeaderId",
                table: "GiftVouchers",
                column: "RedeemedSalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_SoldSalesHeaderId",
                table: "GiftVouchers",
                column: "SoldSalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_Status",
                table: "GiftVouchers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_VoucherAmount",
                table: "GiftVouchers",
                column: "VoucherAmount");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVouchers_VoucherNo",
                table: "GiftVouchers",
                column: "VoucherNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_Barcode",
                table: "GiftVoucherTransactions",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_CustomerReturnHeaderId",
                table: "GiftVoucherTransactions",
                column: "CustomerReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_GiftVoucherId",
                table: "GiftVoucherTransactions",
                column: "GiftVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_ReferenceInvoiceNo",
                table: "GiftVoucherTransactions",
                column: "ReferenceInvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_ReferenceKey",
                table: "GiftVoucherTransactions",
                column: "ReferenceKey",
                unique: true,
                filter: "[ReferenceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_ReferenceReturnNo",
                table: "GiftVoucherTransactions",
                column: "ReferenceReturnNo");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_SalesHeaderId",
                table: "GiftVoucherTransactions",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_SalesPaymentId",
                table: "GiftVoucherTransactions",
                column: "SalesPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_TransactionDate",
                table: "GiftVoucherTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_TransactionType",
                table: "GiftVoucherTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_GiftVoucherTransactions_VoucherNo",
                table: "GiftVoucherTransactions",
                column: "VoucherNo");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_DueDate",
                table: "GrnHeaders",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_GrnNumber",
                table: "GrnHeaders",
                column: "GrnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_InvoiceDate",
                table: "GrnHeaders",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_PurchaseOrderId",
                table: "GrnHeaders",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_ReceivedDate",
                table: "GrnHeaders",
                column: "ReceivedDate");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_Status",
                table: "GrnHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_SupplierId",
                table: "GrnHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_SupplierId_SupplierInvoiceNo",
                table: "GrnHeaders",
                columns: new[] { "SupplierId", "SupplierInvoiceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrnHeaders_TaxSnapshotStatus",
                table: "GrnHeaders",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_BatchNo",
                table: "GrnLines",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_GrnHeaderId",
                table: "GrnLines",
                column: "GrnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_GrnHeaderId_ItemVariantId_BatchNo",
                table: "GrnLines",
                columns: new[] { "GrnHeaderId", "ItemVariantId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_ItemBatchId",
                table: "GrnLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_ItemVariantId",
                table: "GrnLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_LineStatus",
                table: "GrnLines",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_PoLineId",
                table: "GrnLines",
                column: "PoLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_TaxCategoryId",
                table: "GrnLines",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_TaxRateId",
                table: "GrnLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_TaxSnapshotStatus",
                table: "GrnLines",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_IsActive",
                table: "InstalledLicenses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_LicenseId",
                table: "InstalledLicenses",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_LicenseType",
                table: "InstalledLicenses",
                column: "LicenseType");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_MachineCode",
                table: "InstalledLicenses",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_StoreId",
                table: "InstalledLicenses",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_TerminalNo",
                table: "InstalledLicenses",
                column: "TerminalNo");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemBatchId",
                table: "InventoryTransactions",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ItemVariantId",
                table: "InventoryTransactions",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceDocument",
                table: "InventoryTransactions",
                column: "ReferenceDocument");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceDocument_ReferenceLineId",
                table: "InventoryTransactions",
                columns: new[] { "ReferenceDocument", "ReferenceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TransactionDate",
                table: "InventoryTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TransactionType",
                table: "InventoryTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_BatchNo",
                table: "ItemBatches",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ExpiryDate",
                table: "ItemBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_InternalBatchBarcode",
                table: "ItemBatches",
                column: "InternalBatchBarcode",
                unique: true,
                filter: "InternalBatchBarcode IS NOT NULL AND InternalBatchBarcode <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_IsDeactivated",
                table: "ItemBatches",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemVariantId",
                table: "ItemBatches",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemVariantId_BatchNo",
                table: "ItemBatches",
                columns: new[] { "ItemVariantId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ReceivedDate",
                table: "ItemBatches",
                column: "ReceivedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_CategoryId",
                table: "ItemParents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_HasBatchExpiry",
                table: "ItemParents",
                column: "HasBatchExpiry");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_HasBatchTracking",
                table: "ItemParents",
                column: "HasBatchTracking");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_HasExpiryTracking",
                table: "ItemParents",
                column: "HasExpiryTracking");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_IsDeactivated",
                table: "ItemParents",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_IsPurchaseLocked",
                table: "ItemParents",
                column: "IsPurchaseLocked");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_IsSaleLocked",
                table: "ItemParents",
                column: "IsSaleLocked");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_ItemCode",
                table: "ItemParents",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_ItemName",
                table: "ItemParents",
                column: "ItemName");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_ItemType",
                table: "ItemParents",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_SubCategoryId",
                table: "ItemParents",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_TaxCategoryId",
                table: "ItemParents",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemParents_UnitOfMeasureId",
                table: "ItemParents",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPropertyMappings_AttributeGroupId",
                table: "ItemPropertyMappings",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPropertyMappings_AttributeValueId",
                table: "ItemPropertyMappings",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPropertyMappings_ItemVariantId_AttributeGroupId",
                table: "ItemPropertyMappings",
                columns: new[] { "ItemVariantId", "AttributeGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_ItemVariantId",
                table: "ItemSuppliers",
                column: "ItemVariantId",
                unique: true,
                filter: "IsPrimary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_ItemVariantId_SupplierId",
                table: "ItemSuppliers",
                columns: new[] { "ItemVariantId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_SupplierId",
                table: "ItemSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_Barcode",
                table: "ItemVariants",
                column: "Barcode",
                unique: true,
                filter: "Barcode IS NOT NULL AND Barcode <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_IsDeactivated",
                table: "ItemVariants",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_ItemParentId",
                table: "ItemVariants",
                column: "ItemParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_RetailPrice",
                table: "ItemVariants",
                column: "RetailPrice");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_SkuCode",
                table: "ItemVariants",
                column: "SkuCode",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_ExpectedDate",
                table: "PoHeaders",
                column: "ExpectedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_OrderDate",
                table: "PoHeaders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_PoNumber",
                table: "PoHeaders",
                column: "PoNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_Status",
                table: "PoHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_SupplierId",
                table: "PoHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PoHeaders_TaxSnapshotStatus",
                table: "PoHeaders",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_ItemVariantId",
                table: "PoLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_LineStatus",
                table: "PoLines",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_PoHeaderId",
                table: "PoLines",
                column: "PoHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_PoHeaderId_ItemVariantId",
                table: "PoLines",
                columns: new[] { "PoHeaderId", "ItemVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_TaxCategoryId",
                table: "PoLines",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_TaxRateId",
                table: "PoLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_TaxSnapshotStatus",
                table: "PoLines",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_Barcode",
                table: "PriceChangeHistories",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_BatchNo",
                table: "PriceChangeHistories",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ChangedAt",
                table: "PriceChangeHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ChangedBy",
                table: "PriceChangeHistories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ChangeSource",
                table: "PriceChangeHistories",
                column: "ChangeSource");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ItemBatchId",
                table: "PriceChangeHistories",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ItemCode",
                table: "PriceChangeHistories",
                column: "ItemCode");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ItemVariantId",
                table: "PriceChangeHistories",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_ItemVariantId_ChangedAt",
                table: "PriceChangeHistories",
                columns: new[] { "ItemVariantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_PriceChangeNo",
                table: "PriceChangeHistories",
                column: "PriceChangeNo");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_PriceLevel",
                table: "PriceChangeHistories",
                column: "PriceLevel");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_PriceLevel_ChangedAt",
                table: "PriceChangeHistories",
                columns: new[] { "PriceLevel", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_SkuCode",
                table: "PriceChangeHistories",
                column: "SkuCode");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_SourceDocumentNo",
                table: "PriceChangeHistories",
                column: "SourceDocumentNo");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_SourceDocumentType",
                table: "PriceChangeHistories",
                column: "SourceDocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId",
                table: "PriceChangeHistories",
                columns: new[] { "SourceDocumentType", "SourceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceChangeHistories_SourceDocumentType_SourceDocumentId_SourceDocumentLineId",
                table: "PriceChangeHistories",
                columns: new[] { "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredTerminals_IsActive",
                table: "RegisteredTerminals",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredTerminals_IsCashierTerminal",
                table: "RegisteredTerminals",
                column: "IsCashierTerminal");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredTerminals_LicenseId",
                table: "RegisteredTerminals",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredTerminals_MachineCode",
                table: "RegisteredTerminals",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredTerminals_TerminalNo",
                table: "RegisteredTerminals",
                column: "TerminalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_DocumentNumber",
                table: "SalesDocumentAudits",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_DocumentType",
                table: "SalesDocumentAudits",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_EventType",
                table: "SalesDocumentAudits",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_OccurredAtUtc",
                table: "SalesDocumentAudits",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_SalesHeaderId",
                table: "SalesDocumentAudits",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesDocumentAudits_SalesHeaderId_DocumentType_IsSuccessful",
                table: "SalesDocumentAudits",
                columns: new[] { "SalesHeaderId", "DocumentType", "IsSuccessful" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CashierName",
                table: "SalesHeaders",
                column: "CashierName");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CheckoutToken",
                table: "SalesHeaders",
                column: "CheckoutToken",
                unique: true,
                filter: "\"CheckoutToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerCode",
                table: "SalesHeaders",
                column: "CustomerCode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerIsCreditEnabled",
                table: "SalesHeaders",
                column: "CustomerIsCreditEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerIsDiscountEligible",
                table: "SalesHeaders",
                column: "CustomerIsDiscountEligible");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerMasterId",
                table: "SalesHeaders",
                column: "CustomerMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerPhone",
                table: "SalesHeaders",
                column: "CustomerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_CustomerType",
                table: "SalesHeaders",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_DocumentType",
                table: "SalesHeaders",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_InvoiceNo",
                table: "SalesHeaders",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_IsVatRegisteredSale",
                table: "SalesHeaders",
                column: "IsVatRegisteredSale");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_IsVoided",
                table: "SalesHeaders",
                column: "IsVoided");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_IsWholesaleSale",
                table: "SalesHeaders",
                column: "IsWholesaleSale");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_ShiftSessionId",
                table: "SalesHeaders",
                column: "ShiftSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_Status",
                table: "SalesHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_TaxInvoiceNo",
                table: "SalesHeaders",
                column: "TaxInvoiceNo",
                unique: true,
                filter: "\"TaxInvoiceNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_TaxSnapshotStatus",
                table: "SalesHeaders",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_TerminalNo",
                table: "SalesHeaders",
                column: "TerminalNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeaders_TransactionDate",
                table: "SalesHeaders",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_ApprovedBy",
                table: "SalesLineDiscountAudits",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_Barcode",
                table: "SalesLineDiscountAudits",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_CashierName",
                table: "SalesLineDiscountAudits",
                column: "CashierName");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_CreatedAt",
                table: "SalesLineDiscountAudits",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_DiscountReasonId",
                table: "SalesLineDiscountAudits",
                column: "DiscountReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_DiscountRuleId",
                table: "SalesLineDiscountAudits",
                column: "DiscountRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_DiscountType",
                table: "SalesLineDiscountAudits",
                column: "DiscountType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_InvoiceDate",
                table: "SalesLineDiscountAudits",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_InvoiceNo",
                table: "SalesLineDiscountAudits",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_ItemBatchId",
                table: "SalesLineDiscountAudits",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_ItemVariantId",
                table: "SalesLineDiscountAudits",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_ReasonCode",
                table: "SalesLineDiscountAudits",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_RequiresAdminApproval",
                table: "SalesLineDiscountAudits",
                column: "RequiresAdminApproval");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_RequiresManagerApproval",
                table: "SalesLineDiscountAudits",
                column: "RequiresManagerApproval");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_SalesHeaderId",
                table: "SalesLineDiscountAudits",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_SalesLineId",
                table: "SalesLineDiscountAudits",
                column: "SalesLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_SkuCode",
                table: "SalesLineDiscountAudits",
                column: "SkuCode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineDiscountAudits_TerminalNo",
                table: "SalesLineDiscountAudits",
                column: "TerminalNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_BatchNo",
                table: "SalesLines",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountApprovedBy",
                table: "SalesLines",
                column: "DiscountApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountMode",
                table: "SalesLines",
                column: "DiscountMode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountReasonCode",
                table: "SalesLines",
                column: "DiscountReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountReasonId",
                table: "SalesLines",
                column: "DiscountReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountRequiresAdminApproval",
                table: "SalesLines",
                column: "DiscountRequiresAdminApproval");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountRequiresManagerApproval",
                table: "SalesLines",
                column: "DiscountRequiresManagerApproval");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_DiscountRuleId",
                table: "SalesLines",
                column: "DiscountRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_ExpiryDate",
                table: "SalesLines",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeApprovedByUserId",
                table: "SalesLines",
                column: "FreeApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeIssueRuleId",
                table: "SalesLines",
                column: "FreeIssueRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeIssueSnapshotStatus",
                table: "SalesLines",
                column: "FreeIssueSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeIssueType",
                table: "SalesLines",
                column: "FreeIssueType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_FreeReasonCode",
                table: "SalesLines",
                column: "FreeReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_GiftVoucherBarcode",
                table: "SalesLines",
                column: "GiftVoucherBarcode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_GiftVoucherId",
                table: "SalesLines",
                column: "GiftVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_GiftVoucherNo",
                table: "SalesLines",
                column: "GiftVoucherNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsFreeItem",
                table: "SalesLines",
                column: "IsFreeItem");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsGiftVoucherSale",
                table: "SalesLines",
                column: "IsGiftVoucherSale");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsManualDiscount",
                table: "SalesLines",
                column: "IsManualDiscount");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsPriceOverridden",
                table: "SalesLines",
                column: "IsPriceOverridden");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsReturned",
                table: "SalesLines",
                column: "IsReturned");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsRuleDiscount",
                table: "SalesLines",
                column: "IsRuleDiscount");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_IsSupplierRecoverable",
                table: "SalesLines",
                column: "IsSupplierRecoverable");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_ItemBatchId",
                table: "SalesLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_ItemTypeSnapshot",
                table: "SalesLines",
                column: "ItemTypeSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_ItemVariantId",
                table: "SalesLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SalesHeaderId",
                table: "SalesLines",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SupplierClaimId",
                table: "SalesLines",
                column: "SupplierClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SupplierClaimReferenceNo",
                table: "SalesLines",
                column: "SupplierClaimReferenceNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SupplierClaimStatus",
                table: "SalesLines",
                column: "SupplierClaimStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_SupplierId",
                table: "SalesLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_TaxCategoryId",
                table: "SalesLines",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_TaxRateId",
                table: "SalesLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_TaxSnapshotStatus",
                table: "SalesLines",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_CreatedAt",
                table: "SalesPayments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_GiftVoucherBarcode",
                table: "SalesPayments",
                column: "GiftVoucherBarcode");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_GiftVoucherId",
                table: "SalesPayments",
                column: "GiftVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_GiftVoucherNo",
                table: "SalesPayments",
                column: "GiftVoucherNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_PaymentDate",
                table: "SalesPayments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_PaymentType",
                table: "SalesPayments",
                column: "PaymentType");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_ReferenceNo",
                table: "SalesPayments",
                column: "ReferenceNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayments_SalesHeaderId",
                table: "SalesPayments",
                column: "SalesHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_CloseToken",
                table: "ShiftCloseSnapshots",
                column: "CloseToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_ShiftSessionId",
                table: "ShiftCloseSnapshots",
                column: "ShiftSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_TerminalNo_ClosedAt",
                table: "ShiftCloseSnapshots",
                columns: new[] { "TerminalNo", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloseSnapshots_ZReportNo",
                table: "ShiftCloseSnapshots",
                column: "ZReportNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_EndTime",
                table: "ShiftSessions",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_OneOpenPerTerminal",
                table: "ShiftSessions",
                column: "TerminalNo",
                unique: true,
                filter: "\"Status\" IN ('Open', 'Closing')");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_StartTime",
                table: "ShiftSessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSessions_TerminalNo_Status",
                table: "ShiftSessions",
                columns: new[] { "TerminalNo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentHeaders_AdjustmentDate",
                table: "StockAdjustmentHeaders",
                column: "AdjustmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentHeaders_AdjustmentMode",
                table: "StockAdjustmentHeaders",
                column: "AdjustmentMode");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentHeaders_AdjustmentNo",
                table: "StockAdjustmentHeaders",
                column: "AdjustmentNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentHeaders_AuthorizedBy",
                table: "StockAdjustmentHeaders",
                column: "AuthorizedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentHeaders_Status",
                table: "StockAdjustmentHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ItemBatchId",
                table: "StockAdjustmentLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ItemVariantId",
                table: "StockAdjustmentLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_LineStatus",
                table: "StockAdjustmentLines",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ReasonCode",
                table: "StockAdjustmentLines",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_StockAdjustmentHeaderId",
                table: "StockAdjustmentLines",
                column: "StockAdjustmentHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_StockAdjustmentHeaderId_ItemBatchId",
                table: "StockAdjustmentLines",
                columns: new[] { "StockAdjustmentHeaderId", "ItemBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreSettings_IsActive",
                table: "StoreSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId",
                table: "SubCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId_DisplayOrder",
                table: "SubCategories",
                columns: new[] { "CategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId_SubCategoryCode",
                table: "SubCategories",
                columns: new[] { "CategoryId", "SubCategoryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId_SubCategoryName",
                table: "SubCategories",
                columns: new[] { "CategoryId", "SubCategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_DisplayOrder",
                table: "SubCategories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_IsDeactivated",
                table: "SubCategories",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_DueDate",
                table: "SupplierLedgers",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_GrnHeaderId",
                table: "SupplierLedgers",
                column: "GrnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_IsPaid",
                table: "SupplierLedgers",
                column: "IsPaid");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_ReferenceDocument",
                table: "SupplierLedgers",
                column: "ReferenceDocument");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_SupplierId",
                table: "SupplierLedgers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_TransactionDate",
                table: "SupplierLedgers",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_TransactionType",
                table: "SupplierLedgers",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_GrnHeaderId",
                table: "SupplierReturnHeaders",
                column: "GrnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_OriginalInvoiceNo",
                table: "SupplierReturnHeaders",
                column: "OriginalInvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_ReturnDate",
                table: "SupplierReturnHeaders",
                column: "ReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_ReturnNumber",
                table: "SupplierReturnHeaders",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_Status",
                table: "SupplierReturnHeaders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_SupplierId",
                table: "SupplierReturnHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnHeaders_TaxSnapshotStatus",
                table: "SupplierReturnHeaders",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_GrnLineId",
                table: "SupplierReturnLines",
                column: "GrnLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_ItemBatchId",
                table: "SupplierReturnLines",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_ItemVariantId",
                table: "SupplierReturnLines",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_LineStatus",
                table: "SupplierReturnLines",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_ReasonCode",
                table: "SupplierReturnLines",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_ReturnHeaderId",
                table: "SupplierReturnLines",
                column: "ReturnHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_ReturnHeaderId_ItemBatchId",
                table: "SupplierReturnLines",
                columns: new[] { "ReturnHeaderId", "ItemBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TaxCategoryId",
                table: "SupplierReturnLines",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TaxRateId",
                table: "SupplierReturnLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_TaxSnapshotStatus",
                table: "SupplierReturnLines",
                column: "TaxSnapshotStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyName",
                table: "Suppliers",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_IsDeactivated",
                table: "Suppliers",
                column: "IsDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Phone1",
                table: "Suppliers",
                column: "Phone1");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierCode",
                table: "Suppliers",
                column: "SupplierCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierName",
                table: "Suppliers",
                column: "SupplierName");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCategories_CategoryCode",
                table: "TaxCategories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxCategories_DisplayOrder",
                table: "TaxCategories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCategories_IsActive",
                table: "TaxCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCategories_TreatmentType",
                table: "TaxCategories",
                column: "TreatmentType");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxCategoryId",
                table: "TaxRates",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxCategoryId_EffectiveFrom",
                table: "TaxRates",
                columns: new[] { "TaxCategoryId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxCategoryId_IsActive",
                table: "TaxRates",
                columns: new[] { "TaxCategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TaxCode",
                table: "TaxRates",
                column: "TaxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerminalSettings_IsActive",
                table: "TerminalSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalSettings_MachineName",
                table: "TerminalSettings",
                column: "MachineName");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalSettings_TerminalNo",
                table: "TerminalSettings",
                column: "TerminalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_DisplayOrder",
                table: "UnitsOfMeasure",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_IsActive",
                table: "UnitsOfMeasure",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_UomCode",
                table: "UnitsOfMeasure",
                column: "UomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockoutEndUtc",
                table: "Users",
                column: "LockoutEndUtc");

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
                name: "BackupHistory");

            migrationBuilder.DropTable(
                name: "CashDrawerEvents");

            migrationBuilder.DropTable(
                name: "CashierCartLines");

            migrationBuilder.DropTable(
                name: "CategoryAttributeGroups");

            migrationBuilder.DropTable(
                name: "CustomerLedgerAllocations");

            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropTable(
                name: "ExpressItemLayouts");

            migrationBuilder.DropTable(
                name: "FreeIssueReasons");

            migrationBuilder.DropTable(
                name: "FreeIssueRules");

            migrationBuilder.DropTable(
                name: "FreeItemClaimAdjustments");

            migrationBuilder.DropTable(
                name: "GiftVoucherTransactions");

            migrationBuilder.DropTable(
                name: "InstalledLicenses");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "ItemPropertyMappings");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "LoginAuditEvents");

            migrationBuilder.DropTable(
                name: "PriceChangeHistories");

            migrationBuilder.DropTable(
                name: "RegisteredTerminals");

            migrationBuilder.DropTable(
                name: "SalesDocumentAudits");

            migrationBuilder.DropTable(
                name: "SalesLineDiscountAudits");

            migrationBuilder.DropTable(
                name: "ShiftCloseSnapshots");

            migrationBuilder.DropTable(
                name: "StockAdjustmentLines");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropTable(
                name: "SupplierLedgers");

            migrationBuilder.DropTable(
                name: "SupplierReturnLines");

            migrationBuilder.DropTable(
                name: "TerminalSettings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "CashierCartSessions");

            migrationBuilder.DropTable(
                name: "CustomerLedgers");

            migrationBuilder.DropTable(
                name: "CustomerPaymentReceipts");

            migrationBuilder.DropTable(
                name: "CustomerReturnLines");

            migrationBuilder.DropTable(
                name: "FreeItemClaimLogs");

            migrationBuilder.DropTable(
                name: "SalesPayments");

            migrationBuilder.DropTable(
                name: "AttributeValues");

            migrationBuilder.DropTable(
                name: "StockAdjustmentHeaders");

            migrationBuilder.DropTable(
                name: "GrnLines");

            migrationBuilder.DropTable(
                name: "SupplierReturnHeaders");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CustomerReturnHeaders");

            migrationBuilder.DropTable(
                name: "SalesLines");

            migrationBuilder.DropTable(
                name: "AttributeGroups");

            migrationBuilder.DropTable(
                name: "PoLines");

            migrationBuilder.DropTable(
                name: "GrnHeaders");

            migrationBuilder.DropTable(
                name: "GiftVouchers");

            migrationBuilder.DropTable(
                name: "DiscountRules");

            migrationBuilder.DropTable(
                name: "ItemBatches");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropTable(
                name: "PoHeaders");

            migrationBuilder.DropTable(
                name: "SalesHeaders");

            migrationBuilder.DropTable(
                name: "DiscountReasons");

            migrationBuilder.DropTable(
                name: "ItemVariants");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "CustomerMasters");

            migrationBuilder.DropTable(
                name: "ShiftSessions");

            migrationBuilder.DropTable(
                name: "ItemParents");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "TaxCategories");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
