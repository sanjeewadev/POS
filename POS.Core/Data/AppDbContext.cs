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

        // --- EXPRESS ITEM LAYOUT ---
        public DbSet<ExpressItemLayout> ExpressItemLayouts { get; set; } = null!;

        // --- DISCOUNT RULE ENGINE ---
        public DbSet<DiscountRule> DiscountRules { get; set; } = null!;
        public DbSet<DiscountReason> DiscountReasons { get; set; } = null!;
        public DbSet<SalesLineDiscountAudit> SalesLineDiscountAudits { get; set; } = null!;

        // --- CASHIER & SALES ENGINE ---
        public DbSet<SalesHeader> SalesHeaders { get; set; } = null!;
        public DbSet<SalesLine> SalesLines { get; set; } = null!;
        public DbSet<SalesPayment> SalesPayments { get; set; } = null!;
        public DbSet<CustomerReturnHeader> CustomerReturnHeaders { get; set; } = null!;
        public DbSet<CustomerReturnLine> CustomerReturnLines { get; set; } = null!;
        public DbSet<ItemSupplier> ItemSuppliers { get; set; } = null!;

        // --- SHIFT & SECURITY ---
        public DbSet<ShiftSession> ShiftSessions { get; set; } = null!;
        public DbSet<CashMovement> CashMovements { get; set; } = null!;

        // --- SETTINGS / VOUCHERS / CLAIMS ---
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<GiftVoucher> GiftVouchers { get; set; } = null!;

        public DbSet<GiftVoucherTransaction> GiftVoucherTransactions { get; set; }
        // =========================================================
        // FREE ISSUE / SUPPLIER CLAIM
        // =========================================================

        public DbSet<FreeIssueRule> FreeIssueRules { get; set; }

        public DbSet<FreeIssueReason> FreeIssueReasons { get; set; }

        public DbSet<FreeItemClaimLog> FreeItemClaimLogs { get; set; }

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
                // Keep while older pages/repositories still read BaseUom.
                entity.Property(i => i.BaseUom)
                    .HasMaxLength(20);

                entity.Property(i => i.TaxCode)
                    .HasMaxLength(20);

                entity.Property(i => i.UnitOfMeasureId)
                    .HasDefaultValue(1);

                // New correct tracking flags.
                // Batch Tracking and Expiry Tracking are separate.
                entity.Property(i => i.HasBatchTracking)
                    .HasDefaultValue(true);

                entity.Property(i => i.HasExpiryTracking)
                    .HasDefaultValue(false);

                // Legacy compatibility field.
                // Old code used this as combined Batch / Expiry.
                // New Item Master will set this from HasExpiryTracking until all old code is updated.
                entity.Property(i => i.HasBatchExpiry)
                    .HasDefaultValue(false);

                entity.Property(i => i.IsScaleItem)
                    .HasDefaultValue(false);

                entity.Property(i => i.IsSerialized)
                    .HasDefaultValue(false);

                entity.Property(i => i.AllowCashierDiscount)
                    .HasDefaultValue(true);

                entity.Property(i => i.IsPurchaseLocked)
                    .HasDefaultValue(false);

                entity.Property(i => i.IsSaleLocked)
                    .HasDefaultValue(false);

                entity.Property(i => i.IsDeactivated)
                    .HasDefaultValue(false);

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

                entity.HasIndex(i => i.HasBatchTracking);

                entity.HasIndex(i => i.HasExpiryTracking);

                entity.HasIndex(i => i.HasBatchExpiry);

                entity.HasIndex(i => i.IsPurchaseLocked);

                entity.HasIndex(i => i.IsSaleLocked);

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

                // Enterprise batch audit:
                // Every posted GRN line should point to the exact ItemBatch it created or updated.
                entity.HasOne(l => l.ItemBatch)
                    .WithMany()
                    .HasForeignKey(l => l.ItemBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.GrnHeaderId);

                entity.HasIndex(l => l.ItemVariantId);

                entity.HasIndex(l => l.PoLineId);

                entity.HasIndex(l => l.ItemBatchId);

                entity.HasIndex(l => l.BatchNo);

                entity.HasIndex(l => l.LineStatus);

                // Prevent accidental duplicate same item/same batch line inside one GRN.
                // The ViewModel and repository should merge same item + same batch before posting.
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
            // CUSTOMER MASTER / CRM
            // =========================================================
            modelBuilder.Entity<CustomerMaster>(entity =>
            {
                entity.Property(c => c.CustomerCode)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(c => c.FullName)
                    .IsRequired()
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(c => c.Phone)
                    .HasMaxLength(30);

                entity.Property(c => c.Email)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.Address)
                    .HasMaxLength(300);

                entity.Property(c => c.NicNumber)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(c => c.CompanyName)
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(c => c.BusinessRegistrationNumber)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.VatRegistrationNumber)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.CustomerType)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Retail")
                    .UseCollation("NOCASE");

                entity.Property(c => c.IsDiscountEligible)
                    .HasDefaultValue(false);

                entity.Property(c => c.IsCreditEnabled)
                    .HasDefaultValue(false);

                entity.Property(c => c.CreditStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("None")
                    .UseCollation("NOCASE");

                entity.Property(c => c.CreditLimit)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.CurrentBalance)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.CreditDays)
                    .HasDefaultValue(0);

                entity.Property(c => c.IsCreditLocked)
                    .HasDefaultValue(false);

                entity.Property(c => c.IsActive)
                    .HasDefaultValue(true);

                entity.Property(c => c.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.UpdatedBy)
                    .HasMaxLength(100);

                // Temporary legacy fields.
                // Keep until old loyalty/profile pages are replaced.
                entity.Property(c => c.LoyaltyCardNumber)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.LoyaltyPointsBalance)
                    .HasColumnType("decimal(18,2)");

                entity.HasMany(c => c.LedgerEntries)
                    .WithOne(l => l.CustomerMaster)
                    .HasForeignKey(l => l.CustomerMasterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.CustomerCode)
                    .IsUnique();

                entity.HasIndex(c => c.Phone);

                entity.HasIndex(c => c.FullName);

                entity.HasIndex(c => c.CustomerType);

                entity.HasIndex(c => c.IsDiscountEligible);

                entity.HasIndex(c => c.IsCreditEnabled);

                entity.HasIndex(c => c.CreditStatus);

                entity.HasIndex(c => c.Birthday);

                entity.HasIndex(c => c.NicNumber);

                entity.HasIndex(c => c.BusinessRegistrationNumber);

                entity.HasIndex(c => c.VatRegistrationNumber);

                entity.HasIndex(c => c.IsCreditLocked);

                entity.HasIndex(c => c.IsActive);
            });

            // =========================================================
            // CUSTOMER LEDGER
            // =========================================================
            modelBuilder.Entity<CustomerLedger>(entity =>
            {
                entity.Property(l => l.DocumentRef)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.TransactionType)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.DebitAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.CreditAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.ProcessedBy)
                    .HasMaxLength(100);

                entity.Property(l => l.Remarks)
                    .HasMaxLength(255);

                entity.HasOne(l => l.CustomerMaster)
                    .WithMany(c => c.LedgerEntries)
                    .HasForeignKey(l => l.CustomerMasterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.CustomerMasterId);

                entity.HasIndex(l => l.TransactionDate);

                entity.HasIndex(l => l.DocumentRef);

                entity.HasIndex(l => l.TransactionType);
            });

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

            // =========================================================
            // DISCOUNT REASON MASTER
            // =========================================================
            modelBuilder.Entity<DiscountReason>(entity =>
            {
                entity.Property(r => r.ReasonCode)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(r => r.ReasonName)
                    .IsRequired()
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(r => r.Description)
                    .HasMaxLength(300);

                entity.Property(r => r.RequiresManagerApproval)
                    .HasDefaultValue(false);

                entity.Property(r => r.RequiresAdminApproval)
                    .HasDefaultValue(false);

                entity.Property(r => r.ManagerApprovalThreshold)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.IsActive)
                    .HasDefaultValue(true);

                entity.Property(r => r.DisplayOrder)
                    .HasDefaultValue(0);

                entity.Property(r => r.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.Remarks)
                    .HasMaxLength(500);

                entity.HasIndex(r => r.ReasonCode)
                    .IsUnique();

                entity.HasIndex(r => r.ReasonName);

                entity.HasIndex(r => r.IsActive);

                entity.HasIndex(r => r.DisplayOrder);

                entity.HasIndex(r => r.RequiresManagerApproval);

                entity.HasIndex(r => r.RequiresAdminApproval);
            });


            // =========================================================
            // DISCOUNT RULE MASTER
            // =========================================================
            modelBuilder.Entity<DiscountRule>(entity =>
            {
                entity.Property(r => r.RuleName)
                    .IsRequired()
                    .HasMaxLength(150)
                    .UseCollation("NOCASE");

                entity.Property(r => r.DiscountType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("Percent")
                    .UseCollation("NOCASE");

                entity.Property(r => r.DiscountValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.ReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(r => r.ReasonName)
                    .HasMaxLength(150);

                entity.Property(r => r.AppliesToType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("All")
                    .UseCollation("NOCASE");

                entity.Property(r => r.CategoryName)
                    .HasMaxLength(100);

                entity.Property(r => r.SubCategoryName)
                    .HasMaxLength(100);

                entity.Property(r => r.ItemName)
                    .HasMaxLength(150);

                entity.Property(r => r.SkuCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(r => r.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(r => r.CustomerType)
                    .HasMaxLength(30)
                    .HasDefaultValue("All")
                    .UseCollation("NOCASE");

                entity.Property(r => r.IsActive)
                    .HasDefaultValue(true);

                entity.Property(r => r.MaxDiscountAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.MaxDiscountPercent)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.MaxValuePerInvoice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.MaxValuePerDay)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.MaxQtyPerInvoice)
                    .HasColumnType("decimal(18,3)");

                entity.Property(r => r.MaxQtyPerDay)
                    .HasColumnType("decimal(18,3)");

                entity.Property(r => r.RequiresManagerApproval)
                    .HasDefaultValue(false);

                entity.Property(r => r.RequiresAdminApproval)
                    .HasDefaultValue(false);

                entity.Property(r => r.ManagerApprovalThreshold)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.AllowBelowMinimumPrice)
                    .HasDefaultValue(false);

                entity.Property(r => r.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.Remarks)
                    .HasMaxLength(500);

                entity.HasOne(r => r.DiscountReason)
                    .WithMany()
                    .HasForeignKey(r => r.DiscountReasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.RuleName);

                entity.HasIndex(r => r.DiscountType);

                entity.HasIndex(r => r.DiscountReasonId);

                entity.HasIndex(r => r.ReasonCode);

                entity.HasIndex(r => r.AppliesToType);

                entity.HasIndex(r => r.CategoryId);

                entity.HasIndex(r => r.SubCategoryId);

                entity.HasIndex(r => r.ItemParentId);

                entity.HasIndex(r => r.ItemVariantId);

                entity.HasIndex(r => r.CustomerType);

                entity.HasIndex(r => r.ValidFrom);

                entity.HasIndex(r => r.ValidTo);

                entity.HasIndex(r => r.IsActive);

                entity.HasIndex(r => r.RequiresManagerApproval);

                entity.HasIndex(r => r.RequiresAdminApproval);

                entity.HasIndex(r => r.AllowBelowMinimumPrice);
            });


            // =========================================================
            // SALES LINE DISCOUNT AUDIT
            // =========================================================
            modelBuilder.Entity<SalesLineDiscountAudit>(entity =>
            {
                // =====================================================
                // SALE REFERENCES
                // =====================================================

                entity.Property(a => a.InvoiceNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(a => a.CashierName)
                    .HasMaxLength(100);

                entity.Property(a => a.TerminalNo)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                // =====================================================
                // DISCOUNT SNAPSHOT
                // =====================================================

                entity.Property(a => a.DiscountRuleName)
                    .HasMaxLength(150);

                entity.Property(a => a.ReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(a => a.ReasonName)
                    .HasMaxLength(150);

                entity.Property(a => a.DiscountType)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(a => a.DiscountValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.DiscountAmount)
                    .HasColumnType("decimal(18,2)");

                // =====================================================
                // LINE VALUE SNAPSHOT
                // =====================================================

                entity.Property(a => a.OriginalUnitPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.Quantity)
                    .HasColumnType("decimal(18,3)");

                entity.Property(a => a.GrossAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.LineTotalAfterDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.CostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.ProfitAfterDiscount)
                    .HasColumnType("decimal(18,2)");

                // =====================================================
                // ITEM SNAPSHOT
                // =====================================================

                entity.Property(a => a.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(a => a.SkuCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(a => a.ItemDescription)
                    .HasMaxLength(200);

                entity.Property(a => a.BatchNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(a => a.Uom)
                    .HasMaxLength(20);

                // =====================================================
                // APPROVAL / AUDIT
                // =====================================================

                entity.Property(a => a.RequiresManagerApproval)
                    .HasDefaultValue(false);

                entity.Property(a => a.RequiresAdminApproval)
                    .HasDefaultValue(false);

                entity.Property(a => a.ApprovedBy)
                    .HasMaxLength(100);

                entity.Property(a => a.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(a => a.Remarks)
                    .HasMaxLength(500);

                // =====================================================
                // RELATIONSHIPS
                // =====================================================

                entity.HasOne(a => a.SalesHeader)
                    .WithMany()
                    .HasForeignKey(a => a.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.SalesLine)
                    .WithMany()
                    .HasForeignKey(a => a.SalesLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.DiscountRule)
                    .WithMany()
                    .HasForeignKey(a => a.DiscountRuleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.DiscountReason)
                    .WithMany()
                    .HasForeignKey(a => a.DiscountReasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                // =====================================================
                // INDEXES
                // =====================================================

                entity.HasIndex(a => a.SalesHeaderId);

                entity.HasIndex(a => a.SalesLineId);

                entity.HasIndex(a => a.InvoiceNo);

                entity.HasIndex(a => a.InvoiceDate);

                entity.HasIndex(a => a.CashierName);

                entity.HasIndex(a => a.TerminalNo);

                entity.HasIndex(a => a.DiscountRuleId);

                entity.HasIndex(a => a.DiscountReasonId);

                entity.HasIndex(a => a.ReasonCode);

                entity.HasIndex(a => a.DiscountType);

                entity.HasIndex(a => a.ItemVariantId);

                entity.HasIndex(a => a.ItemBatchId);

                entity.HasIndex(a => a.Barcode);

                entity.HasIndex(a => a.SkuCode);

                entity.HasIndex(a => a.RequiresManagerApproval);

                entity.HasIndex(a => a.RequiresAdminApproval);

                entity.HasIndex(a => a.ApprovedBy);

                entity.HasIndex(a => a.CreatedAt);
            });

            // =========================================================
            // SALES HEADER
            // =========================================================
            modelBuilder.Entity<SalesHeader>(entity =>
            {
                entity.Property(s => s.InvoiceNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(s => s.TerminalNo)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CashierName)
                    .IsRequired()
                    .HasMaxLength(100);

                // =====================================================
                // CUSTOMER SNAPSHOT
                // =====================================================

                entity.Property(s => s.CustomerCode)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CustomerName)
                    .HasMaxLength(150);

                entity.Property(s => s.CustomerCompanyName)
                    .HasMaxLength(150);

                entity.Property(s => s.CustomerPhone)
                    .HasMaxLength(30);

                entity.Property(s => s.CustomerType)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CustomerNicOrBrNumber)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CustomerCreditStatus)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(s => s.CustomerIsDiscountEligible)
                    .HasDefaultValue(false);

                entity.Property(s => s.CustomerIsCreditEnabled)
                    .HasDefaultValue(false);

                entity.Property(s => s.IsWholesaleSale)
                    .HasDefaultValue(false);

                entity.HasOne(s => s.CustomerMaster)
                    .WithMany()
                    .HasForeignKey(s => s.CustomerMasterId)
                    .OnDelete(DeleteBehavior.Restrict);

                // =====================================================
                // TOTALS / PAYMENT SUMMARY
                // =====================================================

                entity.Property(s => s.GrossTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(s => s.TotalDiscount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(s => s.NetTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(s => s.AmountTendered)
                    .HasColumnType("decimal(18,2)");

                entity.Property(s => s.BalanceReturned)
                    .HasColumnType("decimal(18,2)");

                entity.Property(s => s.PaymentMethod)
                    .HasMaxLength(50);

                entity.Property(s => s.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.HasOne<ShiftSession>()
                    .WithMany()
                    .HasForeignKey(s => s.ShiftSessionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.SalesLines)
                    .WithOne(l => l.SalesHeader)
                    .HasForeignKey(l => l.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(s => s.SalesPayments)
                    .WithOne(p => p.SalesHeader)
                    .HasForeignKey(p => p.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.InvoiceNo)
                    .IsUnique();

                entity.HasIndex(s => s.ShiftSessionId);

                entity.HasIndex(s => s.TerminalNo);

                entity.HasIndex(s => s.CashierName);

                entity.HasIndex(s => s.TransactionDate);

                entity.HasIndex(s => s.Status);

                entity.HasIndex(s => s.IsVoided);

                entity.HasIndex(s => s.CustomerMasterId);

                entity.HasIndex(s => s.CustomerCode);

                entity.HasIndex(s => s.CustomerPhone);

                entity.HasIndex(s => s.CustomerType);

                entity.HasIndex(s => s.CustomerIsDiscountEligible);

                entity.HasIndex(s => s.CustomerIsCreditEnabled);

                entity.HasIndex(s => s.IsWholesaleSale);
            });


            // =========================================================
            // SALES LINE
            // =========================================================
            modelBuilder.Entity<SalesLine>(entity =>
            {
                entity.Property(l => l.SkuCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(l => l.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(l => l.ItemDescription)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(l => l.BatchNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.Uom)
                    .HasMaxLength(20);

                entity.Property(l => l.Quantity)
                    .HasColumnType("decimal(18,3)");

                entity.Property(l => l.UnitPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.CostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.GrossAmount)
                    .HasColumnType("decimal(18,2)");

                // =========================================================
                // DISCOUNT / PRICE OVERRIDE / TOTALS
                // =========================================================

                entity.Property(l => l.DiscountPercentage)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.DiscountAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.ManualDiscountAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.DiscountMode)
                    .HasMaxLength(30)
                    .HasDefaultValue("None")
                    .UseCollation("NOCASE");

                entity.Property(l => l.IsManualDiscount)
                    .HasDefaultValue(false);

                entity.Property(l => l.IsPriceOverridden)
                    .HasDefaultValue(false);

                entity.Property(l => l.PriceOverrideAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.PriceOverrideApprovedBy)
                    .HasMaxLength(100);

                entity.Property(l => l.LineTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.ProfitAmount)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(l => l.DiscountMode);

                entity.HasIndex(l => l.IsManualDiscount);

                entity.HasIndex(l => l.IsPriceOverridden);

                entity.HasOne(l => l.SalesHeader)
                    .WithMany(h => h.SalesLines)
                    .HasForeignKey(l => l.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ItemVariant)
                    .WithMany()
                    .HasForeignKey(l => l.ItemVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ItemBatch)
                    .WithMany()
                    .HasForeignKey(l => l.ItemBatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.SalesHeaderId);

                entity.HasIndex(l => l.ItemVariantId);

                entity.HasIndex(l => l.ItemBatchId);

                entity.HasIndex(l => l.BatchNo);

                entity.HasIndex(l => l.ExpiryDate);

                entity.HasIndex(l => l.IsReturned);

                // Gift voucher sale line fields.
                entity.Property(l => l.IsGiftVoucherSale)
                    .HasDefaultValue(false);

                entity.Property(l => l.GiftVoucherNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.GiftVoucherBarcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.HasIndex(l => l.IsGiftVoucherSale);

                entity.HasIndex(l => l.GiftVoucherId);

                entity.HasIndex(l => l.GiftVoucherNo);

                entity.HasIndex(l => l.GiftVoucherBarcode);

                // =========================================================
                // FREE ISSUE SNAPSHOT FIELDS
                // =========================================================

                entity.Property(l => l.IsFreeItem)
    .HasDefaultValue(false);

                entity.Property(l => l.FreeIssueRuleName)
                    .HasMaxLength(150);

                entity.Property(l => l.FreeIssueType)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(l => l.FreeReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.FreeReasonText)
                    .HasMaxLength(150);

                entity.Property(l => l.FreeApprovedBy)
                    .HasMaxLength(100);

                entity.Property(l => l.OriginalUnitPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.FreeIssueCostValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.FreeIssueSellingValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(l => l.IsSupplierRecoverable)
                    .HasDefaultValue(false);

                entity.Property(l => l.SupplierName)
                    .HasMaxLength(150);

                entity.Property(l => l.SupplierPromotionReference)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(l => l.SupplierClaimStatus)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(l => l.SupplierClaimReferenceNo)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(l => l.SupplierClaimValue)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(l => l.IsFreeItem);
                entity.HasIndex(l => l.FreeIssueRuleId);
                entity.HasIndex(l => l.FreeIssueType);
                entity.HasIndex(l => l.FreeReasonCode);
                entity.HasIndex(l => l.IsSupplierRecoverable);
                entity.HasIndex(l => l.SupplierId);
                entity.HasIndex(l => l.SupplierClaimId);
                entity.HasIndex(l => l.SupplierClaimStatus);
                entity.HasIndex(l => l.SupplierClaimReferenceNo);

                // =========================================================
                // RULE-BASED DISCOUNT SNAPSHOT
                // =========================================================

                entity.Property(l => l.IsRuleDiscount)
                    .HasDefaultValue(false);

                entity.Property(l => l.DiscountRuleName)
                    .HasMaxLength(150);

                entity.Property(l => l.DiscountReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(l => l.DiscountReasonName)
                    .HasMaxLength(150);

                entity.Property(l => l.DiscountRequiresManagerApproval)
                    .HasDefaultValue(false);

                entity.Property(l => l.DiscountRequiresAdminApproval)
                    .HasDefaultValue(false);

                entity.Property(l => l.DiscountApprovedBy)
                    .HasMaxLength(100);

                entity.HasOne<DiscountRule>()
                    .WithMany()
                    .HasForeignKey(l => l.DiscountRuleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<DiscountReason>()
                    .WithMany()
                    .HasForeignKey(l => l.DiscountReasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => l.IsRuleDiscount);

                entity.HasIndex(l => l.DiscountRuleId);

                entity.HasIndex(l => l.DiscountReasonId);

                entity.HasIndex(l => l.DiscountReasonCode);

                entity.HasIndex(l => l.DiscountRequiresManagerApproval);

                entity.HasIndex(l => l.DiscountRequiresAdminApproval);

                entity.HasIndex(l => l.DiscountApprovedBy);
            });


            // =========================================================
            // SALES PAYMENT
            // =========================================================
            modelBuilder.Entity<SalesPayment>(entity =>
            {
                entity.Property(p => p.PaymentType)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(p => p.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.ReferenceNo)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(p => p.BankOrCardType)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.HasOne(p => p.SalesHeader)
                    .WithMany(h => h.SalesPayments)
                    .HasForeignKey(p => p.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => p.SalesHeaderId);

                entity.HasIndex(p => p.PaymentType);

                entity.HasIndex(p => p.ReferenceNo);

                entity.HasIndex(p => p.PaymentDate);

                entity.HasIndex(p => p.CreatedAt);

                // Gift voucher payment fields.
                entity.Property(p => p.GiftVoucherNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(p => p.GiftVoucherBarcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(p => p.GiftVoucherAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(p => p.GiftVoucherForfeitedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(p => p.GiftVoucherId);

                entity.HasIndex(p => p.GiftVoucherNo);

                entity.HasIndex(p => p.GiftVoucherBarcode);
            });

            // =========================================================
            // GIFT VOUCHERS
            // =========================================================
            modelBuilder.Entity<GiftVoucher>(entity =>
            {
                entity.Property(v => v.VoucherNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(v => v.Barcode)
                    .IsRequired()
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(v => v.VoucherAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.Status)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("Created")
                    .UseCollation("NOCASE");

                entity.Property(v => v.BatchNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(v => v.Description)
                    .HasMaxLength(100);

                entity.Property(v => v.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(v => v.PrintedBy)
                    .HasMaxLength(100);

                entity.Property(v => v.SoldInvoiceNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(v => v.SoldCashierName)
                    .HasMaxLength(100);

                entity.Property(v => v.SoldTerminalNo)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(v => v.RedeemedInvoiceNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(v => v.RedeemedCashierName)
                    .HasMaxLength(100);

                entity.Property(v => v.RedeemedTerminalNo)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(v => v.RedeemedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.ForfeitedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(v => v.BlockedBy)
                    .HasMaxLength(100);

                entity.Property(v => v.BlockReason)
                    .HasMaxLength(255);

                entity.Property(v => v.CancelledBy)
                    .HasMaxLength(100);

                entity.Property(v => v.CancelReason)
                    .HasMaxLength(255);

                entity.Property(v => v.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(v => v.Remarks)
                    .HasMaxLength(500);

                entity.HasOne(v => v.SoldSalesHeader)
                    .WithMany()
                    .HasForeignKey(v => v.SoldSalesHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.RedeemedSalesHeader)
                    .WithMany()
                    .HasForeignKey(v => v.RedeemedSalesHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(v => v.Transactions)
                    .WithOne(t => t.GiftVoucher)
                    .HasForeignKey(t => t.GiftVoucherId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(v => v.VoucherNo)
                    .IsUnique();

                entity.HasIndex(v => v.Barcode)
                    .IsUnique();

                entity.HasIndex(v => v.Status);

                entity.HasIndex(v => v.BatchNo);

                entity.HasIndex(v => v.VoucherAmount);

                entity.HasIndex(v => v.ExpiryDate);

                entity.HasIndex(v => v.CreatedAt);

                entity.HasIndex(v => v.ActivatedAt);

                entity.HasIndex(v => v.RedeemedDate);

                entity.HasIndex(v => v.SoldSalesHeaderId);

                entity.HasIndex(v => v.RedeemedSalesHeaderId);
            });


            // =========================================================
            // GIFT VOUCHER TRANSACTIONS
            // =========================================================
            modelBuilder.Entity<GiftVoucherTransaction>(entity =>
            {
                entity.Property(t => t.TransactionType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(t => t.VoucherNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(t => t.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(t => t.VoucherAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.AppliedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.ForfeitedAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.StatusAfter)
                    .HasMaxLength(30)
                    .UseCollation("NOCASE");

                entity.Property(t => t.ReferenceInvoiceNo)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(t => t.CashierName)
                    .HasMaxLength(100);

                entity.Property(t => t.TerminalNo)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(t => t.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(t => t.Remarks)
                    .HasMaxLength(500);

                entity.HasOne(t => t.GiftVoucher)
                    .WithMany(v => v.Transactions)
                    .HasForeignKey(t => t.GiftVoucherId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.SalesHeader)
                    .WithMany()
                    .HasForeignKey(t => t.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.GiftVoucherId);

                entity.HasIndex(t => t.TransactionDate);

                entity.HasIndex(t => t.TransactionType);

                entity.HasIndex(t => t.VoucherNo);

                entity.HasIndex(t => t.Barcode);

                entity.HasIndex(t => t.ReferenceInvoiceNo);

                entity.HasIndex(t => t.SalesHeaderId);
            });

            // =========================================================
            // FREE ISSUE RULES
            // =========================================================
            modelBuilder.Entity<FreeIssueRule>(entity =>
            {
                entity.Property(r => r.RuleName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(r => r.FreeIssueType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("ShopCost")
                    .UseCollation("NOCASE");

                entity.Property(r => r.ReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(r => r.ReasonName)
                    .HasMaxLength(150);

                entity.Property(r => r.SupplierName)
                    .HasMaxLength(150);

                entity.Property(r => r.SupplierPromotionReference)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(r => r.ClaimValueMode)
                    .HasMaxLength(30)
                    .HasDefaultValue("Cost")
                    .UseCollation("NOCASE");

                entity.Property(r => r.FixedClaimValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.AppliesToType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("ItemVariant")
                    .UseCollation("NOCASE");

                entity.Property(r => r.CategoryName)
                    .HasMaxLength(100);

                entity.Property(r => r.SubCategoryName)
                    .HasMaxLength(100);

                entity.Property(r => r.ItemName)
                    .HasMaxLength(150);

                entity.Property(r => r.SkuCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(r => r.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(r => r.MaxQtyPerInvoice)
                    .HasColumnType("decimal(18,3)");

                entity.Property(r => r.MaxQtyPerDay)
                    .HasColumnType("decimal(18,3)");

                entity.Property(r => r.MaxValuePerInvoice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.MaxValuePerDay)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.ManagerApprovalThreshold)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.Remarks)
                    .HasMaxLength(500);

                entity.HasIndex(r => r.RuleName);

                entity.HasIndex(r => r.FreeIssueType);

                entity.HasIndex(r => r.FreeIssueReasonId);

                entity.HasIndex(r => r.SupplierId);

                entity.HasIndex(r => r.AppliesToType);

                entity.HasIndex(r => r.CategoryId);

                entity.HasIndex(r => r.SubCategoryId);

                entity.HasIndex(r => r.ItemParentId);

                entity.HasIndex(r => r.ItemVariantId);

                entity.HasIndex(r => r.SkuCode);

                entity.HasIndex(r => r.Barcode);

                entity.HasIndex(r => r.ValidFrom);

                entity.HasIndex(r => r.ValidTo);

                entity.HasIndex(r => r.IsActive);
            });


            // =========================================================
            // FREE ISSUE REASONS
            // =========================================================
            modelBuilder.Entity<FreeIssueReason>(entity =>
            {
                entity.Property(r => r.ReasonCode)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(r => r.ReasonName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(r => r.FreeIssueType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("ShopCost")
                    .UseCollation("NOCASE");

                entity.Property(r => r.Description)
                    .HasMaxLength(500);

                entity.Property(r => r.ManagerApprovalThreshold)
                    .HasColumnType("decimal(18,2)");

                entity.Property(r => r.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(r => r.Remarks)
                    .HasMaxLength(500);

                entity.HasIndex(r => r.ReasonCode)
                    .IsUnique();

                entity.HasIndex(r => r.ReasonName);

                entity.HasIndex(r => r.FreeIssueType);

                entity.HasIndex(r => r.IsActive);

                entity.HasIndex(r => r.DisplayOrder);
            });


            // =========================================================
            // FREE ITEM SUPPLIER CLAIM LOGS
            // =========================================================
            modelBuilder.Entity<FreeItemClaimLog>(entity =>
            {
                entity.Property(c => c.InvoiceNo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.CashierName)
                    .HasMaxLength(100);

                entity.Property(c => c.TerminalNo)
                    .HasMaxLength(20)
                    .UseCollation("NOCASE");

                entity.Property(c => c.FreeIssueRuleName)
                    .HasMaxLength(150);

                entity.Property(c => c.FreeReasonCode)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.FreeReasonText)
                    .HasMaxLength(150);

                entity.Property(c => c.FreeIssueType)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("SupplierClaim")
                    .UseCollation("NOCASE");

                entity.Property(c => c.SupplierName)
                    .HasMaxLength(150);

                entity.Property(c => c.SupplierPromotionReference)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.Barcode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.SkuCode)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.ItemDescription)
                    .IsRequired()
                    .HasMaxLength(250);

                entity.Property(c => c.BatchNo)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.Uom)
                    .HasMaxLength(30)
                    .HasDefaultValue("PCS")
                    .UseCollation("NOCASE");

                entity.Property(c => c.Quantity)
                    .HasColumnType("decimal(18,3)");

                entity.Property(c => c.CostPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.OriginalUnitPrice)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.FreeIssueCostValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.FreeIssueSellingValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.ClaimValue)
                    .HasColumnType("decimal(18,2)");

                entity.Property(c => c.ClaimStatus)
                    .IsRequired()
                    .HasMaxLength(30)
                    .HasDefaultValue("Pending")
                    .UseCollation("NOCASE");

                entity.Property(c => c.ClaimReferenceNo)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.SubmittedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.SettledBy)
                    .HasMaxLength(100);

                entity.Property(c => c.SettlementType)
                    .HasMaxLength(50)
                    .UseCollation("NOCASE");

                entity.Property(c => c.SettlementReferenceNo)
                    .HasMaxLength(100)
                    .UseCollation("NOCASE");

                entity.Property(c => c.RejectedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.RejectReason)
                    .HasMaxLength(300);

                entity.Property(c => c.WrittenOffBy)
                    .HasMaxLength(100);

                entity.Property(c => c.WriteOffReason)
                    .HasMaxLength(300);

                entity.Property(c => c.CancelledBy)
                    .HasMaxLength(100);

                entity.Property(c => c.CancelReason)
                    .HasMaxLength(300);

                entity.Property(c => c.FreeApprovedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.UpdatedBy)
                    .HasMaxLength(100);

                entity.Property(c => c.Remarks)
                    .HasMaxLength(500);

                entity.HasOne(c => c.SalesHeader)
                    .WithMany()
                    .HasForeignKey(c => c.SalesHeaderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.SalesLine)
                    .WithMany()
                    .HasForeignKey(c => c.SalesLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.SalesHeaderId);

                entity.HasIndex(c => c.SalesLineId);

                entity.HasIndex(c => c.InvoiceNo);

                entity.HasIndex(c => c.InvoiceDate);

                entity.HasIndex(c => c.FreeIssueRuleId);

                entity.HasIndex(c => c.FreeReasonCode);

                entity.HasIndex(c => c.FreeIssueType);

                entity.HasIndex(c => c.SupplierId);

                entity.HasIndex(c => c.SupplierName);

                entity.HasIndex(c => c.ItemVariantId);

                entity.HasIndex(c => c.ItemBatchId);

                entity.HasIndex(c => c.Barcode);

                entity.HasIndex(c => c.SkuCode);

                entity.HasIndex(c => c.ClaimStatus);

                entity.HasIndex(c => c.ClaimReferenceNo);

                entity.HasIndex(c => c.CreatedAt);

                entity.HasIndex(c => c.SubmittedAt);

                entity.HasIndex(c => c.SettledAt);

                entity.HasIndex(c => c.RejectedAt);

                entity.HasIndex(c => c.WrittenOffAt);

                entity.HasIndex(c => c.CancelledAt);
            });
        }


    }
}