import { getDb } from '../database'

export function getUserByUsername(username: string) {
  return getDb().prepare('SELECT * FROM Users WHERE Username = ?').get(username)
}

export function getAllUsers() {
  // SECURITY: Hide SuperAdmin (Role = 0) from the UI
  return getDb().prepare('SELECT * FROM Users WHERE Role != 0').all()
}

export function addUser(user: any) {
  const stmt = getDb().prepare(`
    INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, Permissions)
    VALUES (?, ?, ?, ?, ?, ?)
  `)
  return stmt.run(
    user.Username,
    user.PasswordHash,
    user.FullName,
    user.Role,
    user.IsActive ? 1 : 0,
    user.Permissions
  )
}

export function updateUser(user: any) {
  const stmt = getDb().prepare(`
    UPDATE Users 
    SET Username = ?, PasswordHash = ?, FullName = ?, Role = ?, IsActive = ?, Permissions = ? 
    WHERE Id = ?
  `)
  return stmt.run(
    user.Username,
    user.PasswordHash,
    user.FullName,
    user.Role,
    user.IsActive ? 1 : 0,
    user.Permissions,
    user.Id
  )
}

export function deleteUser(id: number) {
  return getDb().prepare('DELETE FROM Users WHERE Id = ?').run(id)
}
