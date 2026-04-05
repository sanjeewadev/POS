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
  const db = getDb()

  // Clean up user inputs for the new retail features
  const safePrintName = product.PrintName?.trim() || null
  const validQtyType = product.QuantityType === 'kg' ? 'kg' : 'quantity'

  // Safely handle empty barcodes so we don't trigger the UNIQUE constraint error
  if (product.Barcode && product.Barcode.trim() !== '') {
    return db
      .prepare(
        `
      INSERT INTO Products (
        Name, PrintName, Barcode, Description, Unit, QuantityType, 
        CategoryId, BuyingPrice, SellingPrice, DiscountLimit, Quantity, IsActive
      )
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
    `
      )
      .run(
        product.Name,
        safePrintName,
        product.Barcode.trim(),
        product.Description,
        product.Unit,
        validQtyType,
        product.CategoryId,
        product.BuyingPrice || 0,
        product.SellingPrice || 0,
        product.DiscountLimit || 0,
        product.Quantity || 0
      )
  } else {
    // Insert without barcode
    return db
      .prepare(
        `
      INSERT INTO Products (
        Name, PrintName, Description, Unit, QuantityType, 
        CategoryId, BuyingPrice, SellingPrice, DiscountLimit, Quantity, IsActive
      )
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
    `
      )
      .run(
        product.Name,
        safePrintName,
        product.Description,
        product.Unit,
        validQtyType,
        product.CategoryId,
        product.BuyingPrice || 0,
        product.SellingPrice || 0,
        product.DiscountLimit || 0,
        product.Quantity || 0
      )
  }
}

export function updateProduct(product: any) {
  const validQtyType = product.QuantityType === 'kg' ? 'kg' : 'quantity'

  return getDb()
    .prepare(
      `
    UPDATE Products 
    SET Name = ?, PrintName = ?, Barcode = ?, Description = ?, Unit = ?, 
        QuantityType = ?, CategoryId = ?, BuyingPrice = ?, SellingPrice = ?, DiscountLimit = ?
    WHERE Id = ?
  `
    )
    .run(
      product.Name,
      product.PrintName?.trim() || null,
      product.Barcode?.trim() || null,
      product.Description,
      product.Unit,
      validQtyType,
      product.CategoryId,
      product.BuyingPrice || 0,
      product.SellingPrice || 0,
      product.DiscountLimit || 0,
      product.Id
    )
}

// 🚀 NEW: Move product to the "Recycle Bin"
export function softDeleteProduct(id: number) {
  return getDb().prepare('UPDATE Products SET IsActive = 0 WHERE Id = ?').run(id)
}

// 🚀 NEW: Restore a product from the "Recycle Bin"
export function restoreProduct(id: number) {
  return getDb().prepare('UPDATE Products SET IsActive = 1 WHERE Id = ?').run(id)
}
