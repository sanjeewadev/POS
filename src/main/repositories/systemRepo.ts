// src/main/repositories/systemRepo.ts
import { app } from 'electron'
import * as fs from 'fs'
import * as path from 'path'
import { getDb } from '../database'

// 🚀 FIXED: Points to the exact path we set in database.ts
const getDbPath = () => {
  return path.join(app.getPath('appData'), 'HasithaInventory', 'inventory.db')
}

// ==========================================
// 🏪 0. SHOP SETTINGS MANAGEMENT
// ==========================================
export function getSettings() {
  return getDb().prepare('SELECT * FROM ShopSettings WHERE Id = 1').get()
}

export function updateSettings(settingsData: any) {
  const { ShopName, ShopAddress, ShopPhone, ReceiptFooter } = settingsData
  return getDb()
    .prepare(
      `
    UPDATE ShopSettings
    SET ShopName = ?, ShopAddress = ?, ShopPhone = ?, ReceiptFooter = ?
    WHERE Id = 1
  `
    )
    .run(
      ShopName?.trim() || 'My Shop',
      ShopAddress?.trim() || '',
      ShopPhone?.trim() || '',
      ReceiptFooter?.trim() || 'Thank you!'
    )
}

// ==========================================
// 💾 1. BACKUP DATABASE
// ==========================================
export async function exportDatabase(destinationPath: string) {
  try {
    const sourcePath = getDbPath()
    fs.copyFileSync(sourcePath, destinationPath)
    return { success: true }
  } catch (error: any) {
    throw new Error(`Failed to backup database: ${error.message}`)
  }
}

// ==========================================
// 🔄 2. RESTORE DATABASE
// ==========================================
export async function importDatabase(sourcePath: string) {
  try {
    const liveDbPath = getDbPath()
    const db = getDb()

    db.close()
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
      db.prepare('DELETE FROM StockMovements').run()
      db.prepare('DELETE FROM SalesTransactions').run()
      db.prepare('DELETE FROM Returns').run() // 🚀 NEW: Clear Returns table
      db.prepare('DELETE FROM Products').run()
      db.prepare('DELETE FROM Categories').run()
      db.prepare('DELETE FROM Suppliers').run()

      db.prepare(
        'DELETE FROM sqlite_sequence WHERE name IN ("StockMovements", "SalesTransactions", "Returns", "Products", "Categories", "Suppliers")'
      ).run()

      // NOTE: We intentionally DO NOT delete from Users or ShopSettings!
    })

    reset()
    return { success: true }
  } catch (error: any) {
    throw new Error(`Factory reset failed: ${error.message}`)
  }
}
