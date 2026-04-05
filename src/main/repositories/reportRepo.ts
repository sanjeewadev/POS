import { getDb } from '../database'

// ==========================================
// 📈 1. DASHBOARD KPI METRICS
// ==========================================
export function getDashboardMetrics() {
  const db = getDb()

  const salesData = db
    .prepare(
      `
    SELECT SUM(PaidAmount) as grossSales 
    FROM SalesTransactions 
    WHERE date(TransactionDate, 'localtime') = date('now', 'localtime') AND Status != 3
  `
    )
    .get() as { grossSales: number }

  const costData = db
    .prepare(
      `
    SELECT SUM(Quantity * UnitCost) as totalCost 
    FROM StockMovements 
    WHERE Type = 2 AND date(Date, 'localtime') = date('now', 'localtime') AND IsVoided = 0
  `
    )
    .get() as { totalCost: number }

  const grossSales = salesData?.grossSales || 0
  const totalCost = costData?.totalCost || 0
  const netProfit = grossSales - totalCost

  const pendingCredit = db
    .prepare(
      `
    SELECT SUM(TotalAmount - PaidAmount) as total 
    FROM SalesTransactions 
    WHERE IsCredit = 1 AND Status IN (1, 2)
  `
    )
    .get() as { total: number }

  return {
    grossSales: grossSales,
    netProfit: netProfit,
    pendingCredit: pendingCredit?.total || 0
  }
}

// ==========================================
// 📊 2. BUSINESS INTELLIGENCE (CHART & LISTS)
// ==========================================
export function getChartData(filter: '7_days' | 'this_month' = '7_days') {
  const db = getDb()
  let dateModifier = '-7 days'
  if (filter === 'this_month') dateModifier = 'start of month'

  return db
    .prepare(
      `
    WITH DailyRevenue AS (
      SELECT date(TransactionDate, 'localtime') as dateLabel, SUM(PaidAmount) as sales
      FROM SalesTransactions
      WHERE Status != 3 AND date(TransactionDate, 'localtime') >= date('now', 'localtime', ?)
      GROUP BY date(TransactionDate, 'localtime')
    ),
    DailyCost AS (
      SELECT date(Date, 'localtime') as dateLabel, SUM(Quantity * UnitCost) as cost
      FROM StockMovements
      WHERE Type = 2 AND IsVoided = 0 AND date(Date, 'localtime') >= date('now', 'localtime', ?)
      GROUP BY date(Date, 'localtime')
    )
    SELECT 
      r.dateLabel,
      r.sales,
      (r.sales - COALESCE(c.cost, 0)) as profit
    FROM DailyRevenue r
    LEFT JOIN DailyCost c ON r.dateLabel = c.dateLabel
    ORDER BY r.dateLabel ASC
  `
    )
    .all(dateModifier, dateModifier)
}

export function getTopSellers(limit: number = 5) {
  const db = getDb()
  return db
    .prepare(
      `
    SELECT p.Name, SUM(m.Quantity) as TotalSold, SUM(m.Quantity * m.UnitPrice) as Revenue
    FROM StockMovements m
    JOIN Products p ON m.ProductId = p.Id
    WHERE m.Type = 2 AND m.IsVoided = 0 AND date(m.Date, 'localtime') >= date('now', 'localtime', 'start of month')
    GROUP BY m.ProductId
    ORDER BY Revenue DESC
    LIMIT ?
  `
    )
    .all(limit)
}

export function getLowStockAlerts(threshold: number = 10) {
  const db = getDb()
  return db
    .prepare(
      `
      SELECT Id, Name, Barcode, Quantity, Unit 
      FROM Products 
      WHERE IsActive = 1 AND Quantity <= ?
      ORDER BY Quantity ASC 
    `
    )
    .all(threshold)
}

// ==========================================
// 📜 3. SALES HISTORY & RECEIPTS
// ==========================================
export function getSalesHistory(startDate: string, endDate: string, search: string) {
  const db = getDb()
  let query = `SELECT * FROM SalesTransactions WHERE date(TransactionDate, 'localtime') BETWEEN date(?) AND date(?)`
  const params: any[] = [startDate, endDate]

  if (search) {
    query += ' AND (ReceiptId LIKE ? OR CustomerName LIKE ?)'
    params.push(`%${search}%`, `%${search}%`)
  }
  query += ' ORDER BY TransactionDate DESC'
  return db.prepare(query).all(...params)
}

