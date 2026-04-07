// src/main/database.ts
import Database from 'better-sqlite3'
import { app } from 'electron'
import path from 'path'
import fs from 'fs'

let db: Database.Database

export function initDatabase(): Database.Database {
  // 1. Use the hidden Windows AppData folder for security
  const appDataPath = path.join(app.getPath('appData'), 'pos')

  if (!fs.existsSync(appDataPath)) {
    fs.mkdirSync(appDataPath, { recursive: true })
  }

  const dbPath = path.join(appDataPath, 'inventory.db')
  console.log('📦 Database Path:', dbPath)

  // 2. Open the connection
  db = new Database(dbPath, { verbose: console.log })

  // 3. Performance & Security Pragma Rules
  db.pragma('journal_mode = WAL') // Write-Ahead Logging for high-speed concurrent saves
  db.pragma('foreign_keys = ON') // CRITICAL: Enforces Cascade Deletes

  // 4. Generate the Complete Enterprise Schema
  db.exec(`
    BEGIN TRANSACTION;

    -- 0. 🚀 NEW: SHOP SETTINGS TABLE
    CREATE TABLE IF NOT EXISTS ShopSettings (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ShopName TEXT NOT NULL DEFAULT 'My POS System',
        ShopAddress TEXT,
        ShopPhone TEXT,
        ReceiptFooter TEXT DEFAULT 'Thank you for your business!'
    );

    -- 1. USERS TABLE
    CREATE TABLE IF NOT EXISTS Users (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Username TEXT UNIQUE NOT NULL,
        PasswordHash TEXT NOT NULL,
        FullName TEXT NOT NULL,
        Role INTEGER NOT NULL, -- Enum: 0=SuperAdmin, 1=Admin, 2=Employee
        IsActive INTEGER NOT NULL DEFAULT 1, -- Boolean: 1=True, 0=False
        Permissions TEXT,
        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
    );

    -- 2. CATEGORIES TABLE (With Self-Referencing Parent/Child)
    CREATE TABLE IF NOT EXISTS Categories (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        ParentId INTEGER NULL,
        FOREIGN KEY(ParentId) REFERENCES Categories(Id) ON DELETE CASCADE
    );

    -- 3. SUPPLIERS TABLE
    CREATE TABLE IF NOT EXISTS Suppliers (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT UNIQUE NOT NULL,
        Phone TEXT,
        Note TEXT
    );

    -- 4. PURCHASE INVOICES TABLE
    CREATE TABLE IF NOT EXISTS PurchaseInvoices (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        BillNumber TEXT NOT NULL,
        Date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        TotalAmount REAL NOT NULL DEFAULT 0,
        Note TEXT,
        Status INTEGER NOT NULL DEFAULT 0, -- Enum: 0=Draft, 1=Posted
        SupplierId INTEGER NOT NULL,
        FOREIGN KEY(SupplierId) REFERENCES Suppliers(Id) ON DELETE RESTRICT
    );

    -- 5. PRODUCTS TABLE (🚀 UPDATED with PrintName and QuantityType)
    CREATE TABLE IF NOT EXISTS Products (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        PrintName TEXT, -- For short thermal receipts
        Barcode TEXT UNIQUE,
        Description TEXT,
        Unit TEXT DEFAULT 'Pcs',
        QuantityType TEXT DEFAULT 'quantity', -- 'kg' or 'quantity' for weight support
        CategoryId INTEGER NOT NULL,
        BuyingPrice REAL NOT NULL DEFAULT 0,
        SellingPrice REAL NOT NULL DEFAULT 0,
        DiscountLimit REAL NOT NULL DEFAULT 0,
        Quantity REAL NOT NULL DEFAULT 0,
        IsActive INTEGER NOT NULL DEFAULT 1, -- Built-in Soft Delete!
        FOREIGN KEY(CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
    );

    -- 6. SALES TRANSACTIONS TABLE (🚀 UPDATED with Cash Drawer Math)
    CREATE TABLE IF NOT EXISTS SalesTransactions (
        ReceiptId TEXT PRIMARY KEY NOT NULL,
        TransactionDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        TotalAmount REAL NOT NULL DEFAULT 0,
        PaidAmount REAL NOT NULL DEFAULT 0,
        CashReceived REAL NOT NULL DEFAULT 0, -- Exact cash handed by customer
        ChangeGiven REAL NOT NULL DEFAULT 0,  -- Exact change handed back
        IsCredit INTEGER NOT NULL DEFAULT 0, -- Boolean
        CustomerName TEXT,
        Status INTEGER NOT NULL DEFAULT 0 -- Enum: 0=Paid, 1=Unpaid, 2=PartiallyPaid
    );

    -- 7. CREDIT PAYMENT LOGS TABLE
    CREATE TABLE IF NOT EXISTS CreditPaymentLogs (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ReceiptId TEXT NOT NULL,
        AmountPaid REAL NOT NULL,
        PaymentDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        Note TEXT,
        FOREIGN KEY(ReceiptId) REFERENCES SalesTransactions(ReceiptId) ON DELETE CASCADE
    );

    -- 8. STOCK BATCHES TABLE (🚀 UPDATED with Scheduled Discounts)
    CREATE TABLE IF NOT EXISTS StockBatches (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ProductId INTEGER NOT NULL,
        InitialQuantity REAL NOT NULL DEFAULT 0,
        RemainingQuantity REAL NOT NULL DEFAULT 0,
        CostPrice REAL NOT NULL DEFAULT 0,
        SellingPrice REAL NOT NULL DEFAULT 0,
        Discount REAL NOT NULL DEFAULT 0,
        DiscountType TEXT DEFAULT 'percentage', -- 'percentage' or 'fixed'
        DiscountStartDate DATETIME NULL,
        DiscountEndDate DATETIME NULL,
        DiscountCode TEXT,
        ReceivedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        PurchaseInvoiceId INTEGER NULL,
        FOREIGN KEY(ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
        FOREIGN KEY(PurchaseInvoiceId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE
    );

    -- 9. STOCK MOVEMENTS TABLE (The Core Audit Trail)
    CREATE TABLE IF NOT EXISTS StockMovements (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        ProductId INTEGER NOT NULL,
        Type INTEGER NOT NULL, -- Enum: 1=In, 2=Out, 3=Adjust, 4=SalesReturn, 5=PurchaseReturn
        Quantity REAL NOT NULL DEFAULT 0,
        ReturnedQuantity REAL NOT NULL DEFAULT 0,
        UnitCost REAL NOT NULL DEFAULT 0,
        UnitPrice REAL NOT NULL DEFAULT 0,
        StockBatchId INTEGER NULL,
        Reason INTEGER NOT NULL DEFAULT 0, -- Enum: 0=Correction, 1=Lost
        IsVoided INTEGER NOT NULL DEFAULT 0, -- Boolean
        Note TEXT,
        ReceiptId TEXT,
        FOREIGN KEY(ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
        FOREIGN KEY(StockBatchId) REFERENCES StockBatches(Id) ON DELETE SET NULL
    );

    -- 10. 🚀 NEW: RETURNS TABLE
    CREATE TABLE IF NOT EXISTS Returns (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ReceiptId TEXT NOT NULL,
        BatchId INTEGER NOT NULL,
        Quantity REAL NOT NULL,
        RefundAmount REAL NOT NULL,
        Reason TEXT,
        ReturnDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY(ReceiptId) REFERENCES SalesTransactions(ReceiptId),
        FOREIGN KEY(BatchId) REFERENCES StockBatches(Id)
    );

    COMMIT;
  `)

  // 5. Seed the Default SuperAdmin
  const userCount = db.prepare('SELECT COUNT(*) as count FROM Users').get() as { count: number }
  if (userCount.count === 0) {
    db.prepare(
      `
      INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, Permissions)
      VALUES (?, ?, ?, ?, ?, ?)
    `
    ).run(
      'admin',
      '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', // Exact C# Hash for 'admin123'
      'Super Admin',
      0, // UserRole.SuperAdmin
      1, // IsActive = True
      'ALL'
    )
    console.log('👑 Default SuperAdmin account created!')
  }

  // 6. 🚀 NEW: Seed Default Shop Settings
  const settingsCount = db.prepare('SELECT COUNT(*) as count FROM ShopSettings').get() as {
    count: number
  }
  if (settingsCount.count === 0) {
    db.prepare(
      `INSERT INTO ShopSettings (ShopName, ShopAddress, ShopPhone, ReceiptFooter) VALUES (?, ?, ?, ?)`
    ).run('Universal POS', '123 Main Street', '011-2345678', 'Thank you, come again!')
    console.log('🏪 Default Shop Settings created!')
  }

  console.log('✅ Database Engine Initialized Successfully.')
  return db
}

