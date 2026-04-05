// src/main/repositories/supplierRepo.ts
import { getDb } from '../database'

export function getAllSuppliers() {
  return getDb().prepare('SELECT * FROM Suppliers ORDER BY Name ASC').all()
}

export function addSupplier(supplier: any) {
  // Update: Removed 'Note' so the database doesn't crash from 'undefined'
  return getDb()
    .prepare('INSERT INTO Suppliers (Name, Phone) VALUES (?, ?)')
    .run(supplier.Name, supplier.Phone || null)
}

export function updateSupplier(supplier: any) {
  // Update: Removed 'Note' here as well
  return getDb()
    .prepare('UPDATE Suppliers SET Name = ?, Phone = ? WHERE Id = ?')
    .run(supplier.Name, supplier.Phone || null, supplier.Id)
}

export function deleteSupplier(id: number) {
  return getDb().prepare('DELETE FROM Suppliers WHERE Id = ?').run(id)
}
