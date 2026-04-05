// src/main/handlers/inventoryHandlers.ts
import { ipcMain } from 'electron'
import * as catRepo from '../repositories/categoryRepo'
import * as supRepo from '../repositories/supplierRepo'
import * as prodRepo from '../repositories/productRepo'

export function registerInventoryHandlers() {
  // Categories
  ipcMain.handle('get-categories', () => catRepo.getAllCategories())
  ipcMain.handle('add-category', (_, cat) => catRepo.addCategory(cat))
  ipcMain.handle('update-category', (_, cat) => catRepo.updateCategory(cat))
  ipcMain.handle('delete-category', (_, id) => catRepo.deleteCategory(id))

  // Suppliers
  ipcMain.handle('get-suppliers', () => supRepo.getAllSuppliers())
  ipcMain.handle('add-supplier', (_, sup) => supRepo.addSupplier(sup))
  ipcMain.handle('update-supplier', (_, sup) => supRepo.updateSupplier(sup))
  ipcMain.handle('delete-supplier', (_, id) => supRepo.deleteSupplier(id))

  // Products
  ipcMain.handle('get-products', () => prodRepo.getAllProducts())
  ipcMain.handle('add-product', (_, prod) => prodRepo.addProduct(prod))
  ipcMain.handle('update-product', (_, prod) => prodRepo.updateProduct(prod))
  ipcMain.handle('delete-product', (_, id) => prodRepo.deleteProduct(id))
}
