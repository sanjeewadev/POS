// src/main/repositories/stockRepo.ts
import { getDb } from '../database'

// ==========================================
// 🛒 1. THE CHECKOUT ENGINE
// ==========================================
export function processCompleteSale(transaction: any, movements: any[]) {
  const db = getDb()

  const checkoutTransaction = db.transaction((txn, movs) => {
    // 🚀 NEW: Added CashReceived and ChangeGiven to prevent cashier math errors!
    db.prepare(
      `INSERT INTO SalesTransactions (ReceiptId, TransactionDate, TotalAmount, PaidAmount, CashReceived, ChangeGiven, IsCredit, CustomerName, Status)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`
    ).run(
      txn.ReceiptId,
      txn.TransactionDate,
      txn.TotalAmount,
      txn.PaidAmount,
      txn.CashReceived || 0,
      txn.ChangeGiven || 0,
      txn.IsCredit ? 1 : 0,
      txn.CustomerName,
      txn.Status
    )

    const updateBatchStmt = db.prepare('UPDATE StockBatches SET RemainingQuantity = ? WHERE Id = ?')
    const updateProductStmt = db.prepare('UPDATE Products SET Quantity = ? WHERE Id = ?')
    const insertMovementStmt = db.prepare(`
      INSERT INTO StockMovements (Date, ProductId, Type, Quantity, UnitCost, UnitPrice, StockBatchId, Reason, IsVoided, Note, ReceiptId)
      VALUES (?, ?, ?, ?, ?, ?, ?, 0, 0, ?, ?)
    `)

    for (const move of movs) {
      const batch: any = db
        .prepare('SELECT RemainingQuantity, CostPrice FROM StockBatches WHERE Id = ?')
        .get(move.StockBatchId)

      if (!batch) throw new Error('Fatal Error: Source stock batch not found.')
      if (batch.RemainingQuantity < move.Quantity)
        throw new Error(`Cart out of sync! Only ${batch.RemainingQuantity} left.`)

      const product: any = db
        .prepare('SELECT Quantity FROM Products WHERE Id = ?')
        .get(move.ProductId)
      if (!product) throw new Error('Fatal Error: Product not found.')

      updateBatchStmt.run(batch.RemainingQuantity - move.Quantity, move.StockBatchId)
      updateProductStmt.run(product.Quantity - move.Quantity, move.ProductId)

      insertMovementStmt.run(
        new Date().toISOString(),
        move.ProductId,
        2,
        move.Quantity,
        batch.CostPrice,
        move.UnitPrice,
        move.StockBatchId,
        move.Note || '',
        txn.ReceiptId
      )
    }
  })

  checkoutTransaction(transaction, movements)
}

