using System;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;

namespace POS.Core.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // --- CORE MASTERS ---
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<SubCategory> SubCategories { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;

        // --- MATRIX INVENTORY ---
        public DbSet<AttributeGroup> AttributeGroups { get; set; } = null!;
        public DbSet<AttributeValue> AttributeValues { get; set; } = null!;
        public DbSet<CategoryAttributeGroup> CategoryAttributeGroups { get; set; } = null!;
        public DbSet<ItemParent> ItemParents { get; set; } = null!;
        public DbSet<ItemVariant> ItemVariants { get; set; } = null!;
        public DbSet<ItemPropertyMapping> ItemPropertyMappings { get; set; } = null!;
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; } = null!;
        public DbSet<ItemBatch> ItemBatches { get; set; } = null!;

        // --- ENTERPRISE LEDGERS ---
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<SupplierLedger> SupplierLedgers { get; set; } = null!;
        public DbSet<DocumentSequence> DocumentSequences { get; set; } = null!;

        // --- PROCUREMENT & TRANSACTIONS (B2B) ---
        public DbSet<GrnHeader> GrnHeaders { get; set; } = null!;
        public DbSet<GrnLine> GrnLines { get; set; } = null!;
        public DbSet<PoHeader> PoHeaders { get; set; } = null!;
        public DbSet<PoLine> PoLines { get; set; } = null!;
        public DbSet<SupplierReturnHeader> ReturnHeaders { get; set; } = null!;
        public DbSet<SupplierReturnLine> ReturnLines { get; set; } = null!;
        public DbSet<StockAdjustmentHeader> StockAdjustmentHeaders { get; set; } = null!;
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; } = null!;

        // --- CRM & CUSTOMER FINANCE ---
        public DbSet<CustomerMaster> CustomerMasters { get; set; } = null!;
        public DbSet<CustomerLedger> CustomerLedgers { get; set; } = null!;

        // --- LOYALTY & PROMOTIONS ENGINE ---
        public DbSet<PromoRule> PromoRules { get; set; } = null!;
        public DbSet<PromoCondition> PromoConditions { get; set; } = null!;
        public DbSet<PromoReward> PromoRewards { get; set; } = null!;
        public DbSet<LoyaltyDiscountProfile> LoyaltyDiscountProfiles { get; set; } = null!;
        public DbSet<ExpressItemLayout> ExpressItemLayouts { get; set; } = null!;

        // --- CASHIER & SALES ENGINE ---
        public DbSet<SalesHeader> SalesHeaders { get; set; } = null!;
        public DbSet<SalesLine> SalesLines { get; set; } = null!;
        public DbSet<SalesPayment> SalesPayments { get; set; } = null!;
        public DbSet<CustomerReturnHeader> CustomerReturnHeaders { get; set; } = null!;
        public DbSet<CustomerReturnLine> CustomerReturnLines { get; set; } = null!;
        public DbSet<ItemSupplier> ItemSuppliers { get; set; } = null!;
        public DbSet<QuotationHeader> QuotationHeaders { get; set; } = null!;
        public DbSet<QuotationLine> QuotationLines { get; set; } = null!;

        // --- SHIFT & SECURITY ---
        public DbSet<ShiftSession> ShiftSessions { get; set; } = null!;
        public DbSet<CashMovement> CashMovements { get; set; } = null!;

        // --- SETTINGS / VOUCHERS / CLAIMS ---
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<GiftVoucher> GiftVouchers { get; set; } = null!;
        public DbSet<FreeItemClaimLog> FreeItemClaims { get; set; } = null!;

        // Keep these because some existing repositories may already use these names.
        public DbSet<SupplierReturnHeader> SupplierReturnHeaders { get; set; } = null!;
        public DbSet<SupplierReturnLine> SupplierReturnLines { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dbFolder = System.IO.Path.Combine(appData, "POS");

                System.IO.Directory.CreateDirectory(dbFolder);

                string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            DateTime seedDate = new DateTime(2026, 1, 1);

            // =========================================================
            // USER / SECURITY
            // =========================================================
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // =========================================================
            // CATEGORY MASTER
            // =========================================================
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.CategoryCode)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(c => c.CategoryName)
                    .IsRequired()
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.HasIndex(c => c.CategoryCode)
                    .IsUnique();

                entity.HasIndex(c => c.CategoryName)
                    .IsUnique();

                entity.HasIndex(c => c.IsDeactivated);
            });

            // =========================================================
            // SUB CATEGORY MASTER
            // =========================================================
            modelBuilder.Entity<SubCategory>(entity =>
            {
                entity.Property(s => s.SubCategoryCode)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(s => s.SubCategoryName)
                    .IsRequired()
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.HasOne(s => s.Category)
                    .WithMany(c => c.SubCategories)
                    .HasForeignKey(s => s.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.CategoryId, s.SubCategoryCode })
                    .IsUnique();

                entity.HasIndex(s => new { s.CategoryId, s.SubCategoryName })
                    .IsUnique();

                entity.HasIndex(s => s.IsDeactivated);
            });

            // =========================================================
            // UNIT OF MEASURE MASTER
            // =========================================================
            modelBuilder.Entity<UnitOfMeasure>(entity =>
            {
                entity.Property(u => u.UomCode)
                    .IsRequired()
                    .HasMaxLength(10)
                    .UseCollation("NOCASE");

                entity.Property(u => u.UomDescription)
                    .IsRequired()
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.HasIndex(u => u.UomCode)
                    .IsUnique();

                entity.HasIndex(u => u.IsActive);

                entity.HasIndex(u => u.DisplayOrder);

                entity.HasData(
                    new UnitOfMeasure
                    {
                        Id = 1,
                        UomCode = "PCS",
                        UomDescription = "Pieces",
                        AllowDecimals = false,
                        DisplayOrder = 10,
                        IsActive = true,
                        CreatedAt = seedDate,
                        UpdatedAt = seedDate,
                        DeactivatedAt = null
                    }
                );
            });

            // =========================================================
            // ATTRIBUTE GROUP MASTER
            // =========================================================
            modelBuilder.Entity<AttributeGroup>(entity =>
            {
                entity.Property(a => a.GroupName)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.HasIndex(a => a.GroupName)
                    .IsUnique();

                entity.HasIndex(a => a.IsDeactivated);

                entity.HasIndex(a => a.DisplayOrder);
            });

            // =========================================================
            // ATTRIBUTE VALUE MASTER
            // =========================================================
            modelBuilder.Entity<AttributeValue>(entity =>
            {
                entity.Property(a => a.ValueName)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.HasOne(a => a.AttributeGroup)
                    .WithMany(g => g.AttributeValues)
                    .HasForeignKey(a => a.AttributeGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => new { a.AttributeGroupId, a.ValueName })
                    .IsUnique();

                entity.HasIndex(a => a.IsDeactivated);

                entity.HasIndex(a => new { a.AttributeGroupId, a.DisplayOrder });
            });

            // =========================================================
            // CATEGORY <-> ATTRIBUTE GROUP JOIN TABLE
            // =========================================================
            modelBuilder.Entity<CategoryAttributeGroup>(entity =>
            {
                entity.HasKey(c => new { c.CategoryId, c.AttributeGroupId });

                entity.HasOne(c => c.Category)
                    .WithMany(ca => ca.CategoryAssignments)
                    .HasForeignKey(c => c.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.AttributeGroup)
                    .WithMany(a => a.CategoryAssignments)
                    .HasForeignKey(c => c.AttributeGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.AttributeGroupId);
            });

            // =========================================================
            // ITEM PARENT MASTER
            // =========================================================
            modelBuilder.Entity<ItemParent>(entity =>
            {
                entity.Property(i => i.ItemCode)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(i => i.ItemName)
                    .IsRequired()
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(i => i.PrintName)
                    .HasMaxLength(50);

                // Temporary legacy field.
                // Keep only until all Item Master code is moved to UnitOfMeasureId.
                entity.Property(i => i.BaseUom)
                    .HasMaxLength(20);

                entity.Property(i => i.TaxCode)
                    .HasMaxLength(20);

                entity.Property(i => i.UnitOfMeasureId)
                    .HasDefaultValue(1);

                entity.HasOne(i => i.Category)
                    .WithMany()
                    .HasForeignKey(i => i.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.SubCategory)
                    .WithMany()
                    .HasForeignKey(i => i.SubCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.UnitOfMeasure)
                    .WithMany(u => u.ItemParents)
                    .HasForeignKey(i => i.UnitOfMeasureId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(i => i.ItemCode)
                    .IsUnique();

                entity.HasIndex(i => i.ItemName);

                entity.HasIndex(i => i.CategoryId);

                entity.HasIndex(i => i.SubCategoryId);

                entity.HasIndex(i => i.UnitOfMeasureId);

                entity.HasIndex(i => i.IsDeactivated);
            });

            // =========================================================
            // ITEM VARIANT PROPERTY MAPPING
            // =========================================================
            modelBuilder.Entity<ItemPropertyMapping>(entity =>
            {
                // Main composite key.
                entity.HasKey(m => new
                {
                    m.ItemVariantId,
                    m.AttributeGroupId,
                    m.AttributeValueId
                });

                entity.HasOne(m => m.ItemVariant)
                    .WithMany(v => v.PropertyMappings)
                    .HasForeignKey(m => m.ItemVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.AttributeGroup)
                    .WithMany()
                    .HasForeignKey(m => m.AttributeGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.AttributeValue)
                    .WithMany()
                    .HasForeignKey(m => m.AttributeValueId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Critical rule:
                // One variant can have only one value for the same attribute group.
                //
                // Valid:
                // Variant 1 -> Color: Red
                // Variant 1 -> Size: Medium
                //
                // Invalid:
                // Variant 1 -> Color: Red
                // Variant 1 -> Color: Yellow
                entity.HasIndex(m => new
                {
                    m.ItemVariantId,
                    m.AttributeGroupId
                })
                .IsUnique();

                entity.HasIndex(m => m.AttributeValueId);

                entity.HasIndex(m => m.AttributeGroupId);
            });

            // =========================================================
            // ITEM VARIANT MASTER
            // =========================================================
            modelBuilder.Entity<ItemVariant>(entity =>
            {
                entity.Property(v => v.SkuCode)
                    .IsRequired()
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(v => v.VariantDescription)
                    .HasMaxLength(250)
                    .UseCollation("NOCASE");

                entity.Property(v => v.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(v => v.AverageCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.CostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.RetailPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.WholesalePrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.MinimumPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.MaximumPrice)
                    .HasColumnType("decimal(18,2)");

                entity.HasOne(v => v.ItemParent)
                    .WithMany(p => p.Variants)
                    .HasForeignKey(v => v.ItemParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // SKU must be unique across the system.
                entity.HasIndex(v => v.SkuCode)
                    .IsUnique();

                // Barcode must be unique only when it is not empty.
                // This allows blank barcode values during draft/manual entry.
                entity.HasIndex(v => v.Barcode)
                    .IsUnique()
                    .HasFilter("Barcode IS NOT NULL AND Barcode <> ''");

                entity.HasIndex(v => v.ItemParentId);

                entity.HasIndex(v => v.IsDeactivated);

                entity.HasIndex(v => v.RetailPrice);
            });

            // =========================================================
            // ITEM BATCH MASTER
            // =========================================================
            modelBuilder.Entity<ItemBatch>(entity =>
            {
                entity.Property(b => b.BatchNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(b => b.CostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(b => b.RetailPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(b => b.WholesalePrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(b => b.CurrentStock)
                    .HasColumnType("decimal(18,3)");

                entity.HasOne(b => b.ItemVariant)
                    .WithMany(v => v.ItemBatches)
                    .HasForeignKey(b => b.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Same batch number should not exist twice for the same variant.
                entity.HasIndex(b => new
                {
                    b.ItemVariantId,
                    b.BatchNo
                })
                .IsUnique();

                entity.HasIndex(b => b.ItemVariantId);

                entity.HasIndex(b => b.BatchNo);

                entity.HasIndex(b => b.ExpiryDate);

                entity.HasIndex(b => b.ReceivedDate);

                entity.HasIndex(b => b.IsDeactivated);
            });


            // =========================================================
            // INVENTORY TRANSACTION LEDGER
            // =========================================================
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.Property(t => t.TransactionType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(t => t.ReferenceDocument)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(t => t.Quantity)
                    .HasColumnType("decimal(18,3)");

                entity.Property(t => t.UnitCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.CreatedBy)
                    .HasMaxLength(50);

                entity.Property(t => t.Remarks)
                    .HasMaxLength(250);

                entity.HasOne(t => t.ItemVariant)
                    .WithMany()
                    .HasForeignKey(t => t.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.ItemBatch)
                    .WithMany()
                    .HasForeignKey(t => t.ItemBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.ItemVariantId);

                entity.HasIndex(t => t.ItemBatchId);

                entity.HasIndex(t => t.TransactionDate);

                entity.HasIndex(t => t.TransactionType);

                entity.HasIndex(t => t.ReferenceDocument);

                entity.HasIndex(t => new
                {
                    t.ReferenceDocument,
                    t.ReferenceLineId
                });
            });


            // =========================================================
            // GRN HEADER
            // =========================================================
            modelBuilder.Entity<GrnHeader>(entity =>
            {
                entity.Property(g => g.GrnNumber)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(g => g.SupplierInvoiceNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(g => g.Remarks)
                    .HasMaxLength(500);

                entity.Property(g => g.Status)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(g => g.CreatedBy)
                    .HasMaxLength(50);

                entity.Property(g => g.PostedBy)
                    .HasMaxLength(50);

                entity.Property(g => g.CancelledBy)
                    .HasMaxLength(50);

                entity.Property(g => g.CancellationReason)
                    .HasMaxLength(250);

                entity.Property(g => g.Subtotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(g => g.GlobalBillDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(g => g.FreightAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(g => g.TotalDiscountAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(g => g.NetPayable)
                    .HasColumnType("decimal(18,2)");

                entity.HasOne(g => g.Supplier)
                    .WithMany()
                    .HasForeignKey(g => g.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(g => g.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(g => g.GrnNumber)
                    .IsUnique();

                // Important commercial rule:
                // one supplier invoice should not be posted twice for the same supplier.
                entity.HasIndex(g => new
                {
                    g.SupplierId,
                    g.SupplierInvoiceNo
                })
                .IsUnique();

                entity.HasIndex(g => g.SupplierId);

                entity.HasIndex(g => g.PurchaseOrderId);

                entity.HasIndex(g => g.InvoiceDate);

                entity.HasIndex(g => g.ReceivedDate);

                entity.HasIndex(g => g.DueDate);

                entity.HasIndex(g => g.Status);
            });


            // =========================================================
            // GRN LINE
            // =========================================================
            modelBuilder.Entity<GrnLine>(entity =>
            {
                entity.Property(l => l.BatchNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.Uom)
                    .HasMaxLength(20);

                entity.Property(l => l.OrderedQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.ReceivedQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.UnitCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LandedCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.HasOne(l => l.GrnHeader)
                    .WithMany(h => h.GrnLines)
                    .HasForeignKey(l => l.GrnHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ItemVariant)
                    .WithMany()
                    .HasForeignKey(l => l.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.PoLine)
                    .WithMany()
                    .HasForeignKey(l => l.PoLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.GrnHeaderId);

                entity.HasIndex(l => l.ItemVariantId);

                entity.HasIndex(l => l.PoLineId);

                entity.HasIndex(l => l.BatchNo);

                entity.HasIndex(l => l.LineStatus);

                // Prevent accidental duplicate same item/same batch line inside one GRN.
                // If user scans same item and batch twice, ViewModel should merge quantities.
                entity.HasIndex(l => new
                {
                    l.GrnHeaderId,
                    l.ItemVariantId,
                    l.BatchNo
                })
                .IsUnique();
            });


            // =========================================================
            // SUPPLIER LEDGER
            // =========================================================
            modelBuilder.Entity<SupplierLedger>(entity =>
            {
                entity.Property(l => l.TransactionType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(l => l.ReferenceDocument)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.ChargeAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.PaymentAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.BalanceAfterTransaction)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.PaymentMethod)
                    .HasMaxLength(50);

                entity.Property(l => l.BankName)
                    .HasMaxLength(100);

                entity.Property(l => l.ReferenceNumber)
                    .HasMaxLength(50);

                entity.Property(l => l.Remarks)
                    .HasMaxLength(250);

                entity.Property(l => l.CreatedBy)
                    .HasMaxLength(50);

                entity.HasOne(l => l.Supplier)
                    .WithMany()
                    .HasForeignKey(l => l.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.GrnHeader)
                    .WithMany()
                    .HasForeignKey(l => l.GrnHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.SupplierId);

                entity.HasIndex(l => l.GrnHeaderId);

                entity.HasIndex(l => l.TransactionDate);

                entity.HasIndex(l => l.TransactionType);

                entity.HasIndex(l => l.ReferenceDocument);

                entity.HasIndex(l => l.DueDate);

                entity.HasIndex(l => l.IsPaid);
            });

            // =========================================================
            // SUPPLIER RETURN HEADER
            // =========================================================
            modelBuilder.Entity<SupplierReturnHeader>(entity =>
            {
                entity.ToTable("SupplierReturnHeaders");

                entity.Property(r => r.ReturnNumber)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(r => r.OriginalInvoiceNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(r => r.AuthorizedBy)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(r => r.Remarks)
                    .HasMaxLength(500);

                entity.Property(r => r.GrossCredit)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.RestockingFee)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.NetCredit)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.Status)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(r => r.CreatedBy)
                    .HasMaxLength(50);

                entity.Property(r => r.PostedBy)
                    .HasMaxLength(50);

                entity.Property(r => r.CancelledBy)
                    .HasMaxLength(50);

                entity.Property(r => r.CancellationReason)
                    .HasMaxLength(250);

                entity.HasOne(r => r.Supplier)
                    .WithMany()
                    .HasForeignKey(r => r.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.GrnHeader)
                    .WithMany()
                    .HasForeignKey(r => r.GrnHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.ReturnNumber)
                    .IsUnique();

                entity.HasIndex(r => r.SupplierId);

                entity.HasIndex(r => r.GrnHeaderId);

                entity.HasIndex(r => r.OriginalInvoiceNo);

                entity.HasIndex(r => r.ReturnDate);

                entity.HasIndex(r => r.Status);
            });


            // =========================================================
            // SUPPLIER RETURN LINE
            // =========================================================
            modelBuilder.Entity<SupplierReturnLine>(entity =>
            {
                entity.ToTable("SupplierReturnLines");

                entity.Property(l => l.BatchNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.ReturnQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.HistoricalCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.CreditValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.ReasonCode)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.LineRemarks)
                    .HasMaxLength(250);

                entity.Property(l => l.LineStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.HasOne(l => l.ReturnHeader)
                    .WithMany(h => h.ReturnLines)
                    .HasForeignKey(l => l.ReturnHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.GrnLine)
                    .WithMany()
                    .HasForeignKey(l => l.GrnLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ItemVariant)
                    .WithMany()
                    .HasForeignKey(l => l.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ItemBatch)
                    .WithMany()
                    .HasForeignKey(l => l.ItemBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.ReturnHeaderId);

                entity.HasIndex(l => l.GrnLineId);

                entity.HasIndex(l => l.ItemVariantId);

                entity.HasIndex(l => l.ItemBatchId);

                entity.HasIndex(l => l.ReasonCode);

                entity.HasIndex(l => l.LineStatus);

                // Same physical batch should appear only once in one supplier return document.
                entity.HasIndex(l => new
                {
                    l.ReturnHeaderId,
                    l.ItemBatchId
                })
                .IsUnique();
            });

            // =========================================================
            // DOCUMENT SEQUENCES
            // =========================================================
            modelBuilder.Entity<DocumentSequence>(entity =>
            {
                entity.Property(d => d.DocumentType)
                    .IsRequired()
                    .HasMaxLength(10)
                    .UseCollation("NOCASE");

                entity.Property(d => d.Prefix)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.HasKey(d => d.DocumentType);

                entity.HasData(
                    new DocumentSequence
                    {
                        DocumentType = "GRN",
                        Prefix = "GRN-",
                        NextSequenceNumber = 1,
                        PaddingLength = 5,
                        UpdatedAt = seedDate
                    },
                    new DocumentSequence
                    {
                        DocumentType = "PO",
                        Prefix = "PO-",
                        NextSequenceNumber = 1,
                        PaddingLength = 5,
                        UpdatedAt = seedDate
                    },
                    new DocumentSequence
                    {
                        DocumentType = "PAY",
                        Prefix = "PAY-",
                        NextSequenceNumber = 1,
                        PaddingLength = 6,
                        UpdatedAt = seedDate
                    },
                    new DocumentSequence
                    {
                        DocumentType = "ADJ",
                        Prefix = "ADJ-",
                        NextSequenceNumber = 1,
                        PaddingLength = 5,
                        UpdatedAt = seedDate
                    },
                    new DocumentSequence
                    {
                        DocumentType = "RTN",
                        Prefix = "RTN-",
                        NextSequenceNumber = 1,
                        PaddingLength = 5,
                        UpdatedAt = seedDate
                    }

                );
            });

            // =========================================================
            // CASH MOVEMENTS
            // =========================================================
            modelBuilder.Entity<CashMovement>()
                .HasIndex(c => c.ReferenceVoucherNo)
                .IsUnique();

            modelBuilder.Entity<CashMovement>()
                .HasIndex(c => c.Timestamp);

            // =========================================================
            // SUPPLIER MASTER
            // =========================================================
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.Property(s => s.SupplierCode)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(s => s.SupplierName)
                    .IsRequired()
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CompanyName)
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(s => s.ContactPerson)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(s => s.Phone1)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(s => s.Phone2)
                    .HasMaxLength(20);

                entity.Property(s => s.Email)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(s => s.Address)
                    .HasMaxLength(250);

                entity.Property(s => s.VatNumber)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(s => s.DefaultCreditDays)
                    .HasDefaultValue(30);

                entity.Property(s => s.CurrentBalance)
                    .HasColumnType("decimal(18,2)");

                // Supplier code must be unique.
                // Example:
                // SUP001 cannot be created twice.
                entity.HasIndex(s => s.SupplierCode)
                    .IsUnique();

                // Search/index support for Supplier Master grid and dropdowns.
                entity.HasIndex(s => s.SupplierName);

                entity.HasIndex(s => s.CompanyName);

                entity.HasIndex(s => s.Phone1);

                entity.HasIndex(s => s.IsDeactivated);
            });

            // =========================================================
            // ITEM <-> SUPPLIER ASSIGNMENT
            // =========================================================
            modelBuilder.Entity<ItemSupplier>(entity =>
            {
                entity.Property(i => i.SupplierItemCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(i => i.LastCostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(i => i.MinimumOrderQuantity)
                    .HasDefaultValue(1);

                entity.HasOne(i => i.Supplier)
                    .WithMany(s => s.ItemSuppliers)
                    .HasForeignKey(i => i.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.ItemVariant)
                    .WithMany(v => v.ItemSuppliers)
                    .HasForeignKey(i => i.ItemVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Same supplier should not be assigned twice to the same item variant.
                entity.HasIndex(i => new
                {
                    i.ItemVariantId,
                    i.SupplierId
                })
                .IsUnique();

                // Only one primary supplier is allowed per variant.
                entity.HasIndex(i => i.ItemVariantId)
                    .IsUnique()
                    .HasFilter("IsPrimary = 1");

                entity.HasIndex(i => i.SupplierId);
            });

            // =========================================================
            // PURCHASE ORDER HEADER
            // =========================================================
            modelBuilder.Entity<PoHeader>(entity =>
            {
                entity.Property(p => p.PoNumber)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(p => p.Terms)
                    .HasMaxLength(50);

                entity.Property(p => p.Remarks)
                    .HasMaxLength(500);

                entity.Property(p => p.Status)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(p => p.CreatedBy)
                    .HasMaxLength(50);

                entity.Property(p => p.ApprovedBy)
                    .HasMaxLength(50);

                entity.Property(p => p.CancelledBy)
                    .HasMaxLength(50);

                entity.Property(p => p.CancellationReason)
                    .HasMaxLength(250);

                entity.Property(p => p.Subtotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.GlobalBillDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.TotalTaxAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.TotalDiscountAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.NetPayable)
                    .HasColumnType("decimal(18,2)");

                entity.HasOne(p => p.Supplier)
                    .WithMany()
                    .HasForeignKey(p => p.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.PoNumber)
                    .IsUnique();

                entity.HasIndex(p => p.SupplierId);

                entity.HasIndex(p => p.OrderDate);

                entity.HasIndex(p => p.ExpectedDate);

                entity.HasIndex(p => p.Status);
            });

            // =========================================================
            // PURCHASE ORDER LINE
            // =========================================================
            modelBuilder.Entity<PoLine>(entity =>
            {
                entity.Property(l => l.Uom)
                    .HasMaxLength(20);

                entity.Property(l => l.SupplierItemCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(l => l.OrderQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.ReceivedQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.ExpectedCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.TaxCode)
                    .HasMaxLength(20);

                entity.Property(l => l.TaxAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.HasOne(l => l.PoHeader)
                    .WithMany(h => h.PoLines)
                    .HasForeignKey(l => l.PoHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ItemVariant)
                    .WithMany()
                    .HasForeignKey(l => l.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.PoHeaderId);

                entity.HasIndex(l => l.ItemVariantId);

                // Prevent the same item variant being added twice to the same PO.
                // The ViewModel should merge quantities instead of duplicate rows.
                entity.HasIndex(l => new
                {
                    l.PoHeaderId,
                    l.ItemVariantId
                })
                .IsUnique();

                entity.HasIndex(l => l.LineStatus);
            });

            // =========================================================
            // STOCK ADJUSTMENT HEADER
            // =========================================================
            modelBuilder.Entity<StockAdjustmentHeader>(entity =>
            {
                entity.Property(a => a.AdjustmentNo)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(a => a.AdjustmentMode)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(a => a.AuthorizedBy)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(a => a.Reference)
                    .HasMaxLength(100);

                entity.Property(a => a.Remarks)
                    .HasMaxLength(500);

                entity.Property(a => a.Status)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(a => a.CreatedBy)
                    .HasMaxLength(50);

                entity.Property(a => a.PostedBy)
                    .HasMaxLength(50);

                entity.Property(a => a.CancelledBy)
                    .HasMaxLength(50);

                entity.Property(a => a.CancellationReason)
                    .HasMaxLength(250);

                entity.Property(a => a.TotalImpact)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.TotalIncreaseQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(a => a.TotalDecreaseQty)
                    .HasColumnType("decimal(18,3)");

                entity.HasIndex(a => a.AdjustmentNo)
                    .IsUnique();

                entity.HasIndex(a => a.AdjustmentDate);

                entity.HasIndex(a => a.AdjustmentMode);

                entity.HasIndex(a => a.Status);

                entity.HasIndex(a => a.AuthorizedBy);
            });


            // =========================================================
            // STOCK ADJUSTMENT LINE
            // =========================================================
            modelBuilder.Entity<StockAdjustmentLine>(entity =>
            {
                entity.Property(l => l.SystemQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.ActualQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.VarianceQty)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.ReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.LineRemarks)
                    .HasMaxLength(250);

                entity.Property(l => l.UnitCost)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.CostImpact)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.LineStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.HasOne(l => l.StockAdjustmentHeader)
                    .WithMany(h => h.AdjustmentLines)
                    .HasForeignKey(l => l.StockAdjustmentHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ItemBatch)
                    .WithMany()
                    .HasForeignKey(l => l.ItemBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ItemVariant)
                    .WithMany()
                    .HasForeignKey(l => l.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.StockAdjustmentHeaderId);

                entity.HasIndex(l => l.ItemBatchId);

                entity.HasIndex(l => l.ItemVariantId);

                entity.HasIndex(l => l.ReasonCode);

                entity.HasIndex(l => l.LineStatus);

                // Same batch should not appear twice in one adjustment document.
                entity.HasIndex(l => new
                {
                    l.StockAdjustmentHeaderId,
                    l.ItemBatchId
                })
                .IsUnique();
            });
        }


    }
}