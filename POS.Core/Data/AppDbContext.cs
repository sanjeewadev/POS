using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using System.Collections.Generic;

namespace POS.Core.Data
{
    public class AppDbContext : DbContext

    {
        // 1. Add these two constructors so the Factory can configure it
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        // These will become your actual SQLite tables
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<AttributeValue> AttributeValues { get; set; }
        public DbSet<AttributeGroup> AttributeGroups { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }

        public DbSet<Item> Items { get; set; }

        // ---> NEW TABLES FOR GRN <---
        public DbSet<GrnHeader> GrnHeaders { get; set; }
        public DbSet<GrnDetail> GrnDetails { get; set; }
        public DbSet<ProductUOM> ProductUOMs { get; set; }
        public DbSet<StockBatch> StockBatches { get; set; }
        //public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SyncOutbox> SyncOutbox { get; set; }

        public DbSet<Creditor> Creditors { get; set; }
        //public DbSet<CreditLedger> CreditLedgers { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Expense> Expenses { get; set; }

        // This configures the local SQLite database file
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // This creates a file named 'pos_local.db' in the folder where the app runs
            optionsBuilder.UseSqlite("Data Source=pos_local.db");
        }
    }
}