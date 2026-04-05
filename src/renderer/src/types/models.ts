// src/renderer/src/types/models.ts

export interface User {
  Id: number
  Username: string
  PasswordHash: string
  FullName: string
  Role: number
  IsActive: boolean | number
  Permissions: string
}

export interface Category {
  Id: number
  Name: string
  ParentId: number | null
}

export interface Product {
  Id: number
  Name: string
  PrintName?: string
  Barcode: string
  Description?: string
  Unit: string
  QuantityType?: string
  CategoryId: number
  BuyingPrice: number
  SellingPrice: number
  DiscountLimit: number
  Quantity: number
  IsActive: number
  CategoryName?: string
}

export interface Supplier {
  Id: number
  Name: string
  Phone: string
  Note: string
}

export interface StockBatch {
  Id: number
  ProductId: number
  ProductName?: string
  InitialQuantity: number
  RemainingQuantity: number
  CostPrice: number
  SellingPrice: number
  Discount: number
  DiscountType?: string // 🚀 NEW
  DiscountStartDate?: string | null // 🚀 NEW
  DiscountEndDate?: string | null // 🚀 NEW
  DiscountCode: string
  ReceivedDate: string
}

export interface SalesTransaction {
  ReceiptId: string
  TransactionDate: string
  TotalAmount: number
  PaidAmount: number
  CashReceived?: number // 🚀 NEW: Exact cash handed over
  ChangeGiven?: number // 🚀 NEW: Exact change returned
  IsCredit: boolean | number
  CustomerName: string
  Status: number
}
