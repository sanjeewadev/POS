// src/main/handlers/salesHandlers.ts
import { ipcMain } from 'electron'
import * as stockRepo from '../repositories/stockRepo'
import * as reportRepo from '../repositories/reportRepo'

export function registerSalesHandlers() {
  // Stock, Checkout & Adjustments
  ipcMain.handle('process-sale', (_, txn, movs) => stockRepo.processCompleteSale(txn, movs))
  ipcMain.handle('process-complete-sale', (_, txn, movs) =>
    stockRepo.processCompleteSale(txn, movs)
  ) // Keep for compatibility
  ipcMain.handle('receive-stock', (_, mov) => stockRepo.receiveStock(mov))
  ipcMain.handle('adjust-stock', (_, adj) => stockRepo.adjustStock(adj))
  ipcMain.handle('get-active-batches', () => stockRepo.getActiveBatches())
  ipcMain.handle('get-low-stock', (_, threshold) => stockRepo.getLowStockProducts(threshold))
  ipcMain.handle('get-product-adjustments', (_, productId) =>
    stockRepo.getProductAdjustments(productId)
  )

  // Voids & Returns Engine
  ipcMain.handle('void-receipt', (_, id) => stockRepo.voidReceipt(id))
  ipcMain.handle('process-return', (_, payload) => stockRepo.processReturn(payload))
  ipcMain.handle('get-bill-for-return', (_, receiptId) => reportRepo.getBillForReturn(receiptId))

  // GRN Engine
  ipcMain.handle('process-grn', (_, payload) => stockRepo.processGRN(payload))
  ipcMain.handle('get-supplier-invoices', (_, supplierId) =>
    stockRepo.getSupplierInvoices(supplierId)
  )
  ipcMain.handle('get-invoice-items', (_, invoiceId) => stockRepo.getInvoiceItems(invoiceId))
  ipcMain.handle('get-product-batches', (_, productId) => stockRepo.getProductBatches(productId))

  // Reports & Dashboards
  ipcMain.handle('get-dashboard-metrics', () => reportRepo.getDashboardMetrics())
  ipcMain.handle('get-chart-data', (_, filter) => reportRepo.getChartData(filter))
  ipcMain.handle('get-top-sellers', () => reportRepo.getTopSellers(5))
  ipcMain.handle('get-dashboard-low-stock', () => reportRepo.getLowStockAlerts(5))
  ipcMain.handle('get-dashboard-data', async (_, startDate, endDate) =>
    reportRepo.getDashboardDataFromDB(startDate, endDate)
  )

  // Sales Ledger Queries
  ipcMain.handle('get-today-sales', () => reportRepo.getTodaySales())
  ipcMain.handle('get-receipt-items', (_, receiptId) => reportRepo.getReceiptItems(receiptId))
  ipcMain.handle('getSalesHistory', (_, startDate, endDate, search) =>
    reportRepo.getSalesHistory(startDate, endDate, search)
  )
  ipcMain.handle('getReceiptDetails', (_, receiptId) => reportRepo.getReceiptDetails(receiptId))

  // Credit Accounts
  ipcMain.handle('get-pending-credit', () => reportRepo.getPendingCreditAccounts())
  ipcMain.handle('process-credit-payment', (_, receiptId, amount) =>
    reportRepo.processCreditPayment(receiptId, amount)
  )
  ipcMain.handle('get-audit-logs', (_, startDate, endDate) =>
    reportRepo.getAuditLogs(startDate, endDate)
  )
}
