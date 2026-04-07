// src/preload/index.ts
import { contextBridge, ipcRenderer } from 'electron'
import { electronAPI } from '@electron-toolkit/preload'

// ==========================================
// THE BRIDGE (Custom APIs for React Renderer)
// ==========================================
const api = {
  // --- USERS ---
  getUserByUsername: (username: string) => ipcRenderer.invoke('get-user-by-username', username),
  getUsers: () => ipcRenderer.invoke('get-users'),
  addUser: (user: any) => ipcRenderer.invoke('add-user', user),
  updateUser: (user: any) => ipcRenderer.invoke('update-user', user),
  deleteUser: (id: number) => ipcRenderer.invoke('delete-user', id),

  // --- CATEGORIES ---
  getCategories: () => ipcRenderer.invoke('get-categories'),
  addCategory: (cat: any) => ipcRenderer.invoke('add-category', cat),
  updateCategory: (cat: any) => ipcRenderer.invoke('update-category', cat),
  deleteCategory: (id: number) => ipcRenderer.invoke('delete-category', id),

  // --- SUPPLIERS ---
  getSuppliers: () => ipcRenderer.invoke('get-suppliers'),
  addSupplier: (sup: any) => ipcRenderer.invoke('add-supplier', sup),
  updateSupplier: (sup: any) => ipcRenderer.invoke('update-supplier', sup),
  deleteSupplier: (id: number) => ipcRenderer.invoke('delete-supplier', id),

  // --- PRODUCTS ---
  getProducts: () => ipcRenderer.invoke('get-products'),
  addProduct: (prod: any) => ipcRenderer.invoke('add-product', prod),
  updateProduct: (prod: any) => ipcRenderer.invoke('update-product', prod),
  deleteProduct: (id: number) => ipcRenderer.invoke('delete-product', id),

  // --- STOCK & CHECKOUT ENGINE ---
  processSale: (txn: any, movs: any[]) => ipcRenderer.invoke('process-sale', txn, movs),
  receiveStock: (mov: any) => ipcRenderer.invoke('receive-stock', mov),
  updateBatchPricing: (payload: any) => ipcRenderer.invoke('update-batch-pricing', payload),
  adjustStock: (adj: any) => ipcRenderer.invoke('adjust-stock', adj),
  getActiveBatches: () => ipcRenderer.invoke('get-active-batches'),
  getLowStockProducts: (threshold: number) => ipcRenderer.invoke('get-low-stock', threshold),
  getProductAdjustments: (productId: number) =>
    ipcRenderer.invoke('get-product-adjustments', productId),

  // 🚀 VOIDS & RETURNS ENGINE
  voidReceipt: (id: string) => ipcRenderer.invoke('void-receipt', id),
  processReturn: (payload: any) => ipcRenderer.invoke('process-return', payload),
  getBillForReturn: (receiptId: string) => ipcRenderer.invoke('get-bill-for-return', receiptId),

  // 🚀 GRN & BATCH QUERIES
  processGRN: (payload: any) => ipcRenderer.invoke('process-grn', payload),
  getSupplierInvoices: (supplierId: number) =>
    ipcRenderer.invoke('get-supplier-invoices', supplierId),
  getInvoiceItems: (invoiceId: number) => ipcRenderer.invoke('get-invoice-items', invoiceId),
  getProductBatches: (productId: number) => ipcRenderer.invoke('get-product-batches', productId),

  // --- REPORTS & DASHBOARD ---
  getDashboardMetrics: () => ipcRenderer.invoke('get-dashboard-metrics'),
  getChartData: (filter: string) => ipcRenderer.invoke('get-chart-data', filter),
  // Add this inside your window.api object in preload.ts:
  getDashboardData: (startDate: string, endDate: string) =>
    ipcRenderer.invoke('get-dashboard-data', startDate, endDate),
  getTopSellers: () => ipcRenderer.invoke('get-top-sellers'),
  getLowStockAlerts: () => ipcRenderer.invoke('get-dashboard-low-stock'),
  getAuditLogs: (startDate: string, endDate: string) =>
    ipcRenderer.invoke('get-audit-logs', startDate, endDate),

  // Sales Ledger Queries
  getTodaySales: () => ipcRenderer.invoke('get-today-sales'),
  getReceiptItems: (receiptId: string) => ipcRenderer.invoke('get-receipt-items', receiptId),
  getSalesHistory: (startDate: string, endDate: string, search: string) =>
    ipcRenderer.invoke('getSalesHistory', startDate, endDate, search),
  getReceiptDetails: (receiptId: string) => ipcRenderer.invoke('getReceiptDetails', receiptId),

  getPendingCreditAccounts: () => ipcRenderer.invoke('get-pending-credit'),
  processCreditPayment: (receiptId: string, amount: number) =>
    ipcRenderer.invoke('process-credit-payment', receiptId, amount),
  // Find your stockRepo area and add:
  processCompleteSale: (transaction: any, movements: any[]) =>
    ipcRenderer.invoke('process-complete-sale', transaction, movements),

  // --- SYSTEM & POS ---
  getPrinters: () => ipcRenderer.invoke('get-printers'),
  exportDatabase: () => ipcRenderer.invoke('export-database'),
  importDatabase: () => ipcRenderer.invoke('import-database'),
  factoryReset: () => ipcRenderer.invoke('factory-reset'),

  getSettings: () => ipcRenderer.invoke('get-settings'),
  updateSettings: (settings) => ipcRenderer.invoke('update-settings', settings)
}

// ==========================================
// SECURE EXPOSURE LOGIC
// ==========================================
if (process.contextIsolated) {
  try {
    contextBridge.exposeInMainWorld('electron', electronAPI)
    contextBridge.exposeInMainWorld('api', api)
  } catch (error) {
    console.error(error)
  }
} else {
  // @ts-ignore
  window.electron = electronAPI
  // @ts-ignore
  window.api = api
}
