// src/main/repositories/productRepo.ts
import { getDb } from '../database'

// 🚀 For the POS Screen: Only gets active products
export function getProducts() {
  return getDb()
    .prepare(
      `
    SELECT p.*, c.Name as CategoryName 
    FROM Products p
    LEFT JOIN Categories c ON p.CategoryId = c.Id
    WHERE p.IsActive = 1
    ORDER BY p.Name ASC
  `
    )
    .all()
}

// 🚀 For the Admin Screen: Gets everything (including soft-deleted items)
export function getAllProducts() {
  return getDb()
    .prepare(
      `
    SELECT p.*, c.Name as CategoryName 
    FROM Products p
    LEFT JOIN Categories c ON p.CategoryId = c.Id
    ORDER BY p.IsActive DESC, p.Name ASC
  `
    )
    .all()
}

export function addProduct(product: any) {
  try {
    const db = getDb()

    // 1. Sanitize and prepare the payload
    const payload = {
      Name: product.Name,
      PrintName: product.PrintName?.trim() || null,
      Barcode: product.Barcode?.trim() || null,
      Description: product.Description || '',
      Unit: product.Unit || 'Pcs',
      QuantityType: product.QuantityType === 'kg' ? 'kg' : 'quantity',
      CategoryId: product.CategoryId,
      BuyingPrice: product.BuyingPrice || 0,
      SellingPrice: product.SellingPrice || 0,
      DiscountLimit: product.DiscountLimit || 0,
      Quantity: product.Quantity || 0,
      IsActive: 1
    }

    // 2. Use bulletproof Named Parameters (@FieldName)
    const stmt = db.prepare(`
      INSERT INTO Products (
        Name, PrintName, Barcode, Description, Unit, QuantityType, 
        CategoryId, BuyingPrice, SellingPrice, DiscountLimit, Quantity, IsActive
      ) VALUES (
        @Name, @PrintName, @Barcode, @Description, @Unit, @QuantityType, 
        @CategoryId, @BuyingPrice, @SellingPrice, @DiscountLimit, @Quantity, @IsActive
      )
    `)

    const info = stmt.run(payload)
    return { success: true, id: info.lastInsertRowid }
  } catch (error: any) {
    console.error('Failed to add product:', error)
    throw error // Sends error back to React UI
  }
}

export function updateProduct(product: any) {
  try {
    const db = getDb()

    // 1. Sanitize and prepare the payload
    const payload = {
      Id: product.Id,
      Name: product.Name,
      PrintName: product.PrintName?.trim() || null,
      Barcode: product.Barcode?.trim() || null,
      Description: product.Description || '',
      Unit: product.Unit || 'Pcs',
      QuantityType: product.QuantityType === 'kg' ? 'kg' : 'quantity',
      CategoryId: product.CategoryId,
      BuyingPrice: product.BuyingPrice || 0,
      SellingPrice: product.SellingPrice || 0,
      DiscountLimit: product.DiscountLimit || 0
    }

    // 2. Use bulletproof Named Parameters
    const stmt = db.prepare(`
      UPDATE Products SET
        Name = @Name,
        PrintName = @PrintName,
        Barcode = @Barcode,
        Description = @Description,
        Unit = @Unit,
        QuantityType = @QuantityType,
        CategoryId = @CategoryId,
        BuyingPrice = @BuyingPrice,
        SellingPrice = @SellingPrice,
        DiscountLimit = @DiscountLimit
      WHERE Id = @Id
    `)

    stmt.run(payload)
    return { success: true }
  } catch (error: any) {
    console.error('Failed to update product:', error)
    throw error
  }
}

// 🚀 Move product to the "Recycle Bin" (Soft Delete)
export function softDeleteProduct(id: number) {
  try {
    const db = getDb()
    db.prepare('UPDATE Products SET IsActive = 0 WHERE Id = ?').run(id)
    return { success: true }
  } catch (error: any) {
    console.error('Failed to soft delete product:', error)
    throw error
  }
}

// 🚀 Restore a product from the "Recycle Bin"
export function restoreProduct(id: number) {
  try {
    const db = getDb()
    db.prepare('UPDATE Products SET IsActive = 1 WHERE Id = ?').run(id)
    return { success: true }
  } catch (error: any) {
    console.error('Failed to restore product:', error)
    throw error
  }
}
