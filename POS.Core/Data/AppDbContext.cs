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
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        // --- MATRIX INVENTORY ---
        public DbSet<AttributeGroup> AttributeGroups { get; set; }
        public DbSet<AttributeValue> AttributeValues { get; set; }
        public DbSet<CategoryAttributeGroup> CategoryAttributeGroups { get; set; }
        public DbSet<ItemParent> ItemParents { get; set; }
        public DbSet<ItemVariant> ItemVariants { get; set; }
        public DbSet<ItemPropertyMapping> ItemPropertyMappings { get; set; }
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
        public DbSet<ItemBatch> ItemBatches { get; set; }

        // --- ENTERPRISE LEDGERS ---
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<SupplierLedger> SupplierLedgers { get; set; }
        public DbSet<DocumentSequence> DocumentSequences { get; set; }

        // --- PROCUREMENT & TRANSACTIONS (B2B) ---
        public DbSet<GrnHeader> GrnHeaders { get; set; }
        public DbSet<GrnLine> GrnLines { get; set; }
        public DbSet<PoHeader> PoHeaders { get; set; }
        public DbSet<PoLine> PoLines { get; set; }
        public DbSet<ReturnHeader> ReturnHeaders { get; set; } // Supplier Returns
        public DbSet<ReturnLine> ReturnLines { get; set; }
        public DbSet<StockAdjustmentHeader> StockAdjustmentHeaders { get; set; }
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }

        // --- CRM & CUSTOMER FINANCE (NEW) ---
        public DbSet<CustomerMaster> CustomerMasters { get; set; }
        public DbSet<CustomerLedger> CustomerLedgers { get; set; }

        // --- LOYALTY & PROMOTIONS ENGINE (NEW) ---
        public DbSet<PromoRule> PromoRules { get; set; }
        public DbSet<PromoCondition> PromoConditions { get; set; }
        public DbSet<PromoReward> PromoRewards { get; set; }

        public DbSet<POS.Core.Models.LoyaltyDiscountProfile> LoyaltyDiscountProfiles { get; set; }

        public DbSet<POS.Core.Models.ExpressItemLayout> ExpressItemLayouts { get; set; }

        // --- CASHIER & SALES ENGINE ---
        public DbSet<SalesHeader> SalesHeaders { get; set; }
        public DbSet<SalesLine> SalesLines { get; set; }
        public DbSet<CustomerReturnHeader> CustomerReturnHeaders { get; set; } // Customer Refunds
        public DbSet<CustomerReturnLine> CustomerReturnLines { get; set; }

        public DbSet<ItemSupplier> ItemSuppliers { get; set; } = null!;

        public DbSet<QuotationHeader> QuotationHeaders { get; set; }
        public DbSet<QuotationLine> QuotationLines { get; set; }

        // --- CASHIER & SALES ENGINE ---
        public DbSet<SalesPayment> SalesPayments { get; set; } // <--- ADD THIS LINE

        // --- SHIFT & SECURITY ---
        public DbSet<ShiftSession> ShiftSessions { get; set; }
        public DbSet<CashMovement> CashMovements { get; set; }

        public DbSet<SystemSetting> SystemSettings { get; set; }


        public DbSet<GiftVoucher> GiftVouchers { get; set; }

        public DbSet<FreeItemClaimLog> FreeItemClaims { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dbFolder = System.IO.Path.Combine(appData, "POS");
                string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Make Usernames unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Composite Key for Category to Attribute Group Mapping
            modelBuilder.Entity<CategoryAttributeGroup>()
                .HasKey(c => new { c.CategoryId, c.AttributeGroupId });

            // Composite Key for Variant Property Mapping DNA
            modelBuilder.Entity<ItemPropertyMapping>()
                .HasKey(m => new { m.ItemVariantId, m.AttributeGroupId, m.AttributeValueId });

            // Ensure Reference Documents are indexed for fast searching in ledgers
            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(i => i.ReferenceDocument);
            modelBuilder.Entity<SupplierLedger>()
                .HasIndex(s => s.ReferenceDocument);

            // SEED DATA: Pre-load the GRN sequence so the system knows where to start counting
            modelBuilder.Entity<DocumentSequence>().HasData(
                new DocumentSequence { DocumentType = "GRN", Prefix = "GRN-", NextSequenceNumber = 1, PaddingLength = 5, UpdatedAt = DateTime.Now }
            );

            modelBuilder.Entity<CashMovement>()
        .HasIndex(c => c.ReferenceVoucherNo)
        .IsUnique();

            modelBuilder.Entity<CashMovement>()
                .HasIndex(c => c.Timestamp);
        }
    }
}