// src/main/handlers/userHandlers.ts
import { ipcMain, dialog, BrowserWindow, app } from 'electron'
import * as userRepo from '../repositories/userRepo'
import * as systemRepo from '../repositories/systemRepo'

export function registerUserHandlers() {
  // User Management
  ipcMain.handle('get-user-by-username', (_, username) => userRepo.getUserByUsername(username))
  ipcMain.handle('get-users', () => userRepo.getAllUsers())
  ipcMain.handle('add-user', (_, user) => userRepo.addUser(user))
  ipcMain.handle('update-user', (_, user) => userRepo.updateUser(user))
  ipcMain.handle('delete-user', (_, id) => userRepo.deleteUser(id))

  // System, POS & Backups
  ipcMain.handle('get-printers', async (event) => {
    return await event.sender.getPrintersAsync()
  })

  // 🚀 THE FIX: Registering the Shop Settings IPC Handlers
  ipcMain.handle('get-settings', () => systemRepo.getSettings())
  ipcMain.handle('update-settings', (_, settings) => systemRepo.updateSettings(settings))

  ipcMain.handle('export-database', async (event) => {
    const window = BrowserWindow.fromWebContents(event.sender)
    const { canceled, filePath } = await dialog.showSaveDialog(window!, {
      title: 'Save Database Backup',
      defaultPath: `POS_Backup_${new Date().toISOString().split('T')[0]}.sqlite`,
      filters: [{ name: 'SQLite Database', extensions: ['sqlite', 'db'] }]
    })

    if (canceled || !filePath) return { success: false, canceled: true }
    return await systemRepo.exportDatabase(filePath)
  })

  ipcMain.handle('import-database', async (event) => {
    const window = BrowserWindow.fromWebContents(event.sender)
    const { canceled, filePaths } = await dialog.showOpenDialog(window!, {
      title: 'Select Database Backup to Restore',
      properties: ['openFile'],
      filters: [{ name: 'SQLite Database', extensions: ['sqlite', 'db'] }]
    })

    if (canceled || filePaths.length === 0) return { success: false, canceled: true }

    const result = await systemRepo.importDatabase(filePaths[0])
    if (result.success) {
      app.relaunch()
      app.quit()
    }
    return result
  })

  ipcMain.handle('factory-reset', () => {
    const result = systemRepo.factoryReset()
    if (result.success) {
      app.relaunch()
      app.quit()
    }
    return result
  })
}