// ==========================================
// 🔄 2. RETURNS ENGINE (Partial & Full)
// ==========================================
export function processReturn(payload: any) {
  const db = getDb()

  const returnTxn = db.transaction((data) => {
    const { ReceiptId, Items, RefundAmount } = data

    const insertMovementStmt = db.prepare(`
      INSERT INTO StockMovements (Date, ProductId, Type, Quantity, UnitCost, UnitPrice, StockBatchId, Note, ReceiptId, IsVoided)
      VALUES (?, ?, 4, ?, ?, ?, ?, ?, ?, 0)
    `)

    // 🚀 NEW: Save to our dedicated Returns table for the business owner to audit!
    const insertReturnLogStmt = db.prepare(`
      INSERT INTO Returns (ReceiptId, BatchId, Quantity, RefundAmount, Reason)
      VALUES (?, ?, ?, ?, ?)
    `)

    const updateBatchStmt = db.prepare(
      'UPDATE StockBatches SET RemainingQuantity = RemainingQuantity + ? WHERE Id = ?'
    )
    const updateProductStmt = db.prepare('UPDATE Products SET Quantity = Quantity + ? WHERE Id = ?')

    for (const item of Items) {
      const timestamp = new Date().toISOString()
      const reason = item.Note || 'Customer Return'

      // Log movement
      insertMovementStmt.run(
        timestamp,
        item.ProductId,
        item.Quantity,
        item.UnitCost,
        item.UnitPrice,
        item.StockBatchId,
        reason,
        ReceiptId
      )

      // 🚀 NEW: Log dedicated Return record
      insertReturnLogStmt.run(
        ReceiptId,
        item.StockBatchId,
        item.Quantity,
        item.RefundAmount || item.Quantity * item.UnitPrice,
        reason
      )

      updateBatchStmt.run(item.Quantity, item.StockBatchId)
      updateProductStmt.run(item.Quantity, item.ProductId)
    }

    // Financial Adjustment
    const txn: any = db
      .prepare(
        'SELECT TotalAmount, PaidAmount, IsCredit FROM SalesTransactions WHERE ReceiptId = ?'
      )
      .get(ReceiptId)
    if (txn) {
      const newTotal = Math.max(0, txn.TotalAmount - RefundAmount)
      let newPaid = txn.PaidAmount

      if (txn.IsCredit === 0) {
        newPaid = Math.max(0, newPaid - RefundAmount)
      } else {
        if (newPaid > newTotal) newPaid = newTotal
      }

      let status = 0
      if (txn.IsCredit === 1) {
        if (newPaid === 0 && newTotal > 0) status = 1
        else if (newPaid < newTotal) status = 2
      }

      db.prepare(
        'UPDATE SalesTransactions SET TotalAmount = ?, PaidAmount = ?, Status = ? WHERE ReceiptId = ?'
      ).run(newTotal, newPaid, status, ReceiptId)
    }
  })

  returnTxn(payload)
  return { success: true }
}

// ==========================================
// 📦 3. INVENTORY MANAGEMENT (GRN & Adjust)
// ==========================================
export function receiveStock(movement: any) {
  const db = getDb()
  const receiveTxn = db.transaction((mov) => {
    const product: any = db
      .prepare('SELECT SellingPrice, DiscountLimit, Quantity FROM Products WHERE Id = ?')
      .get(mov.ProductId)
    if (!product) throw new Error('Product not found.')

    const newSellingPrice = mov.SellingPrice !== undefined ? mov.SellingPrice : product.SellingPrice
    const newDiscount = mov.Discount !== undefined ? mov.Discount : product.DiscountLimit

    // 🚀 NEW: Grab the DiscountType from the frontend (defaults to percentage)
    const newDiscountType = mov.DiscountType || 'percentage'

    db.prepare(
      'UPDATE Products SET Quantity = Quantity + ?, SellingPrice = ?, DiscountLimit = ? WHERE Id = ?'
    ).run(mov.Quantity, newSellingPrice, newDiscount, mov.ProductId)

    db.prepare(
      `INSERT INTO StockMovements (Date, ProductId, Type, Quantity, UnitCost, UnitPrice, Reason, IsVoided) VALUES (?, ?, 1, ?, ?, ?, 0, 0)`
    ).run(mov.Date, mov.ProductId, mov.Quantity, mov.UnitCost, newSellingPrice)

    const rnd = Math.floor(Math.random() * 9)
    const discountCode = `${rnd}${String(newDiscount).padStart(3, '0')}${rnd}`

    // 🚀 FIXED: Now saving the exact DiscountType to the database!
    db.prepare(
      `
      INSERT INTO StockBatches (ProductId, InitialQuantity, RemainingQuantity, CostPrice, SellingPrice, Discount, DiscountType, DiscountStartDate, DiscountEndDate, DiscountCode, ReceivedDate) 
      VALUES (?, ?, ?, ?, ?, ?, ?, NULL, NULL, ?, ?)
    `
    ).run(
      mov.ProductId,
      mov.Quantity,
      mov.Quantity,
      mov.UnitCost,
      newSellingPrice,
      newDiscount,
      newDiscountType, // Extracted from payload
      discountCode,
      mov.Date
    )
  })
  receiveTxn(movement)
}
export function processGRN(payload: any) {
  const db = getDb()
  const grnTxn = db.transaction((data) => {
    const { SupplierId, ReferenceNo, InvoiceDate, Items } = data

    let totalAmount = 0
    for (const item of Items) totalAmount += item.total

    const insertInvoice = db
      .prepare(
        `INSERT INTO PurchaseInvoices (BillNumber, SupplierId, TotalAmount, Status, Date) VALUES (?, ?, ?, 1, ?)`
      )
      .run(ReferenceNo, SupplierId, totalAmount, InvoiceDate)
    const invoiceId = insertInvoice.lastInsertRowid

    // 🚀 NEW: Included new discount scheduling columns
    const insertBatchStmt = db.prepare(`
      INSERT INTO StockBatches (ProductId, InitialQuantity, RemainingQuantity, CostPrice, SellingPrice, Discount, DiscountType, DiscountCode, PurchaseInvoiceId, ReceivedDate) 
      VALUES (?, ?, ?, ?, ?, ?, 'percentage', ?, ?, ?)
    `)

    const insertMovementStmt = db.prepare(
      `INSERT INTO StockMovements (ProductId, Type, Quantity, UnitCost, UnitPrice, StockBatchId, ReceiptId, Date) VALUES (?, 1, ?, ?, ?, ?, ?, ?)`
    )
    const updateProductStmt = db.prepare(
      `UPDATE Products SET Quantity = Quantity + ?, BuyingPrice = ?, SellingPrice = ?, DiscountLimit = ? WHERE Id = ?`
    )

    for (const item of Items) {
      const rnd = Math.floor(Math.random() * 9)
      const discountCode = `${rnd}${String(item.discountLimit).padStart(3, '0')}${rnd}`

      const batchResult = insertBatchStmt.run(
        item.productId,
        item.qty,
        item.qty,
        item.buyPrice,
        item.sellPrice,
        item.discountLimit,
        discountCode,
        invoiceId,
        InvoiceDate
      )
      const batchId = batchResult.lastInsertRowid

      insertMovementStmt.run(
        item.productId,
        item.qty,
        item.buyPrice,
        item.sellPrice,
        batchId,
        ReferenceNo,
        InvoiceDate
      )
      updateProductStmt.run(
        item.qty,
        item.buyPrice,
        item.sellPrice,
        item.discountLimit,
        item.productId
      )
    }
  })

  grnTxn(payload)
  return { success: true }
}