export function getReceiptDetails(receiptId: string) {
  const db = getDb()
  const transaction = db
    .prepare('SELECT * FROM SalesTransactions WHERE ReceiptId = ?')
    .get(receiptId)
  const items = db
    .prepare(
      `
    SELECT m.*, p.Name as ProductName 
    FROM StockMovements m
    JOIN Products p ON m.ProductId = p.Id
    WHERE m.ReceiptId = ? AND m.Type = 2
  `
    )
    .all(receiptId)
  return { transaction, items }
}

export function getTodaySales() {
  return getDb()
    .prepare(
      `
    SELECT * FROM SalesTransactions 
    WHERE date(TransactionDate, 'localtime') = date('now', 'localtime')
    ORDER BY TransactionDate DESC
  `
    )
    .all()
}

export function getReceiptItems(receiptId: string) {
  return getDb()
    .prepare(
      `
    SELECT m.*, p.Name as ProductName, p.Unit, p.Barcode, p.SellingPrice as OriginalPrice 
    FROM StockMovements m
    JOIN Products p ON m.ProductId = p.Id
    WHERE m.ReceiptId = ? AND m.Type = 2
  `
    )
    .all(receiptId)
}

export function getBillForReturn(receiptId: string) {
  const db = getDb()
  const txn = db.prepare('SELECT * FROM SalesTransactions WHERE ReceiptId = ?').get(receiptId)
  if (!txn) return null
  const items = db
    .prepare(
      `
    SELECT m.ProductId, p.Name as ProductName, p.Unit, m.StockBatchId, m.UnitPrice, m.UnitCost,
      SUM(CASE WHEN m.Type = 2 THEN m.Quantity ELSE 0 END) as OriginalQty,
      SUM(CASE WHEN m.Type = 4 THEN m.Quantity ELSE 0 END) as ReturnedQty
    FROM StockMovements m
    JOIN Products p ON m.ProductId = p.Id
    WHERE m.ReceiptId = ? AND m.IsVoided = 0
    GROUP BY m.ProductId, m.StockBatchId
  `
    )
    .all(receiptId)
  return { transaction: txn, items }
}

// ==========================================
// 💳 4. CREDIT & DEBT MANAGEMENT
// ==========================================

export function getPendingCreditAccounts() {
  const db = getDb()
  return db
    .prepare(
      `
    SELECT 
      ReceiptId,
      TransactionDate,
      CustomerName,
      TotalAmount as TotalCredit,
      PaidAmount as TotalPaid,
      (TotalAmount - PaidAmount) as TotalPending
    FROM SalesTransactions
    WHERE IsCredit = 1 
      AND Status IN (1, 2) 
      AND (TotalAmount - PaidAmount) > 0
    ORDER BY TransactionDate DESC
  `
    )
    .all()
}

export function processCreditPayment(receiptId: string, amountToPay: number) {
  const db = getDb()

  const payTxn = db.transaction((rId, amount) => {
    // 1. Double check the specific bill
    const bill: any = db
      .prepare('SELECT TotalAmount, PaidAmount FROM SalesTransactions WHERE ReceiptId = ?')
      .get(rId)

    if (!bill) throw new Error('Invoice not found!')

    // 🚀 THE FIX: Database-Level Overpayment Protection
    const remainingDebt = bill.TotalAmount - bill.PaidAmount
    if (amount > remainingDebt + 0.01) {
      throw new Error(
        `CRITICAL: Payment of Rs ${amount} exceeds the remaining debt of Rs ${remainingDebt.toFixed(2)}`
      )
    }

    // 2. Safely apply payment and perfectly calculate new Status (0 = Fully Paid, 2 = Partially Paid)
    db.prepare(
      `
      UPDATE SalesTransactions 
      SET PaidAmount = PaidAmount + ?, 
          Status = CASE WHEN PaidAmount + ? >= TotalAmount - 0.01 THEN 0 ELSE 2 END
      WHERE ReceiptId = ?
    `
    ).run(amount, amount, rId)
  })

  payTxn(receiptId, amountToPay)
  return { success: true }
}

