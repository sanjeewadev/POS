// src/main/repositories/systemRepo.ts
import { app } from 'electron'
import * as fs from 'fs'
import * as path from 'path'
import { getDb } from '../database'

const getDbPath = () => {
  return path.join(app.getPath('appData'), 'HasithaInventory', 'inventory.db')
}

// ==========================================
// 🏪 0. SHOP SETTINGS MANAGEMENT
// ==========================================
export function getSettings() {
  try {
    const db = getDb()
    return db.prepare('SELECT * FROM ShopSettings WHERE Id = 1').get()
  } catch (error) {
    console.error('Failed to get shop settings:', error)
    return null
  }
}

export function updateSettings(settingsData: any) {
  const { ShopName, ShopAddress, ShopPhone, ReceiptFooter } = settingsData
  try {
    const db = getDb()
    db.prepare(
      `
      UPDATE ShopSettings
      SET ShopName = ?, ShopAddress = ?, ShopPhone = ?, ReceiptFooter = ?
      WHERE Id = 1
    `
    ).run(
      ShopName?.trim() || 'My Shop',
      ShopAddress?.trim() || '',
      ShopPhone?.trim() || '',
      ReceiptFooter?.trim() || 'Thank you!'
    )
    return { success: true }
  } catch (error: any) {
    throw new Error(`Failed to update shop settings: ${error.message}`)
  }
}

// ==========================================
// 💾 1. BACKUP DATABASE (Safe for WAL mode)
// ==========================================
export async function exportDatabase(destinationPath: string) {
  try {
    const db = getDb()
    // 🚀 THE FIX: Using better-sqlite3's built-in backup.
    // This safely checkpoints the WAL memory before copying!
    await db.backup(destinationPath)
    return { success: true }
  } catch (error: any) {
    throw new Error(`Failed to backup database: ${error.message}`)
  }
}

// ==========================================
// 🔄 2. RESTORE DATABASE (Safe cleanup)
// ==========================================
export async function importDatabase(sourcePath: string) {
  try {
    const liveDbPath = getDbPath()
    const db = getDb()

    // 1. Close the active connection
    db.close()

    // 2. 🚀 THE FIX: Delete temporary WAL and SHM memory files if they exist
    // If we don't delete these, they will corrupt the incoming older database!
    if (fs.existsSync(liveDbPath + '-wal')) fs.unlinkSync(liveDbPath + '-wal')
    if (fs.existsSync(liveDbPath + '-shm')) fs.unlinkSync(liveDbPath + '-shm')

    // 3. Overwrite the database
    fs.copyFileSync(sourcePath, liveDbPath)

    return { success: true }
  } catch (error: any) {
    throw new Error(`Failed to restore database: ${error.message}`)
  }
}

// ==========================================
// 🚨 3. FACTORY RESET (DANGER ZONE)
// ==========================================
export function factoryReset() {
  const db = getDb()

  try {
    const reset = db.transaction(() => {
      // 🚀 THE FIX: Included ALL enterprise tables
      db.prepare('DELETE FROM Returns').run()
      db.prepare('DELETE FROM StockMovements').run()
      db.prepare('DELETE FROM StockBatches').run()
      db.prepare('DELETE FROM CreditPaymentLogs').run()
      db.prepare('DELETE FROM SalesTransactions').run()
      db.prepare('DELETE FROM PurchaseInvoices').run()
      db.prepare('DELETE FROM Suppliers').run()
      db.prepare('DELETE FROM Products').run()
      db.prepare('DELETE FROM Categories').run()

      db.prepare(
        `
        DELETE FROM sqlite_sequence WHERE name IN (
          "Returns", "StockMovements", "StockBatches", "CreditPaymentLogs", 
          "SalesTransactions", "PurchaseInvoices", "Suppliers", "Products", "Categories"
        )
      `
      ).run()

      // NOTE: We intentionally DO NOT delete from Users or ShopSettings!
    })

    reset()
    return { success: true }
  } catch (error: any) {
    throw new Error(`Factory reset failed: ${error.message}`)
  }
}
