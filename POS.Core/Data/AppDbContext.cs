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
        public DbSet<Item> Items { get; set; }
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

        // FIXED: Changed to 'UnitsOfMeasure' to perfectly match the UI bindings
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }

        // --- PROCUREMENT & TRANSACTIONS ---
        public DbSet<GrnHeader> GrnHeaders { get; set; }
        public DbSet<GrnLine> GrnLines { get; set; }
        public DbSet<PoHeader> PoHeaders { get; set; }
        public DbSet<PoLine> PoLines { get; set; }
        public DbSet<ReturnHeader> ReturnHeaders { get; set; }
        public DbSet<ReturnLine> ReturnLines { get; set; }
        public DbSet<StockAdjustmentHeader> StockAdjustmentHeaders { get; set; }
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dbFolder = System.IO.Path.Combine(appData, "POS");
                string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");

                // Now it uses the same shared path as your App!
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        // CRITICAL FOR MATRIX INVENTORY: Maps the Many-to-Many junction tables properly
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Make Usernames unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // ADD THIS BLOCK: Make Barcodes unique and Lightning Fast to search
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.Barcode)
                .IsUnique();

            modelBuilder.Entity<CategoryAttributeGroup>()
                .HasKey(c => new { c.CategoryId, c.AttributeGroupId });

            modelBuilder.Entity<ItemPropertyMapping>()
                .HasKey(m => new { m.ItemVariantId, m.AttributeGroupId, m.AttributeValueId });
        }
    }
}