// ==========================================
// 🛡️ 5. AUDIT & SECURITY LOGS
// ==========================================
export function getAuditLogs(startDate: string, endDate: string) {
  const db = getDb()
  return db
    .prepare(
      `
    SELECT m.Id, m.Date, m.Type, m.Quantity, m.UnitPrice, m.UnitCost, m.Note, m.Reason, m.IsVoided, m.ReceiptId, p.Name as ProductName, p.Unit
    FROM StockMovements m
    JOIN Products p ON m.ProductId = p.Id
    WHERE date(m.Date, 'localtime') BETWEEN date(?) AND date(?)
      AND (m.Type IN (3, 4) OR m.IsVoided = 1)
    ORDER BY m.Date DESC
  `
    )
    .all(startDate, endDate)
}

export function getDashboardDataFromDB(startDate: string, endDate: string) {
  const db = getDb()

  // Append 23:59:59 so it includes sales made at the end of the day!
  const endDateTime = endDate + ' 23:59:59'

  // 1. Gross Sales
  const salesResult = db
    .prepare(
      `
    SELECT SUM(TotalAmount) as total 
    FROM SalesTransactions 
    WHERE Status != 3 AND TransactionDate BETWEEN ? AND ?
  `
    )
    .get(startDate, endDateTime) as any

  // 2. Net Profit (🚀 FIXED: Subtracts profit lost from Returns!)
  const profitResult = db
    .prepare(
      `
    SELECT 
      SUM(CASE WHEN Type = 2 THEN (UnitPrice - UnitCost) * Quantity ELSE 0 END) -
      SUM(CASE WHEN Type = 4 THEN (UnitPrice - UnitCost) * Quantity ELSE 0 END) as total
    FROM StockMovements
    WHERE ReceiptId IN (
      SELECT ReceiptId FROM SalesTransactions 
      WHERE Status != 3 AND TransactionDate BETWEEN ? AND ?
    ) AND Type IN (2, 4)
  `
    )
    .get(startDate, endDateTime) as any

  // 3. Pending Credit
  const creditResult = db
    .prepare(
      `
    SELECT SUM(TotalAmount - PaidAmount) as total 
    FROM SalesTransactions 
    WHERE IsCredit = 1 AND Status IN (1, 2) AND Status != 3
  `
    )
    .get() as any

  // 4. Chart Data
  const chartRows = db
    .prepare(
      `
    SELECT 
      DATE(TransactionDate) as dateLabel,
      SUM(TotalAmount) as sales
    FROM SalesTransactions
    WHERE Status != 3 AND TransactionDate BETWEEN ? AND ?
    GROUP BY DATE(TransactionDate)
    ORDER BY DATE(TransactionDate) ASC
  `
    )
    .all(startDate, endDateTime) as any[]

  // 🚀 FIXED: Daily chart profit now properly deducts returns!
  const profitStmt = db.prepare(`
    SELECT 
      SUM(CASE WHEN Type = 2 THEN (UnitPrice - UnitCost) * Quantity ELSE 0 END) -
      SUM(CASE WHEN Type = 4 THEN (UnitPrice - UnitCost) * Quantity ELSE 0 END) as dailyProfit
    FROM StockMovements
    WHERE ReceiptId IN (
      SELECT ReceiptId FROM SalesTransactions 
      WHERE Status != 3 AND DATE(TransactionDate) = ?
    ) AND Type IN (2, 4)
  `)

  const finalChartData = chartRows.map((row) => {
    const dailyProfitRow = profitStmt.get(row.dateLabel) as any
    return {
      dateLabel: row.dateLabel,
      sales: row.sales || 0,
      profit: dailyProfitRow.dailyProfit || 0
    }
  })

  return {
    metrics: {
      grossSales: salesResult.total || 0,
      netProfit: profitResult.total || 0,
      pendingCredit: creditResult.total || 0
    },
    chartData: finalChartData
  }
}