export function adjustStock(adjustment: any) {
  const db = getDb()
  const adjustTxn = db.transaction((adj) => {
    const batch: any = db
      .prepare('SELECT RemainingQuantity, CostPrice FROM StockBatches WHERE Id = ?')
      .get(adj.StockBatchId)
    if (!batch) throw new Error('Batch not found.')
    if (adj.Quantity > batch.RemainingQuantity)
      throw new Error(`Cannot remove ${adj.Quantity}. Only ${batch.RemainingQuantity} left.`)

    db.prepare(
      'UPDATE StockBatches SET RemainingQuantity = RemainingQuantity - ? WHERE Id = ?'
    ).run(adj.Quantity, adj.StockBatchId)
    db.prepare('UPDATE Products SET Quantity = MAX(0, Quantity - ?) WHERE Id = ?').run(
      adj.Quantity,
      adj.ProductId
    )

    const unitCost = adj.Reason === 0 ? 0 : batch.CostPrice
    db.prepare(
      `INSERT INTO StockMovements (ProductId, Type, Quantity, UnitCost, UnitPrice, StockBatchId, Reason, IsVoided, Note) VALUES (?, 3, ?, ?, 0, ?, ?, 0, ?)`
    ).run(adj.ProductId, adj.Quantity, unitCost, adj.StockBatchId, adj.Reason, adj.Note)
  })
  adjustTxn(adjustment)
}