export function getDb(): Database.Database {
  if (!db) throw new Error('Database not initialized yet!')
  return db
}

// ============================================================================
// 📊 REPORTING & DASHBOARD QUERIES
// Note: We will move these to reportRepo.ts later, but keeping them here so your app doesn't break!
// ============================================================================

export function getDashboardMetrics() {
  const database = getDb()

  const salesResult = database
    .prepare(
      `
    SELECT SUM(PaidAmount) as total 
    FROM SalesTransactions 
    WHERE date(TransactionDate, 'localtime') = date('now', 'localtime') 
    AND IsCredit = 0
  `
    )
    .get() as { total: number | null }

  const billsResult = database
    .prepare(
      `
    SELECT COUNT(*) as count 
    FROM SalesTransactions 
    WHERE date(TransactionDate, 'localtime') = date('now', 'localtime')
  `
    )
    .get() as { count: number }

  const creditResult = database
    .prepare(
      `
    SELECT SUM(TotalAmount - PaidAmount) as debt 
    FROM SalesTransactions 
    WHERE Status != 0
  `
    )
    .get() as { debt: number | null }

  return {
    todaySales: salesResult?.total || 0,
    billsToday: billsResult?.count || 0,
    pendingCredit: creditResult?.debt || 0
  }
}

export function getRecentTransactions(limit: number = 5) {
  const database = getDb()

  return database
    .prepare(
      `
    SELECT ReceiptId, TransactionDate, CustomerName, TotalAmount, Status 
    FROM SalesTransactions 
    ORDER BY TransactionDate DESC 
    LIMIT ?
  `
    )
    .all(limit)
}

export function getSalesHistory(dateStr: string, search: string = '') {
  const database = getDb()
  const searchTerm = `%${search}%`

  return database
    .prepare(
      `
    SELECT * FROM SalesTransactions 
    WHERE date(TransactionDate, 'localtime') = ? 
    AND (ReceiptId LIKE ? OR CustomerName LIKE ?)
    ORDER BY TransactionDate DESC
  `
    )
    .all(dateStr, searchTerm, searchTerm)
}

export function getReceiptDetails(receiptId: string) {
  const database = getDb()

  return database
    .prepare(
      `
    SELECT sm.Quantity, sm.UnitPrice, p.Name as ProductName 
    FROM StockMovements sm
    LEFT JOIN Products p ON sm.ProductId = p.Id
    WHERE sm.ReceiptId = ?
  `
    )
    .all(receiptId)
}
