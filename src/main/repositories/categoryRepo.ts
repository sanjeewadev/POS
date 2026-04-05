// src/main/repositories/categoryRepo.ts
import { getDb } from '../database'

export function getAllCategories() {
  // Update: Added ORDER BY to keep your file explorer alphabetized!
  return getDb().prepare('SELECT * FROM Categories ORDER BY Name ASC').all()
}

export function addCategory(category: any) {
  return getDb()
    .prepare('INSERT INTO Categories (Name, ParentId) VALUES (?, ?)')
    .run(category.Name, category.ParentId || null)
}

export function updateCategory(category: any) {
  return getDb()
    .prepare('UPDATE Categories SET Name = ?, ParentId = ? WHERE Id = ?')
    .run(category.Name, category.ParentId || null, category.Id)
}

export function deleteCategory(id: number) {
  return getDb().prepare('DELETE FROM Categories WHERE Id = ?').run(id)
}
