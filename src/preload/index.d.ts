import { ElectronAPI } from '@electron-toolkit/preload'

declare global {
  interface Window {
    electron: ElectronAPI
    // We are telling React exactly what our bridge looks like!
    api: {
      // Users
      getUserByUsername: (username: string) => Promise<any>
      getUsers: () => Promise<any[]>
      addUser: (user: any) => Promise<void>
      updateUser: (user: any) => Promise<void>
      deleteUser: (id: number) => Promise<void>

      // Categories
      getCategories: () => Promise<any[]>
      addCategory: (cat: any) => Promise<void>
      updateCategory: (cat: any) => Promise<void>
      deleteCategory: (id: number) => Promise<void>

      // Suppliers
      getSuppliers: () => Promise<any[]>
      addSupplier: (sup: any) => Promise<void>
      updateSupplier: (sup: any) => Promise<void>
      deleteSupplier: (id: number) => Promise<void>

      // Products
      getProducts: () => Promise<any[]>
      addProduct: (prod: any) => Promise<void>
      updateProduct: (prod: any) => Promise<void>
      deleteProduct: (id: number) => Promise<void>

      // Stock
      processSale: (txn: any, movs: any[]) => Promise<void>
      receiveStock: (mov: any) => Promise<void>
      adjustStock: (adj: any) => Promise<void>
      getActiveBatches: () => Promise<any[]>
      getLowStock: (threshold: number) => Promise<any[]>
      voidReceipt: (id: string) => Promise<void>
    }
  }
}
