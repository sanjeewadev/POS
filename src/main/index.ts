// src/main/index.ts
import { app, shell, BrowserWindow } from 'electron'
import { join } from 'path'
import { electronApp, optimizer, is } from '@electron-toolkit/utils'
import icon from '../../resources/icon.png?asset'
import { initDatabase } from './database'

// IMPORT OUR NEW REFACTORED HANDLERS
import { registerInventoryHandlers } from './handlers/inventoryHandlers'
import { registerSalesHandlers } from './handlers/salesHandlers'
import { registerUserHandlers } from './handlers/userHandlers'

app.disableHardwareAcceleration()
app.commandLine.appendSwitch('disable-backgrounding-occluded-windows', 'true')
app.commandLine.appendSwitch('disable-renderer-backgrounding', 'true')

const gotTheLock = app.requestSingleInstanceLock()
if (!gotTheLock) {
  app.quit()
}

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    width: 1366,
    height: 768,
    show: false,
    autoHideMenuBar: true,
    titleBarStyle: 'hidden',
    frame: true,
    ...(process.platform === 'linux' ? { icon } : {}),
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      sandbox: false,
      spellcheck: false
    }
  })

  mainWindow.once('ready-to-show', () => {
    mainWindow.maximize()
    mainWindow.show()
    mainWindow.setAlwaysOnTop(true)
    mainWindow.focus()
    mainWindow.focusOnWebView()
    mainWindow.setAlwaysOnTop(false)
  })

  mainWindow.on('focus', () => {
    mainWindow.webContents.send('window-focused')
  })

  mainWindow.on('show', () => {
    mainWindow.focus()
    mainWindow.setAlwaysOnTop(true)
    setTimeout(() => mainWindow.setAlwaysOnTop(false), 50)
  })

  mainWindow.webContents.setWindowOpenHandler((details) => {
    shell.openExternal(details.url)
    return { action: 'deny' }
  })

  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  // Debug Wiretap
  mainWindow.webContents.on('before-input-event', (_event, input) => {
    console.log(`\n⌨️ [MAIN WIRETAP] Key Detected: "${input.key}"`)
  })
}

app.whenReady().then(() => {
  // 1. Start Database
  initDatabase()

  // 2. Register all IPC Walkie-Talkie Listeners
  registerInventoryHandlers()
  registerSalesHandlers()
  registerUserHandlers()

  electronApp.setAppUserModelId('com.electron')
  app.on('browser-window-created', (_, window) => {
    optimizer.watchWindowShortcuts(window)
  })

  createWindow()

  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