export function voidReceipt(receiptId: string) {
  const db = getDb()
  const voidTxn = db.transaction((rId) => {
    const txn: any = db
      .prepare(
        'SELECT TransactionDate, Status, IsCredit, PaidAmount FROM SalesTransactions WHERE ReceiptId = ?'
      )
      .get(rId)

    if (!txn) throw new Error('Receipt not found.')
    if (txn.Status === 3) throw new Error('This receipt is already voided.')

    const today = new Date().toISOString().split('T')[0]
    if (!txn.TransactionDate.startsWith(today)) {
      throw new Error(
        'You can only void sales from today. For older sales, please use the Returns page.'
      )
    }
    if (txn.IsCredit === 1 && txn.PaidAmount > 0) {
      throw new Error(
        'Cannot void a credit sale with partial payments. Please use the Returns page.'
      )
    }

    const hasReturns: any = db
      .prepare('SELECT COUNT(*) as count FROM StockMovements WHERE ReceiptId = ? AND Type = 4')
      .get(rId)
    if (hasReturns && hasReturns.count > 0) {
      throw new Error('Action Blocked: This bill already has returned items.')
    }

    db.prepare('UPDATE SalesTransactions SET Status = 3 WHERE ReceiptId = ?').run(rId)

    const movements: any[] = db
      .prepare('SELECT * FROM StockMovements WHERE ReceiptId = ? AND IsVoided = 0 AND Type = 2')
      .all(rId)
    const updateMoveStmt = db.prepare(
      "UPDATE StockMovements SET IsVoided = 1, Note = IFNULL(Note, '') || ' [VOIDED]' WHERE Id = ?"
    )
    const updateProdStmt = db.prepare('UPDATE Products SET Quantity = Quantity + ? WHERE Id = ?')
    const updateBatchStmt = db.prepare(
      'UPDATE StockBatches SET RemainingQuantity = RemainingQuantity + ? WHERE Id = ?'
    )

    for (const move of movements) {
      updateMoveStmt.run(move.Id)
      updateProdStmt.run(move.Quantity, move.ProductId)
      if (move.StockBatchId) updateBatchStmt.run(move.Quantity, move.StockBatchId)
    }
  })

  voidTxn(receiptId)
  return { success: true }
}

// DATA RETRIEVAL FUNCTIONS
export function getActiveBatches() {
  return getDb()
    .prepare(
      `SELECT b.*, p.Name as ProductName, p.Barcode FROM StockBatches b JOIN Products p ON b.ProductId = p.Id WHERE b.RemainingQuantity > 0`
    )
    .all()
}
export function getLowStockProducts(threshold: number) {
  return getDb()
    .prepare(
      `SELECT p.*, c.Name as CategoryName FROM Products p JOIN Categories c ON p.CategoryId = c.Id WHERE p.Quantity <= ? ORDER BY p.Quantity ASC`
    )
    .all(threshold)
}
export function getSupplierInvoices(supplierId: number) {
  return getDb()
    .prepare('SELECT * FROM PurchaseInvoices WHERE SupplierId = ? ORDER BY Date DESC')
    .all(supplierId)
}
export function getInvoiceItems(invoiceId: number) {
  return getDb()
    .prepare(
      `SELECT b.*, p.Name as ProductName, p.Barcode, p.Unit FROM StockBatches b JOIN Products p ON b.ProductId = p.Id WHERE b.PurchaseInvoiceId = ?`
    )
    .all(invoiceId)
}
export function getProductBatches(productId: number) {
  return getDb()
    .prepare(
      `SELECT b.*, s.Name as SupplierName FROM StockBatches b LEFT JOIN PurchaseInvoices inv ON b.PurchaseInvoiceId = inv.Id LEFT JOIN Suppliers s ON inv.SupplierId = s.Id WHERE b.ProductId = ? ORDER BY b.ReceivedDate DESC`
    )
    .all(productId)
}
export function getProductAdjustments(productId: number) {
  return getDb()
    .prepare(`SELECT * FROM StockMovements WHERE ProductId = ? AND Type = 3 ORDER BY Date DESC`)
    .all(productId)
}

// 🚀 NEW: Update Pricing for a Specific Batch
export function updateBatchPricing(payload: {
  BatchId: number
  SellingPrice: number
  Discount: number
  DiscountType: string
}) {
  const db = getDb()
  try {
    db.prepare(
      `
      UPDATE StockBatches 
      SET SellingPrice = ?, Discount = ?, DiscountType = ? 
      WHERE Id = ?
    `
    ).run(
      payload.SellingPrice,
      Math.max(0, payload.Discount),
      payload.DiscountType,
      payload.BatchId
    )

    return { success: true }
  } catch (error: any) {
    console.error('Failed to update batch pricing:', error)
    throw new Error('Database Error: ' + error.message)
  }
}
